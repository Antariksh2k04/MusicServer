# Task 1.3 — Physical phone and browser matrix

Checked 2026-10-08 after the account/origin prerequisite reviews. **Outcome: partial inventory; task remains unchecked.** The owner's confirmed target is a CMF Phone 2 Pro, latest stable installed update, and private signed APK. Exact OS/build, battery settings, and accessory/call-test availability remain unknown.

## Executed local inventory

`scripts/check-prerequisites.ps1 -Scope Device` was executed as part of the combined read-only probe. It neither starts adb nor reads device serials/changes settings. It cannot certify a physical device.

| Item | Observed / acceptance still needed |
| --- | --- |
| Chrome executable | 153.0.8010.48; installed file version only, stable channel/current-update status not verified. |
| Edge executable | 154.0.4258.62; installed file version only, stable channel/current-update status not verified. |
| adb / Java | Neither found on PATH. |
| Android SDK | No configured or usual local SDK directory found. |
| Phone model | Owner reported CMF Phone 2 Pro; no physical connection/model-property observation. |
| Android / Nothing OS / build | Exact installed stable versions, build ID, and security patch not recorded. |
| Power settings | Battery saver, app optimization, and normal-background settings not observed or modified. |
| Playback/accessories | No installed native service/APK, Bluetooth/headset controls, incoming-call fixture, or physical background result. |

## Collection once tooling/phone are available

Later owner confirmation: the debug empty shell from run `37897504459` installs and opens on the CMF Phone 2 Pro. Its artifact/source provenance is recorded in [shell evidence](android-shell-bootstrap.md). Exact Android/Nothing OS/build, normal battery settings, USB access, and accessory/call tests remain unrecorded; no native audio service or background result is implied.

Use official Android platform tools and the owner's trusted computer/USB pairing. The [Android adb guide](https://developer.android.com/tools/adb) describes debugging authorization and selecting a hardware device. Do not commit serials, account identifiers, call details, or signing keys. Inspect device presence locally; do not paste `adb devices` output into committed evidence.

With exactly one authorized hardware device, these read-only commands can capture noncredential build fields:

```powershell
adb -d shell getprop ro.product.model
adb -d shell getprop ro.build.version.release
adb -d shell getprop ro.build.version.sdk
adb -d shell getprop ro.build.display.id
adb -d shell getprop ro.build.version.security_patch
```

Confirm the marketed phone model and exact Nothing OS version in Settings → About phone, stable update status in the updater, and actual app battery configuration. Record browser version/channel in each browser's About page. Record accessory availability and a way to test incoming-call/focus interruptions without storing personal call information. Keep normal battery settings as the test baseline.

The eventual background experiment must use the installed native player on this real phone. Browser versions, generated MP3 bytes, emulator output, or a successful adb connection do not establish playback, seeking, lock-screen controls, or background renewal. Refer to the active `audio-playback` scenarios, local task 2.9, and final cloud task 8.3 (previously 2.5) instead of duplicating acceptance requirements here.

## Review

On 2026-10-09 the owner confirmed **Android 16** on the CMF Phone 2 Pro. This is an owner-reported major version, not an adb observation. Exact Nothing OS/build/security patch, battery configuration, USB authorization, and accessory/call evidence remain pending. Task 1.3 stays unchecked.

Self-review confirmed installed versions are labeled observations, stable channels are unverified, phone data is not fabricated, and no developer-option/battery/accessory action was performed. The inventory tool also skips network SDK paths. The remaining physical records require the owner's device; task 1.3 cannot pass during this unattended run.
