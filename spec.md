# Personal music server — five-day MVP specification

Status: Draft incorporating confirmed owner requirements. Deployment/playback feasibility and exact device build remain to be verified; detailed interaction rules are proposed for review. No implementation has started.
Date: 2026-10-08.

## 1. Goal and confirmed scope

One owner uploads personal MP3 files to private cloud object storage and listens through an Ionic React web interface and Android application. ASP.NET Core provides the backend; MySQL stores library and owner data. The release has a five-day implementation window. MySQL replaces the database originally proposed for this release, following the owner's updated instruction.

The MVP includes uploads and streaming from both web and Android, metadata and artwork extraction, library search, playback controls, a queue, shuffle, repeat, favourites, and playlists. Both clients expose library and listening/organization controls. Android background playback is required, including screen lock, app switching, notification controls, Bluetooth/headset controls, and call interruptions. Investigate it at the start of the implementation window; foreground-only Android delivery does not satisfy this release.

The server must be reachable over the public internet, including mobile data. The current service budget is ₹0. The owner selected a private APK on their CMF Phone 2 Pro running the most recent available OS, Oracle Always Free as the hosting candidate with card verification acceptable, and Google sign-in restricted to one owner with login persistence. Use the latest stable update available to that phone at testing time, rather than a beta. The owner accepted embedded-only metadata/artwork, filename/placeholder fallbacks, identical-file deduplication, track deletion, playlist management/reordering without duplicate tracks, local queue/position restoration without autoplay, server-persisted favourites/playlists, and no manual metadata editing.

Confirmed limits: 5,000 tracks as the library/search design target, 50 MiB per MP3, and 100 files per upload batch. Total audio/artwork storage is separately capped below the provisioned account's free allowance; this can become the binding limit before 5,000 tracks.

Success means the owner can upload, find a track, stream/seek, and manage a listening queue from web and Android, and retain favourites/playlists after restarting the clients and server. Required Android background playback must pass the physical-device gate in section 4. Web playback must respect browser interaction/autoplay policies; uninterrupted playback after browser suspension/closure is not a release guarantee.

## 2. Decision record and remaining gates

These questions materially affect scope, feasibility, deployment, and acceptance testing. Confirmed answers are distinguished from proposed defaults. Resolve the remaining decisions before their implementation begins.

| ID | Question | Proposed default / current status |
| --- | --- | --- |
| D1 | Upload from a desktop browser, Android, or both? | Confirmed: both desktop and Android uploads. |
| D2 | Is background playback required, or is foreground-only an acceptable fallback? | Confirmed: background playback is required. No foreground-only fallback. |
| D3 | Which Android versions and physical phone? Private APK or Google Play? | Confirmed: CMF Phone 2 Pro, most recent available OS, private signed APK. Record exact Android/Nothing OS/build at the Day 1 test. Broader version/device certification and Google Play release are excluded. |
| D4 | Which object-storage provider and backend host? Public internet/mobile data or private network/VPN? Budget? | Confirmed: public internet, ₹0 budget, and try Oracle Always Free with card verification acceptable. Actual account/region capacity, persistent disk, private object storage, HTTPS hostname, and no-charge quotas require the Day 1 provisioning gate. |
| D5 | Expected track count, maximum MP3 size, and maximum batch size? | Confirmed: 5,000-track design target, 50 MiB per file, 100 files per batch, and a separate free-storage byte cap. Proposed byte-cap default below: 8 GB for total cloud objects, lowered if actual free allowance requires it. |
| D6 | Which external identity provider? Persistent login? | Confirmed: Google and persistent login. Bind exactly one owner to Google's verified issuer/subject during provisioning. No local password login. |
| D7 | Embedded-only metadata/artwork, duplicate handling, deletion, playlist operations/duplicates, queue restoration, and manual tag editing? | Confirmed: presented scope defaults accepted, including no duplicate tracks within a playlist. |
| D8 | Should the web interface also stream music? | Confirmed: web streaming is included. Provide browser listening controls and the shared library/queue/favourites/playlist flows, using the same private media API. |

Provider selection must establish byte-range support, authenticated access, upload limits, persistent MySQL storage, stable public HTTPS access, and a deployment path that stays within the ₹0 budget. Free capacity, request counts, and traffic allowances must be verified for the actual account; free tiers are not unlimited storage or availability guarantees.

