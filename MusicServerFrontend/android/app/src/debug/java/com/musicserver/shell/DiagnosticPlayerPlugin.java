package com.musicserver.shell;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import org.json.JSONObject;

@CapacitorPlugin(name = "DiagnosticPlayer")
public final class DiagnosticPlayerPlugin extends Plugin {
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private final List<Runnable> waiting = new ArrayList<>();
    private DiagnosticPlaybackService service;
    private DiagnosticBroker broker;
    private boolean bound;
    private boolean destroyed;
    private boolean foreground = true;
    private final ServiceConnection connection = new ServiceConnection() {
        @Override public void onServiceConnected(ComponentName name, IBinder binder) {
            service = ((DiagnosticPlaybackService.LocalBinder)binder).service();
            List<Runnable> ready = new ArrayList<>(waiting);
            waiting.clear();
            for (Runnable pending : ready) pending.run();
        }
        @Override public void onServiceDisconnected(ComponentName name) { service = null; }
    };
    private final Runnable tick = new Runnable() {
        @Override public void run() {
            if (!destroyed && foreground && service != null) notifyListeners("snapshot", object(service.snapshot()));
            if (!destroyed) handler.postDelayed(this, 1000);
        }
    };

    @Override public void load() {
        broker = DiagnosticBroker.get(getContext());
        Intent intent = new Intent(getContext(), DiagnosticPlaybackService.class).setAction(DiagnosticPlaybackService.BIND_LOCAL);
        bound = getContext().bindService(intent, connection, Context.BIND_AUTO_CREATE);
        handler.post(tick);
    }

    private void withService(PluginCall call, Runnable action) {
        handler.post(() -> {
            if (destroyed || !bound) { call.reject("Native service is unavailable", "serviceUnavailable"); return; }
            if (service == null) {
                if (waiting.size() >= 16) call.reject("Native service is connecting", "serviceUnavailable");
                else waiting.add(action);
            } else action.run();
        });
    }

    private static JSObject object(JSONObject result) {
        JSObject value = new JSObject();
        java.util.Iterator<String> keys = result.keys();
        while (keys.hasNext()) { String key = keys.next(); value.put(key, result.opt(key)); }
        return value;
    }

    @PluginMethod public void attach(PluginCall call) { withService(call, () -> call.resolve(object(service.snapshot()))); }

    @PluginMethod public void signIn(PluginCall call) {
        String key = call.getString("operatorKey", "");
        if (key.length() < 32 || key.length() > 256) { call.reject("Enter the local operator key", "invalidKey"); return; }
        withService(call, () -> {
            service.clearPlayer();
            long expected = broker.invalidate();
            worker.execute(() -> {
                try {
                    broker.login(key, expected);
                    handler.post(() -> {
                        if (destroyed || service == null || broker.epoch() != expected) { cancelled(call); return; }
                        call.resolve(object(service.snapshot()));
                    });
                } catch (IOException e) { reject(call, e); }
            });
        });
    }

    @PluginMethod public void list(PluginCall call) {
        worker.execute(() -> {
            try { JSONObject result = broker.list(); handler.post(() -> { if (!destroyed) call.resolve(object(result)); }); }
            catch (IOException e) { reject(call, e); }
        });
    }

    @PluginMethod public void select(PluginCall call) {
        String id = call.getString("trackId", "");
        withService(call, () -> {
            DiagnosticPlaybackService target = service;
            long expected = target.beginSelection();
            worker.execute(() -> {
                try {
                    JSONObject track = broker.track(id);
                    handler.post(() -> {
                        if (destroyed || service != target || !target.current(expected)) { cancelled(call); return; }
                        try { service.select(track, expected, 0); call.resolve(object(service.snapshot())); }
                        catch (Exception e) { call.reject("Track selection failed", "selectionFailed"); }
                    });
                } catch (IOException e) { reject(call, e); }
            });
        });
    }

    private boolean targetsCurrent(PluginCall call) {
        JSONObject snapshot = service.snapshot();
        JSONObject track = snapshot.optJSONObject("track");
        return track != null && snapshot.optString("instanceId").equals(call.getString("instanceId"))
            && track.optString("id").equals(call.getString("trackId"));
    }

    @PluginMethod public void play(PluginCall call) {
        withService(call, () -> {
            if (!targetsCurrent(call)) { cancelled(call); return; }
            service.playTrack(); call.resolve(object(service.snapshot()));
        });
    }

    @PluginMethod public void pause(PluginCall call) {
        withService(call, () -> {
            if (!targetsCurrent(call)) { cancelled(call); return; }
            service.pauseTrack(); call.resolve(object(service.snapshot()));
        });
    }

    @PluginMethod public void seek(PluginCall call) {
        Double value = call.getDouble("positionMs");
        if (value == null || !Double.isFinite(value) || value < 0 || value > 24 * 60 * 60 * 1000L) {
            call.reject("Invalid seek position", "invalidSeek"); return;
        }
        withService(call, () -> {
            if (!targetsCurrent(call)) { cancelled(call); return; }
            service.seekTrack(value.longValue()); call.resolve(object(service.snapshot()));
        });
    }

    @PluginMethod public void armBackgroundProbe(PluginCall call) {
        withService(call, () -> {
            if (!targetsCurrent(call)) { cancelled(call); return; }
            service.armProbe(); call.resolve(object(service.snapshot()));
        });
    }

    @PluginMethod public void logout(PluginCall call) {
        // Stop and fence late credential responses before any network round-trip or worker wait.
        broker.invalidate();
        withService(call, () -> {
            service.clearPlayer();
            worker.execute(() -> {
                boolean revoked = broker.clearAndRevoke();
                handler.post(() -> {
                    if (destroyed || service == null) { cancelled(call); return; }
                    JSONObject result = service.snapshot();
                    try { result.put("serverRevoked", revoked); } catch (org.json.JSONException ignored) { }
                    call.resolve(object(result));
                });
            });
        });
    }

    private void reject(PluginCall call, IOException failure) {
        String code = failure instanceof DiagnosticBroker.HttpFailure ? failure.getMessage()
            : "cancelled".equals(failure.getMessage()) ? "cancelled" : "networkOrStorageFailure";
        handler.post(() -> { if (!destroyed) call.reject("Local diagnostic request failed", code); });
    }
    private static void cancelled(PluginCall call) { call.reject("A newer command replaced this request", "cancelled"); }

    @Override protected void handleOnPause() { foreground = false; }
    @Override protected void handleOnResume() { foreground = true; }
    @Override protected void handleOnDestroy() {
        destroyed = true;
        handler.removeCallbacksAndMessages(null);
        waiting.clear();
        if (bound) getContext().unbindService(connection);
        bound = false;
        service = null;
        worker.shutdownNow(); // The service/player/broker are app-owned, not activity-owned.
    }
}
