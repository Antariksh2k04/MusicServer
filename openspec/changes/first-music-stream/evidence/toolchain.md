# Task 1.4 — Toolchain selection and local smoke checks

Checked 2026-10-08. **Partial; task 1.4 remains unchecked.** The owner authorized this ticket and the Android shell next if prerequisites are available. No Android project, APK, playback service, or cloud resource was created. Root planning documents are preserved.

## Version selection

Existing project/package files and the npm lockfile govern installed dependencies. `scripts/android-toolchain.json` records the selected native versions for the future shell and the read-only inventory script; it does not install or configure them. After project generation, reconcile actual Gradle/configuration files against these pins. Dependency changes require restore/build evidence.

| Component | Selected version / verification |
| --- | --- |
| Node / npm | 24.18.0 / 11.16.0, observed locally. |
| Ionic React / React / Vite / TypeScript | 9.0.5 / 19.2.8 / 8.3.0 / 5.9.3, installed and pinned in `src/web/package.json`; dependency inventory and web build passed. |
| Capacitor core / CLI / Android | 8.5.2 for all three. Core/CLI archives and their real manifests exist in the public local cache, but no Capacitor package is installed in this project. Android archive is absent. |
| Android Studio | Minimum 2025.2.1; absent from the usual installation location. Record exact installed IDE/JBR build after setup. |
| Java | JDK 21 including `javac`; absent. Exact distribution/patch still needs installation evidence. |
| Android SDK | Minimum app API 24; compile/target API 36; Build Tools 35.0.0; Platform Tools 37.0.1. None found. Platform Tools are distinct from compile/target API. |
| Native build | AGP 8.13.0, Gradle wrapper 8.14.3, Kotlin 2.2.20 when adding Kotlin bridge code; not restored or built. Use the official Capacitor template, rather than inventing wrapper binaries or lockfile integrity. |
| Media3 | 1.11.1 for ExoPlayer/session modules; not installed. Resolve AAR metadata/transitives with the pinned SDK before declaring compatibility. No video/Compose/transcoding modules are needed for this slice. |
| .NET | SDK 10.0.401 from `global.json`, `net10.0`; local backend/test restore and build passed. No Linux ARM64 runtime smoke check. |
| MySQL | Select Community 8.4.12/InnoDB for the eventual host. A later local inventory found an existing 8.0.46 installation and running service; no selected-version or application database durability evidence. |
| Backend packages | Select MySqlConnector 2.6.2, Dapper 2.1.89, TagLibSharp 2.3.0 for later modules. These exact versions are published; they have not been added/restored or tested together. Older cached driver/Dapper versions do not establish these pins. |

## Compatibility sources and remaining uncertainty