### Android options considered

The owner selected the recommended own-phone/private-APK option. The alternatives below explain the decision and are not additional release requirements.

Primary acceptance device: **CMF Phone 2 Pro on its latest available stable update**. Nothing's firmware release notes establish Nothing OS 4.1 based on Android 16 for this model. Rollouts are staggered, so this is a planning baseline, not proof of the installed build or a claim that a specific build will remain newest. Record the actual version, build number, security patch, and battery-optimization settings when testing. [Official model firmware release notes](https://nothing.community/en/d/56305-cmf-phone-2-pro-nothing-os-b41-260415-1710-changelog).

| Option | Requirements and tradeoff |
| --- | --- |
| Own phone and private signed APK — recommended | Supply the physical phone model/Android version; permit APK installation; retain a signing key for updates. Validate all required background scenarios on this device and use a modern Android emulator for supplementary checks. No Play account is required. |
| Wider version range and private APK | Define a minimum supported Android version and test more versions/devices. Proposed range is Android 10+ if wider support is chosen; this adds testing effort to the five-day window. |
| Google Play | Requires an eligible developer account, signing/release declarations, and applicable testing/review. A new account costs US$25. Affected new personal accounts require at least 12 testers for 14 continuous days before applying for production access, so this is incompatible with the present budget/window unless existing account eligibility changes the situation. |

