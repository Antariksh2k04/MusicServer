# Task 2.7 — Local storage boundary

Recorded 2026-10-08. This report documents the Development provider and local failure checks for `first-music-stream`. It supports task 2.7 only; OCI integration, persistent MySQL import/recovery, Google identity, and Android acceptance remain pending. The active capability specs/design govern requirements; root planning documents were preserved.

## Implemented contracts

`src/server/MusicServer/Storage/IObjectStore.cs` defines audio-only, provider-independent operations without SDK types, HTTP credentials, or public URLs:

| Operation | Behavior |
| --- | --- |
| `PutAsync(key, source, length, sha256, cancellation)` | Caller supplies a generated nonempty GUID and independently computed SHA-256. New content is copied with at most 64 KiB buffers; exact positive length and checksum are checked before publication. The same key/length/checksum is an idempotent retry, including at capacity; conflicting content cannot overwrite it. A matching retry returns existing metadata without re-reading the input. |
| `HeadAsync(key, cancellation)` | Exact-key lookup returns immutable metadata or null. The local provider bounds JSON to 4 KiB and validates its key, size, hash syntax, and payload length. It reads stored checksum metadata, rather than hashing the whole audio on each HEAD. |
| `OpenRangeAsync(key, start, length, cancellation)` | Opens a disposable read limited to the requested window. Invalid bounds fail before reading; the stream cannot seek/write beyond the window. Premature EOF is an error. Callers pass cancellation during copying and dispose the read to release the upstream handle. |
| `DeleteAsync(key, cancellation)` | Idempotent exact-key removal, including incomplete writes. It deletes only known payload/metadata files; unknown contents prevent directory removal. Capacity is released only after removal succeeds. |

`StoredObject` carries the key, original byte length, uppercase SHA-256, fixed `audio/mpeg` type, and application ETag derived from digest/length. This is not an OCI-specific ETag. Public fixture IDs are separate from storage keys; keys and paths remain server-private.

## Filesystem implementation and admission

`LocalFileObjectStore` rejects non-Development environments, disabled configuration, relative/network directories, linked directories/files, and invalid limits. It checks existing ancestors before directory creation. A namespace ownership lock prevents simultaneous adapter writers; callers quiesce operations before disposal.

For new writes, byte/object reservations precede staging. Original bytes and metadata are flushed, then a same-filesystem directory rename publishes them together. Windows rename errors 5/32/33 allow at most three additional attempts, delayed 25/50/75 ms, only while the same source exists and destination does not. Cancellation stops this bounded local retry. The transfer is never replayed or an existing destination overwritten. Exhausted errors remain failures.

Incomplete writes are removed when possible; failed removal retains accounting. Reopening the same namespace inventories committed and incomplete payloads, preventing retained bytes from being treated as free. Partial delete failure may already have removed known files; remaining accounting is held until exact-key cleanup completes. Disposal does not bulk-delete the namespace.

Defaults: 50 MiB per payload, 250 MiB committed-plus-pending provider payload bytes, 100 object slots, and 10,000 calls per adapter instance. Failed provider calls consume operation admission; canceled calls rejected before admission do not. Limits are configurable via provider options; diagnostics exposes `Diagnostics__StorageByteLimit` and `Diagnostics__StorageOperationLimit`. The operation budget resets with a new instance and is not a persistent cloud request budget. Metadata/lock files are additional small disk usage.

The diagnostic import validates through one extra staging file, at most 50 MiB, before provider PUT. This additional file is outside provider payload accounting. The existing staging-byte admission still precedes duplicate lookup; the fixture-count check follows that lookup.

## HTTP integration

The fixture catalog now receives `IObjectStore` through DI. It publishes a track only after successful PUT; uncertain/failed writes trigger exact-key deletion, and failed cleanup ownership remains recorded in memory for a shutdown attempt. Storage admission maps to upload `409 quotaExceeded`; other storage I/O errors map to safe `503 storageUnavailable`.

Stream requests authorize and resolve ranges before provider access. HEAD reads metadata without opening the audio range. GET validates metadata/window before media headers; provider failures return safe errors beforehand, while a failure after response start aborts the body. Original full/range bytes, If-Range, invalid/multiple-range outcomes, and cookies/CSRF retain the existing diagnostic contract.