[Capacitor's environment guide](https://capacitorjs.com/docs/getting-started/environment-setup) specifies Node 22+ and Android Studio 2025.2.1+. The [8.0 native upgrade guide](https://capacitorjs.com/docs/updating/8-0) establishes API 24/36, AGP 8.13.0, Gradle 8.14.3, and Kotlin 2.2.20. The [8.5 change](https://capacitorjs.com/docs/updating/8-5) concerns iOS; this project only targets Android/web.

The exact [Capacitor Android 8.5.2 source](https://raw.githubusercontent.com/ionic-team/capacitor/8.5.2/android/capacitor/build.gradle) uses Java 21 source/target compatibility. This is stricter than the [AGP 8.13 minimum JDK 17](https://developer.android.com/build/releases/agp-8-13-0-release-notes). Select a Gradle JDK 21 explicitly; finding Java 17 is insufficient. Build Tools 35.0.0 is AGP's documented default. [Platform Tools release notes](https://developer.android.com/tools/releases/platform-tools) identify 37.0.1 independently of platform API 36. A successful native build still needs to resolve the actual AndroidX/Media3 dependencies.

[Media3 release notes](https://developer.android.com/jetpack/androidx/releases/media3) publish 1.11.1 and the ExoPlayer/session modules. [MySQL release notes](https://dev.mysql.com/doc/relnotes/mysql/8.4/en/news-8-4-12.html) record 8.4.12. Published [MySqlConnector](https://www.nuget.org/packages/MySqlConnector/2.6.2), [Dapper](https://www.nuget.org/packages/Dapper/2.1.89), and [TagLibSharp](https://www.nuget.org/packages/TagLibSharp/2.3.0) provide managed framework targets compatible with modern .NET; that is package metadata, not integration proof.

The proposed cloud host remains Linux ARM64 on eligible OCI A1 resources, subject to final-phase task 7.1 (previously 1.1). No actual image/package architecture, runtime deployment, MySQL binary, or native package compatibility was verified there. Oracle work is deferred until that phase; it does not gate installing/building the local Android shell. No SQLite substitution or host-selection claim is made.

## Executed checks

- `./scripts/check-prerequisites.ps1 -Scope Device`: no Java/adb/SDK discovered.
- `npm view @capacitor/android@8.5.2 version dist.integrity --cache .tools/npm --fetch-retries=0 --fetch-timeout=10000`: **failed EACCES** on the registry request. No elevation, fabricated package, or replacement version was used.
- Read public cached Capacitor 8.5.2 core/CLI package manifests without executing package code. CLI declares Node >=22; Android's published peer requirement accepts core ^8.5.0. This project has no installed Capacitor modules.
- `npm --prefix src/web ls --depth=0` and `npm --prefix src/web run build`: **passed**. The existing browser bundle-size warning remains (main bundle approximately 1.29 MB minified / 282 kB gzip).
- Backend restore used `dotnet restore tests/server/MusicServer.Tests/MusicServer.Tests.csproj --source 'C:/Users/antarikshyad/.nuget/packages' --verbosity minimal` with workspace `DOTNET_CLI_HOME=.tools/dotnet`, `NUGET_PACKAGES=.tools/nuget`, telemetry disabled, and automatic ASP.NET certificate generation disabled. It passed.
- Backend smoke build used `dotnet build tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore --verbosity minimal "-p:BaseOutputPath=$smokeOutput"`, where `$smokeOutput = Join-Path $PWD '.tools/toolchain-smoke/'`. **Passed, zero warnings/errors.** PowerShell argument forwarding produced `.tools/toolchain-smokeDebug/net10.0` as the actual ignored output directory. The separate output avoided locking an existing local server executable.
- `./scripts/check-android-toolchain.ps1`: reports missing JDK/compiler, SDK components, and all three installed Capacitor packages. It reads local version metadata only, rejects UNC/network locations, never executes adb/Gradle, and never certifies a build or phone result. `-JdkDirectory` / `-SdkDirectory` allow explicit local install locations without printing them.
- `node --test tests/scripts/android-toolchain.test.cjs`: **7 passed, zero failed/skipped** after correcting empty-directory handling. Synthetic metadata cannot establish runnable Java/tools or a compiled APK.

## Unblocking the native work

The owner superseded the local-compilation prerequisite on 2026-10-09 by choosing GitHub Actions. Use [remote bootstrap preparation](android-shell-bootstrap.md) and [operator steps](../../../../docs/android-shell-build.md) first. The steps below remain the optional local-build route, not a requirement to install Android Studio on this PC. Remote compilation still requires real build/install evidence and local adb/USB access for task 2.8; selected host/toolchain checks remain open.

1. Install Android Studio 2025.2.1 or newer from [Google](https://developer.android.com/studio). Configure Gradle to use JDK 21; if a newer IDE bundles another major, install/select JDK 21 separately. Record the exact IDE/JDK patch. Installation and SDK licence acceptance were not performed by this session.
2. In SDK Manager install Android SDK Platform 36, SDK Build Tools 35.0.0, and Platform Tools 37.0.1. Record exact revisions; if the manager offers a different revision, reconcile the pin before proceeding.
3. Run the inventory with local paths, for example `./scripts/check-android-toolchain.ps1 -JdkDirectory $env:JAVA_HOME -SdkDirectory $env:ANDROID_HOME`. A metadata match is only preparation, not task completion.
4. With registry access available, install all three Capacitor packages at 8.5.2 through npm and commit its genuine lockfile entries. Then add the checked-in Capacitor configuration and generate the official Android template in task 1.5; build/install the empty shell before exposing playback actions.
5. Keep the native shell independent of the browser-only diagnostic API/player. Bundled assets do not inherit Vite's `/api` proxy. Define phone/backend connectivity and native authority for the later experiment; do not expose the current loopback server or substitute WebView audio for Media3 background playback.

Complete task 1.4 only after the full specified restore/build and architecture evidence, and task 1.5 only after the installable shell is verified. Oracle signup is not required to install/build the local shell. Cloud/Google/phone acceptance remains governed by the active specs and tasks.

## Later local database inventory

During task 2.7, read-only executable version metadata identified MySQL Server/client **8.0.46.0**, and `Get-Service` showed **MySQL80 running**. Neither executable is on the current PATH. This corrects the initial PATH-only inventory; it does not establish the selected 8.4.12 toolchain. No database connection, credential read, service change, upgrade, or schema write was attempted. Before task 3.1, provide an isolated development database on the selected version and restore the pinned driver, then verify actual migrations/restart. Do not modify an existing database merely because its service is running.

## Review

Self-review checked the version sources against the exact Capacitor tag, kept installed dependencies distinct from selected future versions, and confirmed no dependency manifest/lockfile, application behavior, or task checkbox was changed. The first absent-directory test exposed PowerShell converting a null string argument to empty; the inventory now handles it and the regression passes. Tests also reject Java 17, missing compiler files, wrong platform/build/platform-tool metadata, and network paths. No Java/SDK/adb process or fake binary was executed. Native restore/build, host architecture, and physical playback remain unverified; the next dependent shell ticket cannot be completed in this environment.
