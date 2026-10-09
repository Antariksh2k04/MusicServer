# Implementation plan — personal music server MVP

Status: planning only; no application, experiment, cloud resources, or dependencies have been built/provisioned.
Date: 2026-10-08. Implementation window: five days, beginning when implementation is separately authorized.

## 1. Documentation baseline and precedence

The requested design documents were created before this plan:

1. [FRS.md](FRS.md) — functional/nonfunctional requirements, acceptance and exclusions.
2. [SDS-FE.md](SDS-FE.md) — Ionic React screens/state, browser/native playback adapters, authentication/picker boundaries.
3. [SDS-BE.md](SDS-BE.md) — architecture, MySQL schema, canonical HTTP contracts, upload/streaming/authentication/deployment design.

[spec.md](spec.md) was updated for the owner's database change to **MySQL**. Confirmed owner requirements govern the designs; marked technical defaults remain proposals. Do not maintain duplicate endpoint/schema definitions here: SDS-BE is the canonical server design and SDS-FE is the canonical native bridge design.

Confirmed scope: one Google owner, web and Android uploads and original MP3 streaming, embedded metadata/artwork, search, queue/shuffle/repeat, favourites, ordered playlists, deletion, and required Android background playback. Web has the shared listening/organization flows and browser-local paused restoration. Public internet/mobile data; private APK on CMF Phone 2 Pro with latest available stable OS; Oracle Always Free candidate; ₹0 service budget. Limits: 5,000 tracks, 50 MiB/file, 100 files/batch; free storage bytes can bind earlier.

This task creates documentation only. Every code/provisioning/test action below is future work; no full application or experiment is authorized to be built in this documentation turn.

## 2. Architecture to implement after the gates

| Layer | Selected design |
| --- | --- |
| Web | Compiled Ionic React sign-in/upload/library/player/queue/favourites/playlists served same-origin with API; one browser audio adapter with cookie-authenticated streaming. |
| Android UI | Ionic React in Capacitor; library/search/player/queue/favourites/playlists/upload screens. |
| Android native | Media3 service owns playback/queue/checkpoints; auth broker owns refresh; system picker streams selected URIs. No dependency on JS for transitions/renewal. |
| Backend | ASP.NET Core .NET 10 modular monolith, parameterized MySqlConnector/Dapper repositories, bounded import/cleanup workers. |
| Database | MySQL 8.4 InnoDB, private service, persistent storage, explicit schema migrations. |
| Objects | Private OCI Standard bucket for unchanged original audio and bounded JPEG cover derivative. |
| Delivery | Owner-authenticated backend proxy with single byte-range support; browser uses same-origin session cookie, native data source supplies renewable bearer headers. No public/PAR audio URL in initial design. |
| Deployment | One eligible OCI VM for API/private MySQL behind Caddy HTTPS; no paid infrastructure dependencies. |

The recommended proxy gives stable URLs and central access/quota admission, but adds an extra stream hop and server bandwidth use. It must pass real cloud/native tests before being treated as proven. A direct-storage alternative would need a separate bounded URL-grant/renewal/revocation design; do not silently switch to permanent public objects.

MySQL stores owner/session/refresh state, tracks/object intents, favourites, playlists/memberships, upload batches/items/import claims, quota counters, and migration history. Browser/device queue snapshots never become a server queue. InnoDB short transactions protect deduplication, quota reservations, playlist revisions, and logical deletion; cloud calls remain outside database transactions with recorded compensation. See SDS-BE sections 2–3.

## 3. Implementation flows and contracts

### Authentication

Configure Google's browser and Android package/signing identity, and provision exactly one verified issuer/subject. Exchange nonce-bound Google ID tokens for app sessions. Desktop uses a secure HttpOnly cookie plus CSRF; Android uses a proposed ten-minute opaque bearer and 30-day rotating app refresh session, protected natively. Serialize native renewals and require a new sign-in when renewal is invalid. No local password/signup or Google access-token authority on music APIs.

### Upload

Both clients create a bounded manifest and send raw per-item bytes; Android avoids base64/JS copies. Stage locally with limits and SHA-256, inspect audio/tags/cover, deduplicate using a claim and unique final hash, reserve cloud bytes/track slots, PUT random private object keys, then commit a visible track. Poll item outcomes after `202` receipt. Failed/uncertain cloud writes remain accounted for until reconciled/deleted; restarting the API cannot lose job/cleanup intent. See SDS-BE sections 4–5.

### Streaming

