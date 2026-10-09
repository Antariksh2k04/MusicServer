# Tasks

Follow the capability specs for behavior and `design.md` for contracts/architecture; add validation and operational evidence alongside each group. The owner requested implementation and, on 2026-10-08, deferred Oracle until the final phase. Section 0 is complete. Continue local tooling/native investigation, then durable application work against local MySQL and Development-only private storage. Oracle account access, provisioning, SDK integration, and public cloud validation belong only to the final groups below. Google configuration, MySQL, Android tooling, and the physical phone retain their independent prerequisites.

Local task numbers are retained. Moved cloud tasks are renumbered to match groups 7–8; the mapping below preserves historical evidence links and probe labels. Execute by group order and stated dependencies. Newly added local tasks are unchecked preparation, never replacements for retained cloud acceptance. An original task that still requires real resources stays unchecked until its complete acceptance is met. The final phase consists of groups 7–8. Review each completed ticket before continuing.

| Previous task IDs | Current task IDs |
| --- | --- |
| 1.1, 1.2 | 7.1, 7.2 |
| 2.1, 2.2 | 7.3, 7.4 |
| 4.4 | 7.5 |
| 5.3, 5.4, 5.5 | 7.6, 7.7, 7.8 |
| 7.1 | 7.9 |
| 2.3, 2.4, 2.5, 2.6 | 8.1, 8.2, 8.3, 8.4 |
| 6.5, 6.6 | 8.5, 8.6 |
| 7.2, 7.3, 7.4, 7.5 | 8.7, 8.8, 8.9, 8.10 |

## 0. Approved local development phase

- [x] 0.1 Scaffold pinned Ionic React web and ASP.NET Core .NET 10 projects plus relevant tests and ignores; verify frontend/backend restore and builds using documented workspace commands without requiring Oracle or an Android installation.
- [x] 0.2 Implement a bounded range-delivery component and Development-only loopback fixture server with explicit operator-configured diagnostic sessions, cookie/CSRF protection, local private file storage, one-file fixture upload/listing, and failure states; verify HTTP integration tests cover denied access, session expiry/logout, upload failures, byte equality, ranges/If-Range/HEAD, cancellation, and disabled production diagnostics.
- [x] 0.3 Connect the Ionic experiment to diagnostic sign-in, one-file fixture upload/listing, and a persistent browser audio controller with play/pause/seek and truthful error/reload handling; verify build and meaningful controller/UI tests, and record any real-browser CBR/VBR evidence separately from simulated media events.
- [x] 0.4 Document local run/configuration and redacted validation in `evidence/local-development.md`, update AGENTS with actual paths/commands, and verify the reproducible local checks while explicitly retaining cloud, real Google, MySQL, and physical Android gates as unchecked.

## 1. Local toolchain and device prerequisites

- [ ] 1.3 Record the physical CMF Phone 2 Pro stable OS/build, battery settings, accessory/call-test access, and stable Chrome/Edge versions; verify the device/browser matrix is present in `evidence/environment.md` without claiming emulator evidence covers the phone.
- [ ] 1.4 Pin a compatible Ionic/Capacitor/Node/Android/Media3 and .NET/MySQL/driver/metadata toolchain; verify documented versions support the selected host architecture and a minimal restore/build smoke check, and record the exact commands actually used. Android compilation may use the owner-selected manual GitHub Actions runner; local Android Studio/JDK/SDK installation is optional for that build. Record runner versions separately from local adb/phone and production-host evidence; verify account-level no-charge settings before dispatch.
- [ ] 1.5 Create the minimal future `src/web/`, `src/server/`, and test project structure with only this change's dependencies; verify frontend/backend builds and an installable empty Android shell using configured scripts, then update AGENTS command examples to real project paths without adding other MVP features. Bootstrap with the pinned official Capacitor template on GitHub Actions when local registry access is blocked; review and incorporate the returned native sources and genuine package manifest/lockfile before task 2.8. Require actual APK build and physical installation/launch evidence; workflow preparation alone does not complete this task.

## 2. Early local storage and native background experiment

