# Product repository restructuring

2026-10-09. Scope: approved folder/layer restructuring, task 3.0, [GitHub issue #3](https://github.com/Antariksh2k04/MusicServer/issues/3), branch `feature/fullstack/3-restructure-project`, based on merged main `9056dc7f2a30c342e8f892a808cc7ec6cfa9f1a8`.

The local tracked files matched the merged baseline before edits, allowing remote review to distinguish this restructuring from previous prototype work. Ionic and the existing Android project moved to `MusicServerFrontend/`. Backend source/tests moved into the five-project `MusicServerBackend/MusicServer.sln`; namespaces and existing HTTP contracts remain unchanged. The private storage contract and bounded streaming live in Application, stored-object metadata in Domain, the filesystem adapter in Infrastructure, and diagnostic HTTP composition in WebApi. No product entities, EF packages, database, Google integration, or cloud resources were added by this ticket.

Frontend screens now live in `src/app/`, playback controllers in `src/features/playback/`, shared track types in `src/types/`, temporary diagnostic transport in `src/lib/diagnostics/`, and helpers in `src/testing/`. Entry imports and test mocks were rebased. Android package/source sets and Gradle relative paths are preserved; the workflow and startup/inventory/bootstrap commands use the new locations. Review checkpoints now include product folders, docs and workflows while excluding generated native assets/build output. The regression test verifies retained source and detection of an edited backend file at its new path.

## Executed local checks

| Command | Result |
| --- | --- |
| `dotnet restore MusicServerBackend/MusicServer.Tests/MusicServer.Tests.csproj --ignore-failed-sources -p:NuGetAudit=false --verbosity minimal` | Passed using available packages; vulnerability service unavailable |
| `dotnet build MusicServerBackend/MusicServer.sln --no-restore --verbosity minimal` | Passed; zero warnings/errors |
| `dotnet test MusicServerBackend/MusicServer.Tests/MusicServer.Tests.csproj --no-restore --verbosity minimal` | 79 passed, zero skipped |
| `npm --prefix MusicServerFrontend test` | 26 passed |
| `npm --prefix MusicServerFrontend run build` | Passed |
| `npm --prefix MusicServerFrontend run build:native-shell` | Passed |
| `node --test tests/scripts/*.test.cjs` | 25 passed |
| `node --test tests/scripts/review-checkpoint.test.cjs` after extending the checkpoint regression | Passed |
| `openspec validate first-music-stream --strict` | Passed |
| `git -c core.safecrlf=false diff --check` | Passed |

Initial backend restore failed with NU1900 because the restricted environment could not fetch NuGet vulnerability data. Audit was disabled only for the documented local restore command, not in project configuration; no successful vulnerability audit is claimed. Both Vite builds retain the existing large-chunk advisory.

Review checked dependency direction, rebased imports/mocks, root-relative fixture fallback, solution/test discovery, tooling/CI paths, ignored outputs, and preserved Android source. Root project context remains intact with concise superseding architecture notes. Historical evidence retains original commands/paths. No native Gradle build ran locally, and no hosted run on the restructured commit is claimed; verify it using the updated manual workflow. Existing APK behavior is not new-build evidence.

## Database prerequisite observation

Read-only `mysqld.exe --version` reported MySQL Community Server `8.0.46` on Win64 x86_64; a `MySQL80` service is running. The owner selected using the available instance for local EF Core work. No database connection, credentials, database creation, restart, or EF restore was attempted here. The deployment target remains MySQL 8.4; local 8.0 verification must be labeled accordingly.