The browser audio element requests `/api/v1/tracks/{id}/stream` with the existing same-origin owner cookie; Media3 uses owner bearer and Range. Authorize/resolve/admit budget, forward the resolved range to OCI, and return the correct `200`/`206`/`416` contract with fixed-size buffers. Do not compress, embed secrets in URLs, or fetch the whole audio Blob before play/seek. Web handles rejected play promises and expired cookies with paused/press-play/sign-in state. The native broker renews expired credentials once; native queue advancement persists with the UI inactive. Both implement the same queue semantics through distinct adapters. See SDS-BE section 6 and SDS-FE sections 6–7.

### Organization and deletion

Favourite PUT/DELETE is idempotent. Playlist mutations use If-Match revisions, exact-set reordering, and unique membership. A queue-context response snapshots a consistent order without creating server playback state. Logical deletion removes track relationships transactionally, records cloud cleanup, and stops a known deleted current entry in the native queue. Physical object deletion can finish later. See SDS-BE section 4.

## 4. Dependencies and prerequisites

| Dependency | Why needed / resolution |
| --- | --- |
| Eligible Oracle account/home-region capacity | Provision a no-charge VM, durable storage, private bucket and least-privilege identity; card verification accepted. Do not depend on temporary trial credits or historic entitlement numbers. |
| Public hostname/TLS accepted by Google | Stable API/browser origin, certificate renewal and Google web-client configuration. A suitable no-purchase hostname is unverified; do not assume a raw IP solves browser Google login. |
| Actual CMF phone/build/headset | Record OS/build/security patch/normal battery settings; Bluetooth/call/network tests require the physical device. |
| .NET 10 runtime/toolchain + MySQL 8.4 | Confirm Linux ARM64/package compatibility and memory on the eligible VM. Use supported versions and pin patches; never substitute another database silently. |
| MySqlConnector + Dapper | Async parameterized access and explicit transactional repositories; schema migrations/backup need their own controlled procedure. |
| OCI .NET SDK | Private object read/write/delete and range streaming through an instance principal. SDK retry attempts must fit accounting. |
| Ionic React/router + Capacitor toolchain | Pin compatible versions together; candidate Capacitor 8 requires its supported Node/Android Studio/SDK setup. |
| Browser HTML media and target browsers | Proposed first targets: stable desktop Chrome/Edge. Verify cookie ranges, VBR seeks, play policy, route continuity and local checkpoints; background/suspended-tab playback is best effort. |
| Media3 and native bridge | Must support headers, independent refresh/queue, system controls, and native checkpoints. Evaluate an existing plugin briefly; custom bridge effort is the main schedule risk. |
| Credential Manager/Google ID + verification library | Interactive native Google identity and backend claim verification. Signing certificates/audience/nonce must align on both clients. |
| TagLibSharp + SkiaSharp candidates | Metadata and bounded artwork processing; verify license/runtime/native ARM64 requirements and malformed-tag behavior. |
| APK signing key and restricted secret storage | Private install/update continuity, protected server/native credentials. No signing/Google/cloud secrets in committed files. |

Dependencies are design choices, not installed packages. Package versions are frozen only after the early compatibility gate. Avoid adding Redis, a job broker, transcoding binaries, paid plugins, Kubernetes, or a second state authority for this MVP.

## 5. Small cloud/web/Android experiment — proposal only

### Objective and scope

Verify the risky path before building library/playlist screens: **private cloud object → authenticated range proxy → browser audio element and native player on the CMF Phone 2 Pro**. Verify web cookies/seeks/play-policy/session expiry, plus Android seeking, transitions and renewal while JavaScript is inactive.

Time-box to approximately six engineering hours after credentials/resources are available. Allow a separate short provisioning check at the start of Day 1; cloud capacity delays can block the gate and must not be disguised as experiment completion.

The future experiment contains only:

- Three owner-provided MP3 fixtures: CBR, indexed VBR (Xing/VBRI where available), and a representative unindexed VBR; optional small JPEG cover. Keep total scratch objects below approximately 40 MiB and explicitly tracked.
- A minimal ASP.NET Core range endpoint with a fixed fixture catalog and the production-intended OCI adapter. No upload library, playlists, search, database migration or production UI.
- An isolated experimental session/renewal store and operator-issued diagnostic native credential/browser cookie, not an anonymous public token/cookie-issuance endpoint. This permits testing both delivery schemes without first implementing all Google/MySQL functionality. Delete/disable it before production; it is not an owner-provisioning backdoor.
- One minimal web screen with a single audio element, user-initiated play/pause/seek, and a small queue; serve it from the API origin. No complete library or playlist editor.
- One Capacitor screen with start/play/pause/seek and current state, a three-item native queue, media session/notification, and native renewal. Reuse a viable bridge or write the smallest Media3 service needed to test the required boundary.