- [x] 2.7 Introduce the provider-independent storage boundary and Development-only private filesystem adapter plus failure-injection doubles; verify original-byte PUT, exact-key metadata/checksum lookup, bounded range reads, cancellation, targeted deletion, configured local limits, and rejection of the adapter by production configuration; document contracts and local-only evidence beside this change.
- [ ] 2.8 Build the narrow local Media3 service/Capacitor bridge and protected diagnostic native access/refresh broker after the Android shell is installable; verify authenticated seek, forced expiry/renewal without Ionic callbacks, logout/revocation, and single-player reattachment over an explicitly debug-only USB-to-loopback path without exposing diagnostics on LAN/public interfaces; record exact commands and local transport limits.
- [ ] 2.9 Run the local CBR/indexed/unindexed VBR and >=15-minute CMF background experiment with the service from 2.8; verify actual seeks, lock/app-switch, system/accessory controls, call/focus, route disconnect, and paused process-relaunch results in `evidence/local-native-playback.md`, revoke local sessions, and clean only owned fixtures; record public HTTPS/cloud and Wi-Fi/mobile-data recovery as pending and block dependent playback work on local failures.

## 3. Durable schema and app authentication

- [ ] 3.1 Add checksum-tracked ordered MySQL migrations for the slice tables in design section 3, with FK ordering and constraints; verify fresh/repeated migration, interrupted-migration recovery, singleton owner, unique hashes/submission IDs, and durable database restart through integration checks.
- [ ] 3.2 Add the operator-only verified Google issuer/subject provisioning procedure and package/signing/origin configuration; verify missing owner fails readiness and that no public first-owner registration route exists; document the procedure without personal identifiers or secrets in the repository.
- [ ] 3.3 Implement bounded nonce bootstrap and Google proof exchange for browser/native clients; verify real configured-owner sign-in plus non-owner, wrong audience/issuer, expired proof, nonce replay, cancelled sign-in, and provider/network failure cases from `owner-auth`.
- [ ] 3.4 Implement durable opaque browser sessions, secure cookie/CSRF/Origin handling, authenticated CSRF reacquisition, rate limiting, and minimal auth/health responses; verify cookie flags, rejected CSRF mutations, restart persistence, and exact `401`/`403` outcomes without redirects or leaked config.
- [ ] 3.5 Implement native access/refresh rotation and session-family revocation in MySQL; verify concurrent refresh, consumed-token reuse, expiry, logout, and lost successful refresh response behavior through integration tests with no plaintext token persistence.
- [ ] 3.6 Connect Ionic web Google Identity Services and native Credential Manager through one protected native auth broker; verify both clients persist valid login across restart, serialize native renewal, use protected storage, and clear local playback/session state on online/offline logout.
- [ ] 3.7 Document the finalized auth API/configuration and redacted owner/non-owner evidence alongside this change; verify it matches design contracts and that diagnostic credential issuance is absent from the production build.

## 4. Single-file import and durable recovery

- [ ] 4.1 Implement the one-item manifest and raw content contracts, durable bounded staging, incremental hash, byte accounting, and idempotency; verify zero/multiple file rejection, 50 MiB boundary, length mismatch/disconnect, busy receiver, lost acknowledgements, and changed manifest identity with integration tests.
- [ ] 4.2 Implement independent bounded MP3 validation and TagLibSharp text/duration extraction without rewriting bytes; verify tagged/tagless/Unicode/markup-like/corrupt-optional-tag, renamed non-MP3, CBR, and VBR fixtures against `mp3-import` and `track-library` scenarios.
- [ ] 4.3 Implement durable import leases/claims and quota reservation transactions with deterministic lock order; verify duplicate races, track/storage/staging admission, and no cloud write before reservation, including waiter behavior after claimant failure.
- [ ] 4.9 Connect the shared import worker and atomic visible-track/result commit to local MySQL and the private Development storage adapter after 3.1 and 4.1–4.3; verify real local database durability across restart, original-byte equality, duplicate races, and durable failed-commit cleanup intent without phantom tracks; record provider identity so local results cannot be mistaken for OCI acceptance. Task 4.5 adds reconciliation and cleanup execution afterward.
- [ ] 4.5 Implement uncertain-PUT reconciliation, durable cleanup/backoff, restart recovery, and attempt-aware retry; verify lost PUT acknowledgement, worker/API restart, failed cleanup, expired leases, and retry do not release uncertain usage or lose orphan-object intent.
- [ ] 4.6 Implement the browser single-file upload/progress/status/retry UI using cookie/CSRF; verify processing is distinct from transfer completion and all failed/duplicate/imported outcomes map to truthful UI state.
- [ ] 4.7 Implement Android picker content-URI inspection and raw native transfer, including bounded spooling for unknown length; verify real phone files upload without base64/full-file JS copies, cancelled selection changes nothing, and foreground upload does not stop active playback.
- [ ] 4.8 Record final import state/API behavior and recovery/operator instructions beside this change; verify cross-client valid/invalid/duplicate import evidence and that operational instructions describe targeted cleanup without duplicating normative requirements in root documents.

