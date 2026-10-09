# Approved local development evidence

Recorded 2026-10-08. The owner approved section 0 before Oracle signup. This report covers only that temporary local phase; the original production tasks remain unchecked. Root FRS/SDS/spec/implementation-plan documents were preserved.

Later task 2.7 replaces direct fixture file access with the storage boundary. See [local storage evidence](local-storage.md) for current provider/staging accounting and failure checks; the results below retain the historical section-0 snapshot.

## Implemented boundary

- Pinned Ionic React frontend and ASP.NET Core .NET 10 backend, with Vitest/xUnit projects and repository ignores.
- Explicitly enabled Development-only diagnostics, IPv4 loopback listener, operator-configured key, bounded/revocable cookie sessions, exact Origin plus antiforgery validation for mutations. Session/key state stays in memory; no anonymous session issuance or production authentication bypass.
- One raw MP3 fixture per submission, complete-length and structural frame validation, SHA-256 deduplication, private randomly named local files, filename/unknown-tag fallbacks, and list/detail endpoints. Only completed fixtures are visible. Local catalog is volatile; normal shutdown attempts owned-file cleanup. This is not production embedded-tag extraction, MySQL durability, or private cloud storage.
- Protected original-byte streams with a fixed 64 KiB copy buffer; full/single/open/suffix ranges, ETag/If-Range, HEAD, defined invalid/multiple-range responses, cancellation, and no-store responses.
- Browser-owned singleton audio controller, play/pause/seek, truthful loading/buffering/failure states, paused checkpoint restoration, expiry checks, and reload. Diagnostic key stays out of browser storage; only versioned fixture ID/position is checkpointed. Switching Library/Upload views preserves the controller.

Local limits: 50 MiB per file, 100 fixtures, 250 MiB combined, 1,800-second session default and 3,600 maximum. Diagnostic HTTP cookie names/configuration are distinct from production HTTPS identity requirements. Production readiness deliberately remains 503.

## Actual toolchain

| Tool | Observed version |
| --- | --- |
| .NET SDK / target | 10.0.401 / net10.0 |
| Node / npm | 24.18.0 / 11.16.0 |
| Ionic React / React | 9.0.5 / 19.2.8 |
| TypeScript / Vite | 5.9.3 / 8.3.0 |
| Vitest / jsdom | 4.1.10 / 29.1.1 |
| xUnit / ASP.NET testing package | 2.9.3 / 10.0.5 |
| Installed Chrome (attempt only) | 153.0.8010.48 |

These local builds do not establish compatibility with a future Oracle host architecture, Android/Media3 toolchain, or MySQL driver. The original tasks 1.1–1.5 remain separate checks; the later owner-approved reorder moved Oracle/public-origin tasks to 7.1–7.2. Follow the current [task mapping](../tasks.md) rather than these historical numbers.

## Reproducible commands

Portable restore/build/test/start instructions are in root `README.md`. This restricted workspace had no usable npm network access. Public dependencies already cached on this machine were copied into ignored writable caches; no account or credentials were used. NuGet restored from the existing read-only machine package cache into the workspace.