A separate narrow Google login check should verify that the selected signing/package/audience and browser origin can obtain/validate the configured owner's ID token. If that is not completed during the experiment, record Google integration as still open; an experimental bearer does not prove production authentication.

### Procedure and evidence

| Step | Action | Required observation |
| --- | --- | --- |
| E1 | Confirm account/resource eligibility, private bucket, actual limits, region, SDK identity and public trusted HTTPS endpoint. Upload only tracked fixtures. | Anonymous object/API access denied; no paid/trial-only resource dependence; environment recorded. |
| E2 | Probe HEAD, full GET, `bytes=0-1023`, an interior range, open-ended/suffix range, invalid/out-of-bounds range and If-Range. Compare selected bytes with local original. | Correct statuses/headers/lengths/bytes; no whole-object buffering for a small range; cancellation closes upstream; errors do not reveal credentials. |
| E3 | Stream all fixtures natively; seek to ~25%, ~75%, then backward. Observe real network requests and target position. | Actual `206` range traffic and audible/position progress without complete-file-first fetch. Proposed indexed-file seek tolerance ±2 seconds; unindexed findings explicitly recorded. |
| E3-W | Stream/seek the same fixtures in recorded desktop browsers using the HttpOnly owner cookie. Exercise a small queue, route changes, reload, blocked play and expired/revoked cookie. | Valid cookie grants media access; no cookie is denied without HTML redirect; real ranges and seek behavior work; one player survives route changes; reload is paused; blocked play and expiry have usable handling. |
| E4 | Start the three-entry queue with repeat-all, lock screen/use another app for ≥15 minutes and ≥2 automatic transitions. Detach UI command callbacks or otherwise show that queue advancement does not require JS. | Native transition logs and updated media metadata; no timer/React end-of-track dependency; no extra player on return. |
| E5 | Use notification/lock-screen previous/next/play/pause and Bluetooth controls; disconnect output and interrupt with a call/competing audio. | Coherent state, required controls, disconnection pause, focus handling, and no restart after an explicit pause. |
| E6 | Use a short experimental access TTL (e.g. 60 seconds, renewable session ≥30 minutes); force a later range request/next transition after expiry with UI inactive. | Native refresh and reopened stream succeed; revoked/expired refresh stops further requests and reports sign-in requirement. No JS renewal. |
| E7 | Interrupt network, switch Wi-Fi/mobile data, return UI, dismiss task, then test process stop/force stop separately. | Bounded recovery respects pause; one authoritative player; ordinary background cases pass; termination limitations recorded separately. |

Record device/OS/build, chosen dependency versions, fixture sizes/hash/encoding, cloud-operation/traffic counts, status/Content-Range samples, requested/observed seek times, timestamped native transition/renewal events, focus/battery settings, and pass/fail per scenario. Logs must exclude tokens, account secrets and credential-bearing URLs.

### Pass/fail and cleanup

Proceed to the full application only when protected ranges and representative seeks pass in browser and native engines, web cookie/play-policy/expiry handling works, required Android background transitions/system controls pass on the real phone, and native renewal is demonstrated. Test Google configuration, picker source streaming, and MySQL durable connectivity as adjacent narrow Day 1 checks before depending on them.

If an existing plugin advances tracks or renews only through JavaScript, reject that integration and evaluate the native service within the time box. If the native approach or zero-budget host cannot be made viable, report a gate failure and revise schedule/approach with the owner. Foreground-only delivery, public audio objects, a paid service, or an emulator-only result is not an accepted substitute.

After the experiment, revoke diagnostic sessions, remove only the recorded scratch objects, verify cleanup/accounting, preserve redacted evidence, and retain no temporary authentication backdoor. No experiment code is created during the current documentation task.

## 6. Five-day work packages

Days are elapsed implementation days, not dates. Precedence follows the table; a failed early gate changes the forecast rather than shortening required validation.