## 5. Local library and protected media delivery

- [ ] 5.1 Implement parameterized bounded list/detail queries with full-field stable title/artist/id ordering and private DTOs; verify empty/populated/paged results, Unicode rendering, hidden incomplete imports, no private keys/paths, and persistence after API/MySQL restart.
- [ ] 5.2 Implement shared Ionic library/loading/error/refresh/selection surfaces with only slice actions and placeholders; verify each client's imports appear on the other, network error differs from empty state, and metadata renders as safe text.
- [ ] 5.6 Connect committed local MySQL track lookup, per-request owner authority, and bounded range delivery through the storage boundary after 4.9; verify exact full/HEAD/range/If-Range bytes/statuses, denied access, missing storage, injected outage, cancellation/accounting, and executable redacted local probes; retain provider-sensitive checks for final tasks 7.6–7.8.

## 6. Shared web and native playback modules

- [ ] 6.1 Integrate one persistent browser audio controller with selection/play/pause/seek snapshots and route subscriptions; verify actual media events, clamped seeks, rapid commands, route continuity, rejected play promises, and no full-file Blob or credential-bearing URLs.
- [ ] 6.2 Implement browser credential-free selected-track checkpoints, paused restoration, auth probing, and bounded media retries; verify reload/expiry/sign-in remain paused and pause/logout/new selection cancels stale recovery.
- [ ] 6.3 Integrate the Media3 service with production native auth/data-source renewal, required foreground-service configuration, metadata, focus, and route handling; verify new requests after access expiry renew without Ionic and explicit pause survives interruption.
- [ ] 6.4 Connect Ionic to native revisioned selected-track commands/snapshots and credential-free checkpoints; verify reattachment uses the running player, process relaunch restores paused, stale seeks cannot replace newer intent, and natural completion stops without a production queue/repeat loop.

Exercise these modules through the local authenticated path and record local results immediately. Real Google checks require configured development clients and appropriate local TLS; diagnostic sign-in never completes them. Production authority and provider-sensitive playback still require final deployed checks.

## 7. Final phase — Oracle readiness, integration, and deployment

Begin only after local modules and the early native experiment are reviewed. Preserve the retained task criteria; no Oracle provisioning happens before 7.1 passes. Production public-origin acceptance is here, while development Google client configuration may be needed earlier.

