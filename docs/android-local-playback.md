# Local Android playback experiment

Use the debug APK on the CMF Phone 2 Pro (owner reports Android 16). This is the task 2.8 prototype, followed by task 2.9 physical validation. Normative scenarios remain in [audio-playback](../openspec/changes/first-music-stream/specs/audio-playback/spec.md). Google, MySQL, production uploads, and cloud transport are separate pending work.

## Build and install

Commit/push the reviewed native sources and run **Android shell bootstrap** manually using the existing [build guide](android-shell-build.md). The workflow now runs `testDebugUnitTest assembleDebug assembleDebugAndroidTest assembleRelease` and analyzes the built release APK for diagnostic isolation. It returns the debug APK and build report; it does not run connected-device tests. Check that run's actual result before claiming compilation. Install its APK on the phone. Different temporary CI signing certificates may require uninstalling the previous shell; that clears its local state.

For local Android tooling, run `npm --prefix src/web ci`, then `npm --prefix src/web run android:sync`. From `src/web/android`, use `./gradlew.bat testDebugUnitTest assembleDebug assembleRelease`. Do not regenerate the project with `cap add android`.

## USB connection and fixtures

Install Google's [SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools) on the PC, enable USB debugging on the phone, connect a USB data cable, and authorize this PC. With exactly one authorized physical device, in PowerShell:

```powershell
adb -d reverse tcp:5080 tcp:5080
./scripts/dev-server.ps1
```

Leave the server and USB connection running. The app uses exactly `http://127.0.0.1:5080/api/v1/diagnostics/native`; it has no editable host or LAN mode. `adb reverse` tunnels the phone's port 5080 to the PC's loopback server. The server remains bound to loopback. No `ionic serve` or phone-side Vite connection is required: the APK contains the Ionic interface.

In another PC terminal, run `npm --prefix src/web run dev`. Open `http://127.0.0.1:5173`, enter the operator key chosen at server startup, and upload owner-provided MP3 fixtures. Use that same key in the Android debug app. Refresh fixtures, select a track, then press Play. Keys are cleared from the input after submission; issued tokens remain native. Restarting the diagnostic server invalidates its temporary library and all sessions.

Use independently identified CBR, Xing/VBRI indexed VBR, representative unindexed VBR, and a track longer than 15 minutes, each under 50 MiB. Generated HTTP-test frames are not audible-playback fixtures. Keep media/hash inventory privately under `.local/`; commit only redacted results.

## Checks to record on the phone

Record exact Nothing OS/build/security patch and normal battery settings in the change's evidence. Android 16 alone does not identify the installed build. Collect noncredential properties with `adb -d shell getprop ro.build.display.id` and `adb -d shell getprop ro.build.version.security_patch`; inspect power settings and accessories on the phone.

1. Play each fixture; seek to approximately 25%, 75%, and backward before complete-file download. Record requested/audible positions and time to resume. Record unindexed VBR failures explicitly.
2. Note the player instance and native renewal/request counters under **Experiment diagnostics**. Press **Test background renewal** while playing and immediately lock/switch away. After 45 seconds, the service discards buffered audio and reopens at the current position. Unlock after at least 50 seconds: the same instance, increased native renewal count, and a new `206` request/range prove the native path ran. The default access TTL is 30 seconds. Do not pause/seek/change selection during this probe; those cancel it.
3. Separately listen for at least 15 minutes while locked/another app is foregrounded. Reopen Ionic and check position and unchanged player instance. The renewal probe intentionally interrupts the buffer; keep it separate from the uninterrupted listening check.
4. Exercise notification/lock-screen play/pause and exposed seek; Bluetooth/headset buttons; incoming call and competing audio; output disconnection. Calls/focus loss and disconnection should pause and require manual resume. A previously explicit pause must survive interruption.
5. Pause, terminate the process, and relaunch. Saved track/position should restore paused after authenticated detail validation. Force-stop survival is not promised. The checkpoint contains only a track ID, position, and timestamp.
6. Sign out while playing; confirm silence, no restoration, and native authority removal. With USB disconnected, repeat logout; the UI must say that server revocation was unconfirmed. Reconnect and sign in again. To test remote revocation, sign in on the desktop, obtain its CSRF context, and POST `/api/v1/diagnostics/native/revoke-all` with that cookie, exact Origin, and CSRF header; subsequent native ranges/refresh must fail. Never put tokens or the operator key in shell command arguments or committed evidence.

Use `./gradlew.bat connectedDebugAndroidTest` only with an authorized physical device and local tooling. The instrumentation suite checks package identity and service reattachment; it cannot establish audible/background acceptance by itself.

## Limits and cleanup

The service owns one ExoPlayer/MediaSession and a bounded native data source. Ionic receives snapshots only while foregrounded; no JS audio, renewal, or retry timer exists. Network playback recovery is limited to three delayed attempts. Credentials use Android Keystore AES-GCM and app-private no-backup storage. Capacitor logging is disabled to prevent bridge arguments from logging the operator key. The diagnostic JS module uses the plugin export injected by the pinned Capacitor 8.5.2 runtime; verify that export on the built APK, and revisit it during production bridge integration.

Native implementation and Media3 dependencies exist only in `src/debug`; the release variant has no diagnostic service, broker, or cleartext network-security allowance. Building the release variant is an isolation check, not production readiness. Private Google identity and release signing are still pending.

After recording results, sign out on each client and stop only the test server you started; its catalog cleanup targets its owned fixtures. Remove the one USB mapping with `adb -d reverse --remove tcp:5080`. USB evidence does not prove public HTTPS, cloud ranges, Wi-Fi/mobile-data recovery, or the full MVP's multi-track transitions.