Commands actually used from the root (frontend commands execute in `src/web`):

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.tools/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet restore tests/server/MusicServer.Tests/MusicServer.Tests.csproj --source 'C:/Users/antarikshyad/.nuget/packages' --verbosity minimal
dotnet build tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore --verbosity minimal
```

```powershell
# In src/web; uses the prepared ignored cache.
npm install --offline --cache ../../.tools/npm --logs-dir ../../.tools/npm/_logs --no-audit --no-fund
npm install --package-lock-only --offline --cache ../../.tools/npm --logs-dir ../../.tools/npm/_logs --no-audit --no-fund
npm ci --offline --cache ../../.tools/npm --logs-dir ../../.tools/npm/_logs --no-audit --no-fund
npm run build
npm run test
npm run dev
```

The cache paths are machine-specific evidence, not a dependency on this user's profile for normal repository builds. The startup script was exercised with a process-only test key; it restores prior environment configuration. Real keys/tokens/media are absent from this report.

## Validation results

| Check | Result / interpretation |
| --- | --- |
| Backend restore/build | Passed, zero warnings/errors. |
| xUnit HTTP/copy suite | 29 passed, zero failed/skipped after runtime key-storage correction. Covers disabled production diagnostics, anonymous/nonloopback denial, wrong key, CSRF, import/deduplication, failed uploads/no residual media, quota, expiry/logout, exact ranges, HEAD/If-Range, missing fixtures, short upstream, and bounded cancellation. |
| Frontend install/build | Initial offline install and typechecked Vite build passed. Clean `npm ci` also passed after cached platform metadata repaired the lockfile and Vite was stopped to release its Windows native binary lock. The subsequent final typechecked Vite build passed. |
| Vitest controller/UI suite | 11 passed in two files, zero failed. Media events are simulated; Ionic wrappers in UI tests are replaced with semantic containers. Covers play races, buffering/error, clamped seeking, paused restoration, malformed checkpoints, logout, denied sign-in, view persistence, upload failure, expiry, and reload. |
| Live backend + Vite proxy | Passed via HTTP client: bootstrap/sign-in 200, duplicate upload 200 (the same structural bytes had already been saved), range 206 with `bytes 0-9/1251` and exact byte equality, logout 204, later stream 401. Liveness/bootstrap also returned 200. |
| Automated Chrome smoke | Not passed: browser target crashed in this environment. No browser interaction, audible playback, codec, or real CBR/VBR seek result is claimed. |
| Build size | Vite warns about the ~1.29 MB minified main Ionic bundle (~282 kB gzip). Code splitting/performance remains a future integration concern. |

The byte fixture is three synthetic structural frames, **not an audible decoding fixture**. There is no supplied real CBR/VBR MP3 or completed manual audible seek test. Follow the manual checklist in `README.md` and record actual results before promoting playback claims.

The Windows EventLog provider initially hid failures under this restricted identity; the loopback application now logs to console. A subsequent real startup exposed profile key-ring warmup; diagnostic key repository/provider now remain in memory, and the server bootstrap/proxy tests and backend suite passed afterward. No production key-storage choice is established by this local workaround.

## Remaining release gates

No Oracle account/resources/budget enforcement or public trusted HTTPS is verified. No Google owner flow, persistent native identity broker, MySQL schema/durability, cloud uploads/recovery, Android shell/Media3 background service, phone/lock-screen/accessory/call test, or cross-client release acceptance is implemented. Real CBR/VBR browser seeking also remains unverified. Do not count section 0 as completing those original tasks or archive the change.

## Final reproducibility check

Clean offline `npm ci` completed successfully: 138 packages installed. Initial missing optional platform/fsevents lock entries were repaired from real cached registry metadata using `npm install --package-lock-only`; no dependency version or integrity was invented. The first clean attempt after repair encountered a native Windows binary held open by Vite. Stopping the owned development servers and rerunning the same command resolved that file lock.

After the successful clean install, `npm run build` passed (TypeScript plus Vite; existing bundle-size warning) and `npm run test` passed again: 11 tests, two files, zero failed. The final backend suite after the key-storage correction passed all 29 tests with zero failed/skipped. OpenSpec strict validation returned valid with no issues. AGENTS now documents the actual structure and commands in 400 words; README includes startup configuration, limits, and the outstanding manual playback checklist. All smoke development servers started for this session have been stopped.

## Review corrections — 2026-10-08

All four reported local-phase findings are resolved:

- Reload/restore lookups use an intent revision. Selection, sign-in, expiry, logout, and unmount invalidate earlier lookups; obsolete success/error callbacks cannot change selection or restore a cleared checkpoint. Regressions cover A→B, A→B→A, stale failure, and delayed restore after confirmed logout.
- Repeated Play while loading/playing/buffering is idempotent; no new `playing` event is required to retain status. Pause/resume remains available.
- Initial session validation and library failures are handled separately. Valid authority retains Refresh library on network/503 failure, an unsuccessful list is not displayed as empty, and a confirmed upload remains successful if its follow-up list fails. Actual 401 still requires sign-in.
- The fixture-count limit is checked after duplicate digest lookup, with staging-byte admission still checked first. The HTTP regression fills 100 fixtures, recovers the last fixture's duplicate outcome, rejects new content, verifies unchanged original bytes, and checks that failed/duplicate staging leaves no extra files.

Validation after corrections: `npm run test` **20 passed**, `npm run build` **passed** with the existing Ionic bundle warning, and backend suite **30 passed**, zero failed/skipped. A running application held the normal Windows apphost executable open; tests used isolated ignored output instead of stopping that server:

```powershell
# Same workspace DOTNET_CLI_HOME/NUGET_PACKAGES settings as above.
$reviewOutput = Join-Path $PWD '.tools/review-bin/'
dotnet test tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore --verbosity minimal "-p:BaseOutputPath=$reviewOutput"
```

The first new pending-play test had an unresolved test promise, corrected before the passing rerun. The author reviewed the changed request ownership, authentication/list states, repeated-command behavior, and quota ordering after the checks; no additional defect was identified within these four fixes. This was a self-review, not a separate reviewer-agent run. Previous audible/cloud/native limitations still apply. Section 0 remains complete; no production task was marked complete by these corrections.
