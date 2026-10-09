# Music Server

Ionic React and ASP.NET Core local streaming experiment for the active OpenSpec change `first-music-stream`, with a Capacitor/Media3 Android debug prototype awaiting native build and physical validation. Production Google identity, MySQL persistence, and OCI storage remain pending.

## Build

Prerequisites: .NET SDK 10.0.401 and Node 24.18.0/npm 11.16.0 (or compatible versions permitted by package engines).

From the repository root:

```powershell
dotnet restore tests/server/MusicServer.Tests/MusicServer.Tests.csproj
dotnet build tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore
dotnet test tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore
npm --prefix src/web ci
npm --prefix src/web run build
npm --prefix src/web test
```

Dependency versions are pinned in project/package files; npm's lockfile pins transitive dependencies. This environment can use ignored workspace caches for offline restore. See the change's local-development evidence for the exact commands used here.

Stop the frontend development server before `npm ci`; Windows can lock the native build-tool binaries while Vite is running.

## Local experiment

After restoring dependencies, start the backend from the root:

```powershell
./scripts/dev-server.ps1
```

Enter a private 32–256 character operator key at the hidden prompt; use the same key in the browser's diagnostic sign-in. The script keeps configuration in process environment variables and restores previous values on exit. It never writes or displays the key. Stop the server with Ctrl+C. Start the frontend in another terminal:

```powershell
npm --prefix src/web run dev
```

Open `http://127.0.0.1:5173`. The Vite proxy sends `/api` to the loopback backend at port 5080. Diagnostic APIs live under `/api/v1/diagnostics`; the production health-readiness endpoint remains `503` until real dependencies exist.

Personal audio belongs only in ignored `.local/` storage, never frontend public assets. Diagnostic fixture state is temporary and is not the owner's MySQL library. Use the same browser origin throughout; restart invalidates diagnostic sessions and their temporary catalog.

Select **Upload**, choose one MP3, and wait for confirmation. Return to **Library**, select the fixture, press **Play**, and seek after duration loads. Switching views preserves the player. Reloading restores selection/position paused while the fixture and diagnostic session still exist. **Reload track** rechecks session/availability and loads paused. A failed or cancelled upload is never presented as confirmed saved; refresh before retrying an ambiguous transfer.

The local probe checks MP3 frame structure and supplies filename/unknown-tag fallbacks. Embedded metadata extraction remains a production task. This browser experiment has no cloud access, database, or Google login. The separate Android debug prototype uses a native background service; follow [Local Android playback](docs/android-local-playback.md) to build and test it over USB. Playback on the physical phone has not yet been verified.

## Local configuration and limits

The startup script fixes `Development`, diagnostics enabled, `http://127.0.0.1:5173` as browser origin, and repository `.local/media` storage. The backend binds IPv4 loopback port 5080; Vite uses port 5173 and fails if occupied. Use these addresses exactly.

Backend environment settings use double underscores, for example `Diagnostics__SessionSeconds`. Defaults are 1,800-second sessions (maximum 3,600), 250 MiB provider payload capacity, 100 temporary fixtures, and 50 MiB per file. Validation uses one additional staging file of at most 50 MiB; provider pending writes count toward the 250 MiB cap. Small metadata/lock files are additional disk usage. `Diagnostics__StorageOperationLimit` defaults to 10,000 provider calls per process; failed calls count and exhausted admission requires a restart. These are local test limits. `Local__Port` can change backend port only if the Vite proxy is also changed. `Diagnostics__OperatorKey` can supply an existing process-only key for private automation.

Diagnostic cookies are HttpOnly/SameSite Strict, with Origin and antiforgery checks on mutations. They intentionally serve only local HTTP with distinct diagnostic names. Native debug sessions have 30-second access (`Diagnostics__NativeAccessSeconds`, maximum 600), rotating refresh authority, and the same absolute session lifetime; tokens are native headers, not media URLs. Browser-context native issuance and mixed cookie/bearer requests are rejected. Diagnostics are absent unless explicitly enabled in Development; liveness is 200 and production readiness remains 503. Do not expose this experiment publicly. Normal shutdown attempts to remove owned fixture files; a crash may leave ignored orphan files, which are not reimported on restart.

