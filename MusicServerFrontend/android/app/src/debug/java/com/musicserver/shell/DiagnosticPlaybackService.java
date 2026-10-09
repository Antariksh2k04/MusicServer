package com.musicserver.shell;

import android.app.PendingIntent;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Binder;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import androidx.media3.common.AudioAttributes;
import androidx.media3.common.C;
import androidx.media3.common.ForwardingPlayer;
import androidx.media3.common.MediaItem;
import androidx.media3.common.MediaMetadata;
import androidx.media3.common.PlaybackException;
import androidx.media3.common.Player;
import androidx.media3.exoplayer.ExoPlayer;
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory;
import androidx.media3.exoplayer.upstream.DefaultLoadErrorHandlingPolicy;
import androidx.media3.session.MediaSession;
import androidx.media3.session.MediaSessionService;
import androidx.media3.session.SessionCommands;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicLong;
import org.json.JSONException;
import org.json.JSONObject;

/** Owns the only Android player. The activity only binds; it never constructs an engine. */
public final class DiagnosticPlaybackService extends MediaSessionService {
    static final String BIND_LOCAL = "com.musicserver.shell.BIND_DIAGNOSTIC_PLAYER";
    private static final AtomicLong snapshotSequence = new AtomicLong();
    final class LocalBinder extends Binder { DiagnosticPlaybackService service() { return DiagnosticPlaybackService.this; } }
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private final String instanceId = UUID.randomUUID().toString();
    private ExoPlayer player;
    private MediaSession session;
    private DiagnosticBroker broker;
    private SharedPreferences checkpoint;
    private JSONObject track;
    private long intent;
    private boolean playRequested;
    private boolean destroyed;
    private int retries;
    private String error = "";
    private String probe = "idle";
    private Runnable pendingRetry;
    private Runnable pendingProbe;
    private final Runnable saveTick = new Runnable() {
        @Override public void run() { save(); handler.postDelayed(this, 10000); }
    };