Sources: [Google Play account requirements](https://support.google.com/googleplay/android-developer/answer/6112435), [personal account testing](https://support.google.com/googleplay/android-developer/answer/14151465).

Minimum supported Android version is different from the target SDK. Capacitor 8 documents minimum API 24, compile/target API 36, and Android Studio 2025.2.1 or newer. The chosen playback/upload/authentication plugins may impose additional constraints. Confirm versions together during the early gate; do not assume compatibility based only on the phone's OS. [Capacitor 8 requirements](https://capacitorjs.com/docs/updating/8-0).

### ₹0 hosting decision and gate

Oracle Always Free is the selected candidate. The alternatives below are recorded for context; no alternative is automatically approved if Oracle provisioning fails.

- **Recommended candidate if card verification and capacity are available:** Oracle Always Free compute for ASP.NET Core and a private MySQL service with persistent block storage, plus private object storage within the account's free allowances. Use Always Free resources rather than depending on temporary trial credits. Card verification and a temporary authorization hold are required; free instances may be unavailable or reclaimed when idle. This is a candidate pending provisioning, not a guaranteed free host. [Oracle signup requirements](https://www.oracle.com/cloud/free/faq/), [Always Free resource conditions](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm).
- **Alternative if an existing computer can remain online:** host the backend/MySQL service there and choose free private object storage separately. Public HTTPS reachability, a stable hostname, authentication redirects, and upload/streaming limits still need a workable plan. Existing electricity/internet usage is an owner-provided resource. Sleeping/offline hardware makes the server unavailable.
- Cloudflare R2 offers 10 GB-month of free storage, operation allowances, and no egress charge, but requires a subscription checkout and charges for usage above allowances. It is only a candidate if its billing model can satisfy the owner's budget constraint; free allowance alone is not a hard ₹0 guarantee. [R2 pricing](https://developers.cloudflare.com/r2/pricing/), [R2 signup](https://developers.cloudflare.com/r2/get-started/).
- Backblaze B2 offers the first 10 GB free and free egress up to three times average monthly storage. Its storage, egress, and operation limits must fit actual listening usage and a no-spend configuration before selection. [B2 pricing](https://www.backblaze.com/cloud-storage/pricing).
- Render's free web service has an ephemeral filesystem and cannot attach a persistent disk, so it cannot host the proposed persistent MySQL service on its free local disk. An external database would be a separate hosting choice, not a persistence fix for that disk. [Render free-service restrictions](https://render.com/docs/free).

Do not purchase services or enable paid overages to resolve a quota problem. Refuse uploads before exceeding the configured storage budget, account for artwork and partial uploads, and stop/reject operations when hard request/traffic allowances would be exceeded where those apply. An advisory billing alert alone is insufficient to guarantee no spend. The deployment gate must identify an enforceable no-charge account/quota arrangement or record that the hosting requirement is unresolved. Do not guarantee 24/7 availability on an unprovisioned free tier.

## 3. Proposed product behavior

The stories below combine the accepted scope defaults with proposed interaction details; detailed limits and behaviors not explicitly answered remain reviewable draft choices:

- The web interface supports sign-in, batch uploads/status, library/search, streaming, player/queue/shuffle/repeat, favourites, and playlists. The Android app supports the same product flows with system file-picker uploads and native background playback.
- Import embedded tags and embedded artwork only. Use filename and placeholder fallbacks. Do not fetch external metadata or artwork, and do not expose manual metadata editing.
- Detect duplicate imports by identical file bytes, not title/artist. Keep distinct encodings or files with different tags as separate tracks.
- Favourites and playlists persist on the server. Queue, shuffle/repeat settings, current track, and playback checkpoint persist separately in browser-local storage and on the Android device. Restore paused after reload/restart; no cross-device or cross-tab live playback synchronization.
- Restoring a stopped app does not autoplay. Continuing an already-running background playback session is different from reopening after process termination.
- The owner can delete tracks. Playlists support creation, renaming, deletion, adding/removing tracks, and reordering; duplicate tracks within a playlist are skipped. The listening queue can contain duplicate entries.

## 4. Early background-playback investigation

### Documentation findings

Android documents hosting the player and media session in a service for background playback, with a media playback foreground-service declaration and permissions. Its media session also provides system playback controls. [Android background playback documentation](https://developer.android.com/media/media3/session/background-playback).

A Capacitor media-session plugin documents reliability problems with WebView audio in the background. Its compatibility guidance names older Capacitor versions, so compatibility with the chosen stack must be checked. [Plugin documentation](https://github.com/jofr/capacitor-media-session).

Another candidate plugin documents native URL playback, background notifications, and Capacitor 7/8 installation paths. Its documentation does not prove that it satisfies this application's queue, authentication, and lifecycle requirements. [Native audio plugin documentation](https://github.com/mediagrid/capacitor-native-audio).

Android audio-focus behavior must also be checked on the chosen Android target and test device. [Android audio-focus documentation](https://developer.android.com/media/optimize/audio-focus).

**Inference:** prefer evaluating a native playback service exposed through Capacitor; do not assume browser audio plus a notification will satisfy background playback. No library has been selected and no device feasibility has been demonstrated.

### Day 1 feasibility gate — future implementation work

Time-box the first implementation work to a playback investigation before building the complete library UI. This specification phase performs documentation research only; it does not create a prototype.

The investigation must demonstrate on the owner's CMF Phone 2 Pro with its latest available stable OS and normal battery settings:

1. Stream a private MP3 through the proposed authenticated delivery path; seek forward and backward.
2. Lock the phone and switch to another app for at least 15 minutes and across at least two track transitions. Advancement must work while the Ionic view is inactive.
3. Use notification/lock-screen play, pause, previous, and next controls; verify title, artist, and artwork update.
4. Exercise required Bluetooth/headset controls and route disconnection. Pause on headphone/Bluetooth disconnection rather than unexpectedly playing through the speaker.
5. Exercise a call or competing audio application: respect audio focus, and do not resume after the owner explicitly paused. Propose manual resume after a call or permanent focus loss; temporary ducking follows platform behavior.
6. Test a brief network interruption, Wi-Fi/mobile-data change, and a playback request after access credentials or a signed URL have expired. Renewal must work without foregrounding the UI.
7. Return to the app and verify one player, one current track, and consistent queue/position. Record behavior when swiping away the task, stopping the process, and force-stopping the app separately.
8. Record whether ordinary background playback needs an OEM battery-optimization exemption. Test battery-saver mode separately and document limitations; do not present an emulator result as proof of physical-phone behavior.

Deliverable: candidate/version, setup needs, physical device/OS, tested scenarios, observed failures, and a pass/fail recommendation. Background playback is required: if the gate fails, revise the approach or schedule with the owner. Foreground-only playback fails release acceptance.

## 5. User stories and acceptance criteria

### US1 — Sign in as the sole owner

As the owner, I want private access to my server so that only I can manage and listen to my library.

- Exactly one configured external identity is authorized as owner. A successful provider login by any other identity is rejected; first login must not automatically claim ownership. Identify the owner by a verified provider/issuer and immutable subject identifier, not an unverified email supplied by the client.
- Library, artwork, upload, mutation, and stream-access requests require owner authorization. Anonymous clients cannot list objects or obtain new media access.
- Provider credentials/secrets are not embedded in the APK. Verify identity signature, issuer, audience, and expiry on the backend and use only identity scopes needed for sign-in. Protect browser sessions and mutations against CSRF as appropriate to the session design; keep persistent Android session credentials in platform-protected storage. Deployed access uses HTTPS.
- Android sign-in uses a supported native identity SDK or a system-browser authorization flow with protected callbacks/state and PKCE where applicable, rather than provider sign-in inside the Ionic WebView. Configure desktop and Android clients/redirects separately. Cancelled/denied provider login returns safely to sign-in. [Native OAuth guidance](https://www.rfc-editor.org/rfc/rfc8252), [Google embedded-browser policy](https://developers.google.com/identity/protocols/oauth2/policies).
- A valid persisted app session survives app restart. Verify Google identity at sign-in and issue the app's own bounded, renewable owner session; do not treat a Google access token as authorization to this music API. App-session expiry renews when allowed or asks for sign-in without discarding server data. Sign-in persistence must not depend on repeatedly prompting Google during background playback.
- Signing out stops playback and clears local credentials and restored listening state. It retains the server library, favourites, and playlists.
- Authentication failures are distinguishable from connection/provider failures. Backend authentication/session endpoints are rate limited. Provider account recovery remains with the identity provider; no local password or email-recovery system is included.
- Any temporary storage URL is scoped to the required object and bounded lifetime. A previously issued URL can remain usable until expiry; immediate URL revocation is not assumed. The delivery design must document that lifetime.

### US2 — Upload a batch of MP3 files

As the owner, I want to select local MP3 files and see each import's outcome so that I can build my library reliably.

- From both the desktop browser and Android system file picker, select multiple MP3s within confirmed limits. Show per-file progress/status and a batch summary: imported, duplicate, or failed. Cancelling file selection leaves the library unchanged. Use explicit selected-file access rather than scanning the entire phone library.
- Handle picker-provided files without assuming a desktop-style filesystem path. Record picker/device limitations in the Day 1 device check. Both clients enforce the same server limits and duplicate rules; verify a desktop-uploaded file streams on Android and vice versa.
- Validate file content sufficiently to identify supported MP3 input; extension alone is insufficient. Reject empty files, clearly invalid/non-MP3 content, and files over the limit with actionable errors.
- Accept supported constant- and variable-bitrate MP3 samples without transcoding. Malformed optional tags must not reject otherwise usable audio. Exhaustive detection of every damaged audio frame is not promised.
- A successful import appears in the library only after its original audio object and essential track record are available. A failed/partial upload does not appear as a playable track.
- One failed file does not roll back successful files in the batch. Retrying failed files does not create duplicates, including concurrent/repeated imports of identical bytes.
- Identical files are reported as duplicates. Same title/artist or same filename with different bytes remains importable.
- Interrupted uploads can be restarted from the beginning. Resumable multipart uploads and uploads continuing after the upload page closes or Android backgrounds/terminates the upload view are not required. A foreground Android upload must not stop an existing playback service.
- Reject a batch/file when the reserved/imported audio, artwork, and outstanding uploads would exceed the confirmed no-charge storage cap. Explain the quota error without starting a partially billable import. Track-count capacity does not imply sufficient free bytes for every track.
- Partial objects are cleaned up or recorded for retryable cleanup; the owner can identify failed imports without seeing storage secrets.

### US3 — Extract metadata and artwork

As the owner, I want recognizable track information and covers without entering them manually.

- Extract available title, artist, album, album artist, track/disc number, and duration from supported embedded metadata/audio headers. Optional fields may be absent.
- Trim empty tag values. Use the filename without its extension when title is missing, and display `Unknown artist` / `Unknown album` when those fields are missing.
- Preserve Unicode text and show metadata as text, including characters that resemble HTML.
- Use a valid embedded front-cover image when available; otherwise use the first supported embedded image. Proposed supported artwork inputs are JPEG and PNG. Missing, unsupported, corrupt, or oversized artwork uses a placeholder without failing the track import.
- Proposed artwork safety limits are 5 MiB encoded input and 20 megapixels decoded dimensions. Exceeding either limit uses the placeholder; bounds apply before allocating an unbounded decoded image. The original MP3 bytes are retained unchanged.
- The track can be searched and streamed even when optional metadata or artwork extraction fails. Metadata extraction itself is part of import, not a later manual task.

### US4 — Browse and search the library

As the owner, I want to find tracks by title, artist, or album so that I can play my music quickly.

- Display title, artist, album, duration when available, artwork/placeholder, and favourite state. Empty library, loading, failure, and no-results states have distinct messages/actions.
- Search matches case-insensitive substrings in title, artist, or album. Trim leading/trailing query whitespace; an empty query returns the library.
- Search applies to the entire library, including tracks beyond the currently visible page. Results have stable ordering by title, artist, then track identity.
- A query with punctuation, quotes, or Unicode does not break search. Accent-insensitive matching, fuzzy search, and lyrics search are excluded.
- Imported/deleted tracks appear/disappear after refresh. Loading or updating library data does not interrupt current playback.
- At the confirmed library size, scrolling and searching remain usable without rendering/loading all artwork simultaneously. Quantitative targets are proposed in section 7.

### US5 — Stream and control a track

As the owner, I want play/pause, seek, previous, and next controls so that I can listen interactively.

- Selecting a track starts a queue snapshot of the currently displayed result set in its displayed order, beginning at the selected track. Playing a playlist uses its stored order. Searches/playlist edits afterward do not silently replace an active queue.
- Show current title, artist, artwork, elapsed time, duration when known, and playing/paused/buffering/error state. Disable meaningless controls for an empty queue.
- Pause retains position; play resumes it. Seek moves within the track without fetching the complete file first; clamp requests to valid bounds. Verify variable-bitrate seeking on representative MP3s.
- Stream original MP3 bytes from private storage using byte-range-capable delivery. Storage/API authorization must work for initial play and subsequent seek/range requests.
- Previous restarts the current track when its position is greater than three seconds; otherwise selects the previous queue entry. With no previous entry, restart the current track. Next selects the next entry under US7's boundary rules.
- Only one playback engine emits audio. Rapid repeated control taps cannot produce overlapping players or a stale track replacing a newer selection.
- An unsupported/damaged track or unavailable object shows an error and allows retry, next, or removal. Do not endlessly skip or retry an entire broken queue.
- App launch does not autoplay a restored session. If a service is already playing, reopening the UI attaches to that session.
- On web, a single persistent browser audio controller survives in-app route changes and uses same-origin cookie-authenticated media URLs. Start/resume requires user interaction when browser policy requires it; a rejected play request leaves a visible paused/press-play state. Do not fetch a full audio Blob before playback/seeking.
- An expired browser session stops new protected media requests and presents sign-in; successful sign-in restores the listening context paused. Browser reload does not autoplay.

### US6 — Manage the listening queue

As the owner, I want to see and change upcoming tracks without losing the current song.

- Show the effective playback order and current entry. Support play next, append, remove upcoming entry, and reorder upcoming entries. Queue duplicates have separate identities so removing one occurrence leaves the others.
- Play next inserts immediately after the current entry, including when shuffle is active. Manual reordering updates the actual upcoming order.
- Removing/reordering upcoming entries does not restart the current track. Removing the current entry stops it, selects the next available entry at zero, and remains paused. An empty queue stops playback.
- Clearing the queue stops playback. Replacing it through an explicit track/playlist selection starts the selected context.
- Restore queue entries, current entry, shuffle/repeat state, and the last saved position after process restart. Save position at least every ten seconds while playing and on pause/track change; a crash can lose up to one checkpoint interval.
- Removed library tracks are discarded during restoration/refresh. An invalid saved position is clamped; if the saved current track is gone, select the next valid entry paused.
- Queue persistence is local to the browser profile/client instance or Android device. Library membership changes are reflected when fetched, without remote playback synchronization. Each browser tab has one audio engine and an independent active queue; concurrent clients do not automatically stop each other.

### US7 — Shuffle and repeat

As the owner, I want predictable shuffle and repeat modes for my current queue.

- Repeat cycles through off, all, and one; shuffle is independently on/off. Defaults are repeat off and shuffle off, with subsequent settings restored locally.
- Enabling shuffle leaves the current track playing and randomizes unplayed queue entries. Each queue entry is visited once per cycle; duplicate entries still count separately.
- Turning shuffle off restores the base/manual upcoming order without replaying entries already completed in that cycle. Show the resulting order in the queue.
- Repeat off stops at the end. Repeat all wraps to the beginning; with shuffle, create a new cycle and avoid immediately repeating the same entry when alternatives exist. Repeat one repeats the current entry on natural completion.
- Explicit next/previous overrides repeat one. At the last entry, explicit next wraps only with repeat all; otherwise it stops. Previous at the first entry restarts it, including repeat-all mode.
- A one-entry queue and empty queue have valid behavior: repeat all/one can repeat one entry; an empty queue never starts or loops. Track errors never trigger unlimited repeat retries.

### US8 — Save favourites

As the owner, I want a persistent favourites collection so that I can return to tracks I like.

- Toggle favourite state from a library row or player; display the same state everywhere after a successful save.
- A favourites view contains only favourited tracks and can be used as a playback context. Empty favourites has a clear state.
- Favourites survive app/server restart and sign-out/sign-in. Repeated requests cannot create duplicate favourite records.
- A failed save is shown and does not pretend to be persisted. Removing a favourite does not interrupt current playback or remove the track from an existing queue snapshot.

### US9 — Manage playlists

As the owner, I want ordered named playlists so that I can organize listening sessions.

- Create, rename, and delete playlists. Trim names; reject blank names and names over a proposed 100-character limit. Under the draft default, reject duplicate playlist names case-insensitively.
- Add library tracks, remove entries, and reorder them. Under the draft default, adding an existing track reports it as already present and leaves order unchanged.
- Changes persist on the server and survive restarts. A failed save leaves a visible error and retains the last confirmed server state.
- Play from the beginning or a selected playlist entry; the queue uses a snapshot of stored playlist order and current shuffle/repeat settings. An empty playlist cannot start playback.
- Deleting a playlist removes its definition/membership only. It does not delete tracks or stop a queue already created from it.
- Public sharing, collaborative editing, folders, automatic playlists, and playlist import/export are excluded.

### US10 — Delete a library track

As the owner, I want to remove an uploaded track so that my library and storage remain manageable.

- Confirm destructive track deletion. Remove the track from library search, favourites, and playlist memberships; preserve the remaining playlist order.
- Remove deleted entries from the local queue when the app receives/refreshes the deletion. If the current entry is removed, stop it and select the next valid entry paused, as in US6.
- Delete private audio and derived artwork, or retain a retryable cleanup record when storage deletion fails. A database-deleted track never reappears merely because an orphan object exists.
- A failed database mutation is reported; do not claim successful deletion. Refresh resolves stale views from another owner session.
- Deletion prevents issuing new stream access. Already-buffered bytes and unexpired previously issued URLs are subject to the delivery policy in US1; remote immediate interruption is not guaranteed.

### US11 — Handle interrupted connectivity and expired access

As the owner, I want understandable recovery from connection problems without losing my saved library organization.

- Brief interruptions may consume an existing playback buffer; show buffering when audio cannot continue. This does not constitute offline downloads.
- If connectivity returns while playback is still requested, attempt bounded recovery at the last position. Proposed policy: three retries with increasing delay, then an explicit retry action. Pause/logout/deletion cancels recovery.
- Distinguish connection errors, expired owner sessions, missing tracks, and playback-format errors. Recoverable temporary media access is renewed transparently when the owner session permits it, including for background queue transitions.
- When recovery needs sign-in, stop further stream requests and ask for sign-in on return to the app. Do not loop silently in the background.
- Offline mutation queuing is excluded. Failed uploads/favourite/playlist changes remain clearly unsaved and can be retried; server data is not discarded.

## 6. Edge-case checklist

| Scenario | Required outcome / referenced behavior |
| --- | --- |
| Zero-byte file, renamed non-MP3, excessive size/count | Per-file/selection error; no playable phantom record; US2. |
| Missing tags, non-Latin text, invalid cover, multiple covers | Defined text/artwork fallback; valid audio still imports; US3. |
| Same bytes uploaded twice or concurrently | One imported track, duplicate outcome for the others; US2. |
| Same filename/title, different bytes | Separate tracks; no tag-based merging; US2. |
| Upload/storage/metadata/database failure mid-import | Successful files remain; failed entry can restart; incomplete objects tracked/cleaned; US2–US3. |
| Invalid search characters or no matching tracks | Safe query and distinct no-results state; US4. |
| Repeated fast play/seek/next actions | One player, final intended selection wins; US5. |
| Signed URL/token expires during seek or between background tracks | Authorized renewal or a clear sign-in/error state; US11 and Day 1 gate. |
| Library deletion while queued/playing; stale server references | Remove unavailable entries when known; stop deleted current entry; no infinite skip loop; US6/US10. |
| Shuffle plus repeat, explicit next, duplicated queue entries | Follow explicit cycle and boundary rules; US6–US7. |
| Empty or one-track queue/playlist | No invalid index, unwanted autoplay, or busy loop; US6–US9. |
| Rename collision, whitespace-only name, failed reorder save | Validation or visible unsaved error; preserve last confirmed state; US9. |
| Call, competing audio, headphone disconnect | Respect agreed audio-focus/route behavior; section 4. |
| Lock screen, app switch, task dismissal, process kill, force stop | Test separately; screen lock/app switching/system controls are required. Force-stop survival is excluded. |
| Restart with stale queue or position past duration | Discard missing entries, clamp position, restore paused; US6. |
| Browser play blocked, route navigation, reload, or expired cookie | Show press-play/sign-in as appropriate; route changes retain one player; reload restores paused; anonymous media requests fail. |
| Backend restart or object-storage outage | Persistent library survives; readable failures and retry; US11/section 7. |

## 7. Data, security, and measurable quality

- MySQL persists track metadata, private object references, import status, favourites, playlist definitions, and ordered memberships. Audio and derived artwork reside in private object storage. MySQL and object storage are separate resources; import/deletion recovery must handle partial success.
- MySQL uses InnoDB transactions and durable data storage. The proposed deployment has one backend instance and one private MySQL service; database replication, multi-instance application deployment, and ephemeral production database storage are outside this release. MySQL is accessed as a database service, not as a shared database file.
- The ₹0 budget is a release constraint. Confirm Oracle storage, request, and traffic caps before deployment. Product track limit: at most 5,000 imported tracks. Proposed byte-cap default: 8 GB (decimal) of total cloud objects, including audio, derived artwork, and outstanding partial uploads; lower the byte cap if the actual free allowance is smaller. Reserve space for outstanding uploads and account for provider-side retained object versions/backups where applicable. The 5,000-track target is an indexing/search envelope, not a free-storage guarantee. Storage exhaustion must not corrupt MySQL or remove existing tracks.
- Scope all management and stream-access actions to the sole authenticated owner. Keep cloud credentials on the server/deployment environment. Do not expose bucket-listing access, passwords, session tokens, or complete signed URLs in user-facing errors or logs.
- No durable client copy of full audio is required. Transient playback buffering and small metadata/artwork caches are permitted; they do not offer an offline audio library.
- Proposed performance criteria: with a 5,000-track metadata dataset, 10 Mbps network throughput, approximately 100 ms round-trip latency, and an awake backend, search results render within two seconds and playback starts within five seconds for at least 19 of 20 representative attempts. Use representative audio within the object quota; no need to upload 5,000 full MP3s to test search capacity. Define the deployed test path and cold-start behavior before treating these as release thresholds.
- Upload tests use confirmed batch/file limits; playback tests include representative constant- and variable-bitrate MP3s and embedded/missing/corrupt metadata/artwork. Test web playback/seek/queue/reload/session expiry on recorded browser versions and Android playback, queue boundaries, recovery, and restart persistence on the physical device. Proposed first browser targets are current stable desktop Chrome and Edge; finalize the browser matrix before implementation.
- Record how to provision the owner, deploy/update the backend, install the APK or agreed distribution artifact, set secrets, and back up/restore the MySQL database together with its referenced objects. Automated disaster recovery and high availability are excluded.

## 8. Explicit exclusions

The confirmed exclusions are offline downloads, transcoding, recommendations, and multiple users.

Additional proposed exclusions to keep the five-day release bounded:

- Other audio formats, DRM-protected content, format/bitrate conversion, adaptive streaming, gapless playback guarantees, crossfade, equalizer, replay-gain normalization, and playback-speed controls.
- iOS native support, casting, Android Auto integration, voice assistants, and dedicated Wear OS support. Normal Android notification/headset controls are included and required. Web streaming is included; guaranteed browser background/closed-tab playback and PWA offline audio are excluded.
- Public signup, local password login/recovery, multiple identity-provider integrations, shared libraries, public playlists, and collaboration. The one selected external provider is included; provider-managed recovery is external to this app.
- External metadata/cover lookup, manual tag editing, lyrics, automatic deduplication of different encodings, folder synchronization, local filesystem scanning, and directory uploads.
- Resumable upload protocols, guaranteed background uploads, offline mutations, bulk library editing/deletion, and automated imports from other music services.
- Cross-device queue/position synchronization, listening history/analytics, smart playlists, playlist import/export, and automatic recommendations.
- Guaranteed continuation after force stop, reboot, OS process termination, or severe network loss. A saved queue can be restored paused; force-stop behavior is distinct from ordinary background playback.
- Google Play/public app-store launch, broad OEM/device certification, multi-instance deployment, high availability, and a public CDN with permanently public media URLs.

## 9. Proposed five-day sequence

This is a plan for later implementation, not authorization to implement now. Resolve open requirements first. Days refer to the implementation window, not calendar dates.

| Day | Focus | Exit evidence |
| --- | --- | --- |
| 1 | Resolve deployment/device/browser decisions and provision a no-charge persistent host; investigate private range streaming in browser and native background playback; verify Android picker and external-login paths. | Recorded web/device gate outcomes, selected delivery/player approach, viable no-charge host, and explicit resolution if a gate fails. |
| 2 | External owner authentication, private uploads from desktop and Android, metadata/artwork extraction, MySQL persistence, search/library. | Both upload surfaces process invalid/duplicate/tagless files as specified; only the configured external identity is authorized. |
| 3 | Shared listening UI, browser audio adapter and native Android integration; seeking, queue, shuffle/repeat, local restoration and required Android system controls. | Web/player and Android queue/control/lifecycle scenarios pass on recorded targets. |
| 4 | Favourites, playlists, deletion, expired-access/network recovery, integration fixes. | Organization survives restart; unavailable/deleted media and expired access recover as specified. |
| 5 | Deploy the agreed backend and Android artifact; run complete acceptance flow and record limitations. | End-to-end evidence, install/deployment instructions, backup/restore procedure, and unresolved defects listed. |

## 10. Release acceptance

The release is complete only when:

1. Confirmed decisions D1–D8 are reflected in implementation, phone/OS/browser versions and provisioned service limits are recorded, and proposed interaction/quality rules have been reviewed. The selected deployment passes its feasibility gate.
2. The applicable user-story criteria pass using deployed private storage and the physical Android test device. Record accepted deviations rather than declaring them passed.
3. The owner can sign in through the selected provider, upload valid/invalid/duplicate files, search, stream/seek, modify a queue, exercise shuffle/repeat, save favourites, create/reorder/play a playlist, and delete a track from web and Android. Another valid provider identity is denied owner access.
4. Server restart preserves library/favourites/playlists; web reload and Android process restart restore valid paused listening state. Browser route changes retain playback; blocked autoplay and expired cookies have clear handling. Anonymous library/media-access requests fail.
5. Required background playback passes the Day 1 scenarios and final regression check. Foreground-only delivery is a failed release criterion.
6. Public-internet/mobile-data access and the confirmed no-charge deployment arrangement work, including persistent MySQL and enforced quotas. Source/configuration documentation and the agreed installable artifact are available; no excluded feature is required for the acceptance flow.

This draft does not yet establish background-playback feasibility, Oracle resource availability, or a guaranteed five-day outcome. The ₹0 deployment and required background playback must pass their early gates; neither can be assumed from documentation alone.
