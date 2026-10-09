# Local Android playback experiment

Use the debug APK on the CMF Phone 2 Pro (owner reports Android 16). This is the task 2.8 prototype, followed by task 2.9 physical validation. Normative scenarios remain in [audio-playback](../openspec/changes/first-music-stream/specs/audio-playback/spec.md). Google, MySQL, production uploads, and cloud transport are separate pending work.

## Build and install

Commit/push the reviewed native sources and run **Android shell bootstrap** manually using the existing [build guide](android-shell-build.md). The workflow now runs `testDebugUnitTest assembleDebug assembleDebugAndroidTest assembleRelease` and analyzes the built release APK for diagnostic isolation. It returns the debug APK and build report; it does not run connected-device tests. Check that run's actual result before claiming compilation. Install its APK on the phone. Different temporary CI signing certificates may require uninstalling the previous shell; that clears its local state.

For local Android tooling, run `npm --prefix src/web ci`, then `npm --prefix src/web run android:sync`. From `src/web/android`, use `./gradlew.bat testDebugUnitTest assembleDebug assembleRelease`. Do not regenerate the project with `cap add android`.

## Wireless connection and fixtures

The owner cannot use USB with this laptop and confirmed that the phone and laptop can share Wi-Fi. Android 16 supports [wireless ADB pairing](https://developer.android.com/tools/adb#connect-to-a-device-over-wi-fi) without an initial USB connection. Download Google's [SDK Platform Tools for Windows](https://developer.android.com/tools/releases/platform-tools), accept the download terms yourself, and extract the ZIP so `.tools/platform-tools/adb.exe` exists in this repository. This ignored tool directory needs no global PATH edit or Android Studio installation.

Connect both devices to the same Wi-Fi. On the phone, enable **Developer options → Wireless debugging → Pair device with pairing code**. Keep that dialog open and use its pairing IP/port below. Enter the temporary code only at the local terminal prompt. Then return to the main Wireless debugging screen and use its **IP address & port** for the connection; the connection port is different from the pairing port. Replace the quoted placeholders in PowerShell, from the repository root:

```powershell
$adbPath = Join-Path $PWD '.tools/platform-tools/adb.exe'
& $adbPath version
& $adbPath pair 'PHONE_IP:PAIRING_PORT'
$phoneEndpoint = 'PHONE_IP:CONNECTION_PORT'
& $adbPath connect $phoneEndpoint
& $adbPath -s $phoneEndpoint get-state
& $adbPath -s $phoneEndpoint reverse tcp:5080 tcp:5080
& $adbPath -s $phoneEndpoint reverse --list
./scripts/dev-server.ps1
```

`get-state` must return `device`, and the reverse list must contain `tcp:5080 tcp:5080`, before playback testing. Keep the server and wireless debugging connection running. The app uses exactly `http://127.0.0.1:5080/api/v1/diagnostics/native`; it has no editable host or LAN mode. `adb reverse` tunnels the phone's port 5080 to the PC's loopback server through the paired debugging connection. The server remains bound to loopback; do not change its binding or expose port 5080 to the LAN. No `ionic serve` or phone-side Vite connection is required: the APK contains the Ionic interface.

If pairing succeeds but connection fails, use the current connection port, not the expired pairing port. If automatic discovery fails, explicit `adb connect` is sufficient when the devices can reach each other. Guest/corporate Wi-Fi may isolate devices; use a shared network that permits the debugging connection. When the endpoint changes or the connection drops, reconnect and recreate the one reverse mapping. Use `-s $phoneEndpoint` for wireless commands; `-d` selects USB devices. If USB is available on another computer, authorize that device and use its serial with `-s` instead.

In another PC terminal, run `npm --prefix src/web run dev`. Open `http://127.0.0.1:5173`, enter the operator key chosen at server startup, and upload owner-provided MP3 fixtures. Use that same key in the Android debug app. Refresh fixtures, select a track, then press Play. Keys are cleared from the input after submission; issued tokens remain native. Restarting the diagnostic server invalidates its temporary library and all sessions.

Use independently identified CBR, Xing/VBRI indexed VBR, representative unindexed VBR, and a track longer than 15 minutes, each under 50 MiB. Generated HTTP-test frames are not audible-playback fixtures. Keep media/hash inventory privately under `.local/`; commit only redacted results.

## Checks to record on the phone

Record exact Nothing OS/build/security patch and normal battery settings in the change's evidence. Android 16 alone does not identify the installed build. Collect noncredential properties with `& $adbPath -s $phoneEndpoint shell getprop ro.build.display.id` and `& $adbPath -s $phoneEndpoint shell getprop ro.build.version.security_patch`; inspect power settings and accessories on the phone. Record wireless tunnel connectivity during locked-screen testing; a lost ADB transport is distinct from a player lifecycle failure.

1. Play each fixture; seek to approximately 25%, 75%, and backward before complete-file download. Record requested/audible positions and time to resume. Record unindexed VBR failures explicitly.
2. Note the player instance and native renewal/request counters under **Experiment diagnostics**. Press **Test background renewal** while playing and immediately lock/switch away. After 45 seconds, the service discards buffered audio and reopens at the current position. Unlock after at least 50 seconds: the same instance, increased native renewal count, and a new `206` request/range prove the native path ran. The default access TTL is 30 seconds. Do not pause/seek/change selection during this probe; those cancel it.
3. Separately listen for at least 15 minutes while locked/another app is foregrounded. Reopen Ionic and check position and unchanged player instance. The renewal probe intentionally interrupts the buffer; keep it separate from the uninterrupted listening check.
4. Exercise notification/lock-screen play/pause and exposed seek; Bluetooth/headset buttons; incoming call and competing audio; output disconnection. Calls/focus loss and disconnection should pause and require manual resume. A previously explicit pause must survive interruption.
5. Pause, terminate the process, and relaunch. Saved track/position should restore paused after authenticated detail validation. Force-stop survival is not promised. The checkpoint contains only a track ID, position, and timestamp.
6. Sign out while playing; confirm silence, no restoration, and native authority removal. For offline logout, sign in again, then remove only the reverse mapping using the cleanup command below and repeat logout; the UI must say that server revocation was unconfirmed. Recreate the mapping and sign in again. To test remote revocation, sign in on the desktop, obtain its CSRF context, and POST `/api/v1/diagnostics/native/revoke-all` with that cookie, exact Origin, and CSRF header; subsequent native ranges/refresh must fail. Never put tokens or the operator key in shell command arguments or committed evidence.

Use `./gradlew.bat connectedDebugAndroidTest` only with an authorized physical device and local tooling. The instrumentation suite checks package identity and service reattachment; it cannot establish audible/background acceptance by itself.

## Limits and cleanup

The service owns one ExoPlayer/MediaSession and a bounded native data source. Ionic receives snapshots only while foregrounded; no JS audio, renewal, or retry timer exists. Network playback recovery is limited to three delayed attempts. Credentials use Android Keystore AES-GCM and app-private no-backup storage. Capacitor logging is disabled to prevent bridge arguments from logging the operator key. The diagnostic JS module uses the plugin export injected by the pinned Capacitor 8.5.2 runtime; verify that export on the built APK, and revisit it during production bridge integration.

Native implementation and Media3 dependencies exist only in `src/debug`; the release variant has no diagnostic service, broker, or cleartext network-security allowance. Building the release variant is an isolation check, not production readiness. Private Google identity and release signing are still pending.

After recording results, sign out on each client and stop only the test server you started; its catalog cleanup targets its owned fixtures. Remove only this mapping and disconnect this endpoint:

```powershell
& $adbPath -s $phoneEndpoint reverse --remove tcp:5080
& $adbPath disconnect $phoneEndpoint
```

Turn off Wireless debugging on the phone when finished. A successful paired ADB tunnel, including one over Wi-Fi, does not establish the deployed app's public HTTPS, cloud ranges, Wi-Fi/mobile-data handover, or the full MVP's multi-track transitions. Physical background playback remains unverified until the scenarios above are executed.