Diagnostic DI selects a new isolated namespace each run. Its sessions/catalog remain volatile and normal shutdown attempts only owned-key cleanup. Adapter restart tests reuse a namespace explicitly; they do not establish durable application imports. Crashes or exhausted cleanup admission can leave ignored orphan files. There is no durable cleanup worker yet. Production neither registers this provider nor exposes diagnostics; readiness remains 503.

## Validation

The backend suite covers original bytes and metadata across adapter reopen, idempotent/conflicting writes, checksum/length rejection, concurrent capacity, bounded copying/cancellation, incomplete accounting, targeted deletion/failure, operation/file/range limits, corrupt metadata/payload, namespace ownership, and Development/production guards.

Test-only `FaultingObjectStore` injects pre-PUT failure, lost successful PUT acknowledgement, metadata/range outage, wrong returned window, interrupted body, and delete failure. HTTP regressions verify invisible failed imports, safe errors, successful retry, metadata-only HEAD, no provider reads for invalid/unauthorized requests, and aborted truncated responses. Structural MP3 frames establish byte/protocol behavior, not audible decoding or seeking.

Executed from the root using existing restored packages, without network restore:

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.tools/dotnet'
$env:NUGET_PACKAGES = Join-Path $PWD '.tools/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$storageTestOutput = Join-Path $PWD '.tools/storage-tests/'
dotnet test tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore --verbosity minimal "-p:BaseOutputPath=$storageTestOutput"
```

The isolated ignored output avoids replacing an executable held by an existing development server. PowerShell forwards this output as `.tools/storage-testsDebug/net10.0`. No user server/service was stopped. Initial runs exposed a compiler/analyzer issue and intermittent Windows rename failures; corrections preceded final verification. The rename failures occurred after file disposal; the precise external cause is unverified.

Final backend result: **63 passed, zero failed/skipped**, with zero compiler/analyzer warnings. Frontend source and public DTO shape were unchanged; no new package was installed. This task cannot establish real cloud ranges/budget, database durability, or physical background playback.

## Review and next dependencies

The author reviewed publication/cancellation ownership, conservative capacity after failed cleanup, exact-key isolation, bounded reads, safe HTTP failures, private DTOs, and production exclusion. Review corrected link checks to occur before root/subdirectory creation and made read-side access-denied errors use the same safe storage response. This was a self-review, not an independent reviewer-agent run. No remaining defect was identified within task 2.7; the operational limitations above remain explicit.

Next native work is task 1.5/2.8 after the selected toolchain is installed and the empty Android shell builds. Java/compiler, SDK/adb, and installed Capacitor packages remain absent in this environment, so no native shell or background claim was made. A read-only inventory also found an existing running MySQL 8.0.46 service, rather than the selected 8.4.12; see [toolchain evidence](toolchain.md). No database/service/credentials were accessed or changed. Oracle remains final-phase work.

## Review correction — first-read failures

The subsequent workspace audit identified media headers retained when a provider failed on its first read, before the response started. The endpoint now clears Content-Length, Content-Type, Accept-Ranges, Content-Range, and ETag before producing the safe JSON problem, while preserving no-store and unrelated headers. Missing-object errors use the same cleanup with 404; I/O/access-denied errors use 503. Once a response starts, these failures abort it rather than attempting to replace its body or headers.

Six new HTTP regressions cover full and `bytes=0-9` requests with first-read I/O, missing-file, and access-denied failures. All six failed against the previous implementation; after correction, the full backend suite passed **69 tests, zero failed/skipped**, including the existing interrupted-body abort regression. The new checks verify absent media headers, a complete parseable Problem Details body, length matching when supplied, no-store, safe codes, and successful playback retrieval after clearing the injected fault. These use ASP.NET TestServer, not a live Kestrel transport run. Author review checked both pre-start and started-response branches; no frontend or requirement/task scope changed.

Verification used the workspace environment settings above and isolated output:

```powershell
$streamReviewOutput = Join-Path $PWD '.tools/stream-review-bin'
dotnet test tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore --verbosity minimal "-p:BaseOutputPath=$streamReviewOutput"
```
