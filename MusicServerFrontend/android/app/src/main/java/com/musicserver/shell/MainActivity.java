package com.musicserver.shell;

import com.getcapacitor.BridgeActivity;
import com.getcapacitor.Plugin;
import android.os.Bundle;

public class MainActivity extends BridgeActivity {
    @Override
    public void onCreate(Bundle savedInstanceState) {
        // The local broker/service exist only in src/debug and are absent from release APKs.
        if (BuildConfig.DEBUG) {
            try {
                registerPlugin(Class.forName("com.musicserver.shell.DiagnosticPlayerPlugin").asSubclass(Plugin.class));
            } catch (ClassNotFoundException e) {
                throw new IllegalStateException("Debug player plugin is missing");
            }
        }
        super.onCreate(savedInstanceState);
    }
}