    @Override
    public void onCreate() {
        super.onCreate();
        broker = DiagnosticBroker.get(this);
        checkpoint = getSharedPreferences("diagnostic-checkpoint-v1", MODE_PRIVATE);
        DefaultMediaSourceFactory sources = new DefaultMediaSourceFactory(() -> new DiagnosticDataSource(broker))
            .setLoadErrorHandlingPolicy(new DefaultLoadErrorHandlingPolicy(0) {
                @Override public long getRetryDelayMsFor(LoadErrorInfo info) { return C.TIME_UNSET; }
            });
        player = new ExoPlayer.Builder(this).setMediaSourceFactory(sources).build();
        player.setAudioAttributes(new AudioAttributes.Builder().setUsage(C.USAGE_MEDIA)
            .setContentType(C.AUDIO_CONTENT_TYPE_MUSIC).build(), true);
        player.setHandleAudioBecomingNoisy(true);
        player.setWakeMode(C.WAKE_MODE_LOCAL);
        player.addListener(new Player.Listener() {
            @Override public void onPlaybackSuppressionReasonChanged(int reason) {
                if (reason == Player.PLAYBACK_SUPPRESSION_REASON_TRANSIENT_AUDIO_FOCUS_LOSS) interrupt();
            }
            @Override public void onPlayWhenReadyChanged(boolean ready, int reason) {
                if (!ready && (reason == Player.PLAY_WHEN_READY_CHANGE_REASON_AUDIO_FOCUS_LOSS
                    || reason == Player.PLAY_WHEN_READY_CHANGE_REASON_AUDIO_BECOMING_NOISY)) interrupt();
                if (!ready) save();
            }
            @Override public void onPlaybackStateChanged(int state) {
                if (state == Player.STATE_ENDED) { playRequested = false; newIntent(); save(); }
            }
            @Override public void onPlayerError(PlaybackException failure) { recover(failure); }
        });
        Player systemPlayer = new ForwardingPlayer(player) {
            @Override public void play() { playTrack(); }
            @Override public void pause() { pauseTrack(); }
            @Override public void setPlayWhenReady(boolean value) { if (value) playTrack(); else pauseTrack(); }
            @Override public void seekTo(long position) { seekTrack(position); }
            @Override public void seekTo(int index, long position) { if (index == 0) seekTrack(position); }
            @Override public void stop() { pauseTrack(); DiagnosticPlaybackService.this.player.stop(); }
            @Override public Player.Commands getAvailableCommands() { return systemCommands(); }
        };
        PendingIntent activity = PendingIntent.getActivity(this, 0, new Intent(this, MainActivity.class),
            PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
        session = new MediaSession.Builder(this, systemPlayer).setSessionActivity(activity)
            .setCallback(new MediaSession.Callback() {
                @Override public MediaSession.ConnectionResult onConnect(MediaSession current, MediaSession.ControllerInfo controller) {
                    if (!controller.isTrusted()) return MediaSession.ConnectionResult.reject();
                    return new MediaSession.ConnectionResult.AcceptedResultBuilder(current, controller)
                        .setAvailablePlayerCommands(systemCommands()).setAvailableSessionCommands(SessionCommands.EMPTY).build();
                }
            }).build();
        addSession(session);
        setListener(new MediaSessionService.Listener() {
            @Override public void onForegroundServiceStartNotAllowedException() {
                pauseTrack();
                error = "backgroundStartDenied";
            }
        });
        broker.onUnauthorized(() -> handler.post(() -> {
            if (!destroyed && !broker.signedIn()) { clearPlayer(); error = "signInRequired"; }
        }));
        handler.post(saveTick);
        restorePaused();
    }

    private static Player.Commands systemCommands() {
        return new Player.Commands.Builder().addAllReadOnlyCommands().addAll(Player.COMMAND_PLAY_PAUSE,
            Player.COMMAND_PREPARE, Player.COMMAND_STOP, Player.COMMAND_SEEK_IN_CURRENT_MEDIA_ITEM).build();
    }

    @Override public MediaSession onGetSession(MediaSession.ControllerInfo controller) {
        return controller.isTrusted() ? session : null;
    }

    @Override public IBinder onBind(Intent request) {
        if (BIND_LOCAL.equals(request.getAction())) return new LocalBinder();
        return super.onBind(request);
    }

    long beginSelection() { pauseTrack(); error = ""; return intent; }
    boolean current(long expected) { return !destroyed && expected == intent; }

    void select(JSONObject next, long expected, long position) throws Exception {
        if (!current(expected) || !broker.signedIn()) return;
        String id = DiagnosticBroker.checkedId(next.getString("id"));
        track = next;
        MediaItem item = new MediaItem.Builder().setMediaId(id).setUri(DiagnosticBroker.BASE + "/fixtures/" + id + "/stream")
            .setMimeType("audio/mpeg").setMediaMetadata(new MediaMetadata.Builder().setTitle(next.optString("title"))
                .setArtist(next.optString("artist")).setAlbumTitle(next.optString("album")).build()).build();
        player.setMediaItem(item, Math.max(0, position));
        player.setPlayWhenReady(false);
        player.prepare();
        retries = 0;
        save();
    }

    void playTrack() {
        if (track == null || !broker.signedIn()) return;
        if (playRequested && player.getPlayerError() == null) return;
        newIntent();
        error = "";
        retries = 0;
        playRequested = true;
        if (player.getPlaybackState() == Player.STATE_ENDED) player.seekTo(0);
        if (player.getPlayerError() != null || player.getPlaybackState() == Player.STATE_IDLE) player.prepare();
        player.play();
    }

    void pauseTrack() {
        newIntent();
        playRequested = false;
        player.pause();
        save();
    }

    void seekTrack(long position) {
        if (track == null) return;
        newIntent();
        long duration = player.getDuration();
        player.seekTo(Math.max(0, duration == C.TIME_UNSET ? position : Math.min(position, duration)));
        save();
    }

    void clearPlayer() {
        pauseTrack();
        track = null;
        player.stop();
        player.clearMediaItems();
        checkpoint.edit().clear().commit();
        error = "";
    }

    void armProbe() {
        if (track == null || !playRequested) return;
        if (pendingProbe != null) handler.removeCallbacks(pendingProbe);
        long expected = intent;
        probe = "scheduled";
        pendingProbe = () -> {
            pendingProbe = null;
            if (!current(expected) || !playRequested) { probe = "cancelled"; return; }
            // Stop discards buffered audio, so the request after the 30s access TTL cannot be
            // satisfied by an old response. This entire probe runs with Ionic suspended.
            long position = player.getCurrentPosition();
            player.stop();
            player.seekTo(position);
            player.prepare();
            player.play();
            probe = "reopened-at-" + position;
        };
        handler.postDelayed(pendingProbe, 45000);
    }

    JSONObject snapshot() {
        JSONObject result = new JSONObject();
        try {
            long duration = player.getDuration();
            result.put("instanceId", instanceId).put("revision", snapshotSequence.incrementAndGet()).put("signedIn", broker.signedIn())
                .put("track", track == null ? JSONObject.NULL : track).put("positionMs", player.getCurrentPosition())
                .put("durationMs", duration == C.TIME_UNSET ? JSONObject.NULL : duration)
                .put("status", !error.isEmpty() ? "error" : player.isPlaying() ? "playing"
                    : player.getPlaybackState() == Player.STATE_BUFFERING ? "loading" : "paused")
                .put("error", error).put("probe", probe).put("auth", broker.diagnostics())
                .put("requests", DiagnosticDataSource.requests.get()).put("lastRangeStart", DiagnosticDataSource.lastRangeStart)
                .put("lastHttpStatus", DiagnosticDataSource.lastStatus);
        } catch (JSONException ignored) { }
        return result;
    }

    private void newIntent() {
        intent++;
        if (pendingRetry != null) handler.removeCallbacks(pendingRetry);
        if (pendingProbe != null) { handler.removeCallbacks(pendingProbe); probe = "cancelled"; }
        pendingRetry = null;
        pendingProbe = null;
    }

    private void interrupt() { pauseTrack(); }

    private void recover(PlaybackException failure) {
        int status = 0;
        Throwable cause = failure;
        while (cause != null) {
            if (cause instanceof DiagnosticBroker.HttpFailure) { status = ((DiagnosticBroker.HttpFailure)cause).status; break; }
            cause = cause.getCause();
        }
        error = status == 401 ? "signInRequired" : status == 404 ? "trackUnavailable" : "playbackFailed";
        if (status == 401 || status == 404 || !playRequested || retries >= 3
            || failure.errorCode >= PlaybackException.ERROR_CODE_PARSING_CONTAINER_MALFORMED
                && failure.errorCode <= PlaybackException.ERROR_CODE_DECODING_FORMAT_UNSUPPORTED) {
            playRequested = false;
            player.pause();
            save();
            return;
        }
        long expected = intent;
        long position = player.getCurrentPosition();
        long delay = 1000L << retries++;
        pendingRetry = () -> {
            pendingRetry = null;
            if (!current(expected) || !playRequested || !broker.signedIn()) return;
            error = "";
            player.seekTo(position);
            player.prepare();
            player.play();
        };
        handler.postDelayed(pendingRetry, delay);
    }

    private void save() {
        if (track == null) return;
        checkpoint.edit().putString("trackId", track.optString("id"))
            .putLong("positionMs", player.getCurrentPosition()).putLong("savedAt", System.currentTimeMillis()).apply();
    }

    private void restorePaused() {
        String id = checkpoint.getString("trackId", null);
        if (id == null || !broker.signedIn()) return;
        long position = checkpoint.getLong("positionMs", 0);
        long expected = intent;
        worker.execute(() -> {
            try {
                JSONObject restored = broker.track(id);
                handler.post(() -> {
                    if (!current(expected)) return;
                    try { select(restored, expected, position); }
                    catch (Exception ignored) { error = "restoreFailed"; }
                });
            } catch (Exception ignored) {
                handler.post(() -> { if (current(expected)) error = broker.signedIn() ? "restoreFailed" : "signInRequired"; });
            }
        });
    }

    @Override public void onDestroy() {
        save();
        destroyed = true;
        handler.removeCallbacksAndMessages(null);
        broker.onUnauthorized(() -> {});
        worker.shutdownNow();
        session.release();
        player.release();
        super.onDestroy();
    }
}