Uploads and streams now use the provider-independent `IObjectStore` boundary. Its private filesystem implementation requires an absolute local directory, explicit enablement, and Development; production does not register it. Read the [storage evidence](openspec/changes/first-music-stream/evidence/local-storage.md) for contracts, failure behavior, and cleanup limitations. No Oracle account is needed for these local checks.

## Manual playback check still needed

Use your own valid CBR and VBR MP3s in an actual Chrome/Edge session. Confirm audible playback, pause/resume, seeks near 25%/50%/90%, view changes, and paused restoration after reload. Inspect `/stream` requests for successful full/range responses. Stop the backend or expire the diagnostic session and check failure/recovery messages. Record results in the change's evidence; automated byte/media-event tests do not prove decoding or audible seek accuracy. Android background playback requires a separate native and physical-phone experiment.

## Requirements

The owner deferred Oracle to the final phase. The active [task list](openspec/changes/first-music-stream/tasks.md) now orders local Android/background investigation and application development with local MySQL/private test storage before Oracle setup, OCI integration, and deployed acceptance. Private cloud storage remains required for release. Android tooling and package access remain independent local blockers.

Read-only prerequisite inventory when each corresponding phase is reached:

```powershell
./scripts/check-prerequisites.ps1 -Scope All
./scripts/check-public-origin.ps1
node --test tests/scripts/prerequisites.test.cjs
```

These commands report available local tooling/configuration and validate probe inputs. Their JSON always leaves `gateCompleted` false; a successful command exit is not a release-gate pass. A public-origin request occurs only when an operator supplies `-Origin` with an HTTPS hostname. Account, TLS/Google, and physical-device evidence still needs the actual resources. See the change's environment/public-origin/device-matrix reports for remaining steps.

## Android setup status

The owner selected remote compilation. Successful run `37897504459` produced the empty shell APK; its hashes and source manifest/template pins were checked locally, and the owner confirmed installation/launch on the CMF phone. The reviewed project and real Capacitor lockfile are now under `src/web/`. The debug prototype now adds Media3 playback and native diagnostic authority, awaiting a new native build and physical checks. Follow [Remote Android shell build](docs/android-shell-build.md), then [Local Android playback](docs/android-local-playback.md). The separate Ionic entry builds with `npm --prefix src/web run build:native-shell`; it does not load browser diagnostics/audio. The owner reports Android 16; exact Nothing OS/build, the full toolchain/host gate, and native playback acceptance remain pending.

Task 1.4 has selected native versions in `scripts/android-toolchain.json`. Java/SDK are not installed locally; Capacitor 8.5.2 is pinned in the checked-in package manifest/lockfile but has not been restored on this PC. For optional local compilation, install [Android Studio](https://developer.android.com/studio) 2025.2.1 or newer, select Gradle JDK 21, and use SDK Manager for Platform 36, Build Tools 35.0.0, and Platform Tools 37.0.1. Then check local metadata:

```powershell
./scripts/check-android-toolchain.ps1
node --test tests/scripts/android-toolchain.test.cjs
# For nonstandard installation locations:
# ./scripts/check-android-toolchain.ps1 -JdkDirectory $env:JAVA_HOME -SdkDirectory $env:ANDROID_HOME
```

The inventory does not execute tools, accept licences, or verify an APK. Registry access remains blocked locally; the runner restores the reviewed Capacitor dependencies from the genuine lockfile. See [toolchain evidence](openspec/changes/first-music-stream/evidence/toolchain.md) for pins and [remote bootstrap evidence](openspec/changes/first-music-stream/evidence/android-shell-bootstrap.md) for executed checks and remaining gates. These prerequisites do not require Oracle. `ionic serve`/Vite runs the browser client; `npm --prefix src/web run android:sync` prepares the existing native project after restoring dependencies. Local native open/run commands still need appropriate tooling. Local Platform Tools/USB access is needed for the early loopback playback experiment.

Read `AGENTS.md` and the active change's proposal/specs/design/tasks before contributing. Root FRS/SDS documents are project context; OpenSpec owns detailed active requirements. See `openspec/changes/first-music-stream/evidence/` for validation and remaining gates.