| Day/package | Work | Dependency / exit evidence |
| --- | --- | --- |
| 1 — feasibility | Provision/check eligible public host/MySQL/bucket/HTTPS; run the six-hour browser/native streaming and Android-background experiment; verify Google setup and Android picker path. Freeze package/browser versions and both player adapters. | Web cookie/seek/policy results, real CMF gate results, durable MySQL connection/restart, no-charge limits, viable Google origin/signing, native URI streaming. Any unmet gate remains explicit. |
| 2 — server vertical slice | Versioned MySQL schema, owner/app sessions, import state/claims/quota/cleanup, metadata/cover, both upload clients, paged literal search and protected media. | Cross-client tagged/tagless/invalid/duplicate imports; private access; library survives API/MySQL restart; lost-response/concurrency/quota cases pass. |
| 3 — listening | Integrate browser audio adapter and proven native service into shared listening UI; context snapshots, queue edits/restoration, shuffle/repeat, native focus/system controls and artwork. | Conformance on both engines, web route/reload/policy checks, physical-device background regression; both restart paused. |
| 4 — organization/recovery | Favourites, revision-aware ordered playlists, track deletion/reconciliation, expired-session/network recovery, limited stale-command/error UX. | Server persistence, stale-edit protection, deletion cleanup failures, renewal with inactive UI, cancelled retry behavior. |
| 5 — release | Controlled publish/migrations, signed APK, performance/acceptance pass, backup/restore drill, quota/secret/log review, operator instructions. | FRS/spec release flow passes on public deployment and CMF phone; install/update and paired MySQL/object restoration documented; accepted deviations explicitly listed. |

The scope is aggressive: browser and native playback adapters, native auth/file integration, import compensation and a no-charge public deployment can exceed five days. Shared listening screens reduce duplication; both engines still need acceptance checks. Reusing proven components does not eliminate the gates. Do not schedule excluded features or treat production durability/authentication as automatically covered by the small experiment.

## 7. Technical uncertainties and decisions to close

| ID | Uncertainty | Resolution / effect |
| --- | --- | --- |
| U1 | Oracle free capacity and enforceable ₹0 limits | Inspect actual account/region and provision only eligible resources; failed capacity/no-spend gate blocks cloud release. No paid upgrade assumed. |
| U2 | Free stable HTTPS hostname accepted by Google | Validate certificate trust, authorized origin and ownership/provider rules before committing desktop identity flow. A necessary paid domain would conflict with current budget. |
| U3 | MySQL topology/ARM64 memory/runtime | Default self-hosted MySQL 8.4 on eligible VM; benchmark bounded imports plus stream. Eligible managed HeatWave is an option only with documented topology/backup decision. |
| U4 | Native plugin versus app-owned Media3 bridge | Brief compatibility inspection followed by device experiment; required headers/renewal/autonomous queue/system controls determine adoption. |
| U5 | VBR seeking and metadata parsing | Test real indexed/unindexed/corrupt-tag samples. Report unsupported behavior; do not claim complete format support from one CBR track. |
| U6 | Google owner binding/package/cert/audience and refresh-loss behavior | Verify both sign-in surfaces and test serialized token rotation/reuse/lost response. Secure provisioning remains operator-controlled. |
| U7 | Picker URIs, unknown size, foreground cancellation | Test on CMF system/document providers, bounded temp spool when necessary; upload must not stop playback. |
| U8 | Scope of a "displayed result set" queue snapshot | Proposed all matches in active filter up to 5,000; record interpretation before implementation. Loaded-page-only behavior must not be silently chosen. |
| U9 | Physical OEM battery/audio controls | Required normal-background scenarios must pass; separately record battery saver/task dismissal/force-stop behavior. |
| U10 | Proposed limits/timings/performance | Confirm exact object/artwork/session/retry/checkpoint defaults against FRS and deployed measurements. Do not relabel proposals as confirmed owner decisions. |
| U11 | Browser matrix, audio behavior and local persistence | Finalize browser targets; verify cookie ranges, CBR/VBR time seeking, autoplay rejection, route continuity, cookie expiry, independent tab queues and reload. No browser background guarantee is assumed. |

## 8. Validation and release evidence

Use meaningful tests around authorization, token rotation, MySQL constraints/locks and migrations, duplicate/quota races, partial cloud success, range semantics/cancellation, playlist revisions, queue modes/stale commands, and restart/restore. Use representative metadata/artwork inputs and a synthetic 5,000-record search dataset within the real audio storage cap.

Reserve physical-device checks for behaviors that unit tests cannot establish: screen-lock transitions, headset/notification controls, focus/calls, network changes, native renewal, and OEM lifecycle. Run relevant checks once per material change; do not repeatedly broaden testing without a failure/new risk.

Final release evidence includes the complete upload/listening/organization journey on web and Android, denied anonymous/non-owner access, browser policy/expiry/route/reload handling, required Android background playback, saved organization after API/MySQL restart, paused browser/device restoration, paired backup restore, enforced free budgets, installation/update instructions, and recorded unresolved/accepted deviations. Full application implementation remains a later task.
