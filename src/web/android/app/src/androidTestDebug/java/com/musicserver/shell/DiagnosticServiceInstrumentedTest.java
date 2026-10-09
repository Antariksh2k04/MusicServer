package com.musicserver.shell;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.IBinder;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import org.junit.Test;
import org.junit.runner.RunWith;

@RunWith(AndroidJUnit4.class)
public final class DiagnosticServiceInstrumentedTest {
    @Test public void TwoUiBindings_AttachToSameServicePlayer() throws Exception {
        Context context = InstrumentationRegistry.getInstrumentation().getTargetContext();
        CountDownLatch attached = new CountDownLatch(2);
        String[] instances = new String[2];
        ServiceConnection[] connections = new ServiceConnection[2];
        boolean[] bound = new boolean[2];
        try {
            for (int i = 0; i < 2; i++) {
                final int index = i;
                connections[i] = new ServiceConnection() {
                    @Override public void onServiceConnected(ComponentName name, IBinder binder) {
                        DiagnosticPlaybackService service = ((DiagnosticPlaybackService.LocalBinder)binder).service();
                        instances[index] = service.snapshot().optString("instanceId");
                        attached.countDown();
                    }
                    @Override public void onServiceDisconnected(ComponentName name) { }
                };
                bound[i] = context.bindService(new Intent(context, DiagnosticPlaybackService.class)
                    .setAction(DiagnosticPlaybackService.BIND_LOCAL), connections[i], Context.BIND_AUTO_CREATE);
                assertTrue(bound[i]);
            }
            assertTrue("Native service bindings timed out", attached.await(10, TimeUnit.SECONDS));
            assertTrue(instances[0] != null && !instances[0].isEmpty());
            assertEquals(instances[0], instances[1]);
        } finally {
            for (int i = 0; i < 2; i++) if (bound[i]) context.unbindService(connections[i]);
        }
    }
}