- [ ] 7.1 Check actual Oracle account/home-region compute, durable MySQL disk, private Standard bucket, request/traffic/storage entitlement, and enforceable ₹0 usage; verify a redacted `evidence/environment.md` records eligible resources, hard limits, headroom, and a pass/fail decision before provisioning.
- [ ] 7.2 Validate a no-purchase public HTTPS hostname, certificate trust/renewal, and Google authorized web origin; verify a trusted HTTPS request and Google configuration check, recording a blocker if the hostname cannot meet the requirements.
- [ ] 7.3 Configure eligible scratch resources with private bucket/IAM and tracked CBR, indexed VBR, unindexed VBR, and >15-minute fixtures within file/quota limits; verify anonymous object access is denied and fixture size/hash/keys are recorded securely for targeted cleanup.
- [ ] 7.4 Build the smallest authenticated diagnostic range proxy using the production-intended OCI adapter and isolated operator-issued sessions; verify full/HEAD, closed/open/suffix, invalid, multi-range, If-Range, byte equality, cancellation, and bounded-buffer probes against real cloud storage.
- [ ] 7.5 Implement restricted OCI PUT/checksum/metadata operations and atomic visible-track/result commit; verify original-byte equality, private object access, successful import, duplicate result, and injected database failure after PUT without a playable phantom record.
- [ ] 7.6 Integrate the proven range proxy with committed track lookup and per-request owner authorization; verify full/HEAD/single-range/416/multi-range/If-Range semantics, precise bytes/headers, denied anonymous/expired credentials, and missing object/cloud outage behavior.
- [ ] 7.7 Add persistent cloud-operation/outbound admission, cancellation accounting, upstream range checks, and reverse-proxy media settings; verify no uncounted SDK retries, compression, whole-object buffering, or mismatched `206`, and that cancellation closes the upstream stream.
- [ ] 7.8 Record finalized list/media contracts and redacted request evidence alongside this change; verify the documented probes reproduce range statuses and that list/media endpoints expose no search, artwork, favourites, playlist, or queue capabilities.
- [ ] 7.9 Deploy the slice on the validated no-charge topology with private MySQL, least-privilege IAM, durable staging/keys, same-origin web/API, trusted proxy headers, and preserved APK signing; verify private service exposure, readiness, actual runtime builds, and public HTTPS/mobile-data access, recording operator release/rollback commands.

## 8. Final phase — Cloud playback and release acceptance

- [ ] 8.1 Exercise a minimal Ionic web audio controller through the trusted same-origin cookie path; verify CBR/indexed VBR seeks meet `audio-playback` scenarios, inspect actual `206` traffic, and record unindexed VBR, play-policy, route/reload, and expired-cookie outcomes.
- [ ] 8.2 Integrate the locally proven app-owned Media3 service/Capacitor bridge and native diagnostic renewal broker with real cloud delivery; verify authenticated seeking and a forced new request after short access expiry while Ionic callbacks are inactive, recording real native renewal evidence through the trusted deployed transport.
- [ ] 8.3 Run the single-track physical-device lock/app-switch check for >=15 minutes plus notification/lock-screen/accessory controls, route disconnection, focus/call, network change, and reattachment checks; verify per-scenario pass/fail evidence includes installed builds, timestamps, and seek positions.
- [ ] 8.4 Publish the experiment verdict and measured effort in `evidence/streaming-gate.md`, revoke diagnostic sessions, and remove only tracked scratch objects; verify cleanup/accounting and that gate failures block dependent cloud/release work. Do not claim the deferred multi-track release gate passed.
- [ ] 8.5 Run targeted browser and physical CMF playback regressions using imported production-path CBR/indexed/unindexed VBR tracks; verify scenario evidence includes forward/backward seek, >=15-minute normal background listening, system/accessory controls, focus/disconnect, renewal, and bounded network recovery.
- [ ] 8.6 Document native/browser lifecycle findings, selected-track limitations, and unindexed seek defects beside this change; verify no force-stop/browser-background or multi-track full-release guarantee is inferred from the slice's results.

- [ ] 8.7 Exercise paired MySQL/object backup and isolated restore while import/cleanup commits are paused; verify restored metadata/audio checksums and owner access, and document compatible binary rollback versus schema restore without deleting live audio.
- [ ] 8.8 Run the complete owner sign-in → one-file upload → metadata list → play/pause/seek journey from web and Android, including cross-client playback; verify all four capability scenario reports are complete and no diagnostic auth or temporary fixtures remain.
- [ ] 8.9 Run integrated non-owner/anonymous/revoked-session denial and failed-upload/cloud/database/restart recovery checks against deployment; verify private data stays inaccessible and failures preserve existing tracks and quota consistency.
- [ ] 8.10 Review acceptance evidence, unresolved gates/defects, measured budget usage, and remaining whole-MVP work; verify review clearly distinguishes completed slice behavior from deferred capabilities and that every checked task has supporting evidence.

## Workflow follow-up

- After implementation and acceptance review, explicitly run the archive workflow to sync these deltas into `openspec/specs/<capability>/spec.md` and archive the change; do not archive an unimplemented proposal.
- Verify the synced capability baseline, archived artifacts, and AGENTS governance links agree; preserve root documents as project-level context and plan remaining MVP capabilities as separate changes.
