# Functional Requirements Specification

Document: FRS. Status: requirements baseline with explicitly marked proposed details.
Date: 2026-10-08. Source: [spec.md](spec.md) and subsequent owner instructions to use MySQL and include web streaming.

## 1. Purpose and document responsibilities

Build a personal music server in five implementation days. One owner uploads local MP3s from web and Android, stores them privately in cloud object storage, and streams them through Ionic React web and Android clients. Both expose listening and library organization. ASP.NET Core provides the API and MySQL provides durable relational data.

This document defines what the product must do. [SDS-FE.md](SDS-FE.md) defines frontend and Android design; [SDS-BE.md](SDS-BE.md) defines backend, database, authentication, and canonical HTTP contracts. [implementation_plan.md](implementation_plan.md) will define execution order and experiments after those designs are written. These are documentation deliverables; they do not authorize implementing or provisioning the application in this task.

Confirmed scope takes precedence over proposed design details. The implementation documents may choose technical mechanisms without silently dropping a requirement. A feasibility failure requires a recorded scope/schedule decision; it is not a passing release criterion.

## 2. Actors, platforms, and boundaries

| Actor/system | Responsibility |
| --- | --- |
| Owner | The sole authorized Google identity; uploads, listens, organizes, and deletes music. |
| Other Google identity / anonymous client | Denied owner access, even if Google authentication succeeds. |
| Web browser | Google sign-in, MP3 batch selection/upload/outcomes, library/search, streaming/player, queue/shuffle/repeat, favourites and playlists. |
| Android app | Google sign-in, selected-file uploads, library/search, native background listening, organization. |
| Google | Identity verification and Google-account recovery; no access to music objects is required. |
| Backend / MySQL / private object storage | Owner authorization, import, metadata, library organization, protected original-byte delivery, and recovery. |

Android distribution is a private signed APK; web is served over HTTPS. The physical Android acceptance device is the owner's CMF Phone 2 Pro on its latest available stable OS. Record exact Android/Nothing OS/build and normal battery settings at testing time; broad device certification is excluded. Proposed first web acceptance targets are current stable desktop Chrome and Edge; record exact versions and finalize the matrix before implementation.

Access must work over public HTTPS and mobile data. The service budget is ₹0. Oracle Always Free is the selected hosting candidate and card verification is acceptable, subject to actual capacity and no-charge resource limits. Neither resource availability nor uninterrupted free hosting has been demonstrated.

## 3. Functional requirements and acceptance

### FR-01 — Sole-owner authentication (US1)

As the owner, I can sign in with Google and retain a valid app session across restarts.

- Provision exactly one verified Google issuer/subject as owner. An arbitrary first login cannot claim ownership; client-supplied email or user ID alone cannot authorize access.
- Desktop and Android use supported Google sign-in mechanisms. Do not put Google account sign-in inside the Ionic WebView.
- Protected library, artwork, upload, organization, and media requests require owner authorization. Anonymous bucket/object access is disabled.
- App-session expiry renews when permitted, including during background playback; otherwise request sign-in and stop further protected requests. Google tokens are not the app's API session tokens.
- Signing out stops playback and removes local session/queue restoration data, while server library/favourites/playlists remain.
- Cancelled/denied sign-in, expired sessions, forbidden identities, and connection failures have clear outcomes. Provider recovery stays with Google.

Acceptance: the configured owner can use both clients after restart; another valid Google identity and an anonymous client cannot obtain library or stream access; sign-out prevents new requests with the revoked app session.

### FR-02 — Upload from both clients (US2)

As the owner, I can upload selected local MP3s in a batch and identify each result.

- Support desktop file selection and Android system file-picker selection. Android must work with picker-provided content URIs; it does not scan the device library.
- Limits: 100 selected files per batch, 50 MiB (52,428,800 bytes) per MP3, and 5,000 imported tracks. A separate storage-byte cap may bind earlier.
- Report each file as queued/uploading/processing/imported/duplicate/failed, with byte-transfer progress and a batch summary. Receiving all bytes does not imply successful import.
- Validate MP3 content rather than trusting extension or declared MIME. Reject empty, oversized, clearly unsupported, or clearly damaged input. Support representative constant- and variable-bitrate MP3s without conversion.
- Deduplicate identical bytes, including concurrent imports and lost-response retries. Different bytes with the same filename/title/artist remain separate tracks.
- Do not expose an incomplete import as playable. Successful files survive another file's failure.
- Interrupted transfers restart from zero. Upload continuation after closing/backgrounding the upload view is not required. A foreground upload must not stop an existing playback session.
- Reserve/check cloud capacity before writing cloud objects; track and clean partial objects. A quota rejection must not create a partly billable import.

Acceptance: both clients import valid MP3s, reject invalid inputs, show duplicate outcomes, and can play each other's uploaded tracks. Repeat/retry/concurrent imports produce one track for identical bytes.

### FR-03 — Metadata and artwork (US3)

As the owner, I see recognizable information without entering tags manually.

- Extract available embedded title, artist, album, album artist, track/disc numbers, and duration. Preserve Unicode and render metadata as text.
- Empty/missing title falls back to filename without extension. Missing artist/album displays `Unknown artist` / `Unknown album`.
- Prefer a supported embedded front cover, then the first supported embedded cover. JPEG/PNG are proposed inputs. Missing/corrupt/unsupported/oversized cover uses a placeholder.
- Proposed input limits: 5 MiB encoded artwork and 20 megapixels. Valid audio remains importable when optional tags/artwork fail. Original MP3 bytes remain unchanged.
- No external lookup or manual metadata editing.

Acceptance: tagless, Unicode-tagged, and invalid-cover samples remain searchable/playable with the defined fallbacks; original bytes match the uploaded file.

### FR-04 — Library and literal search (US4)

As the owner, I find tracks by title, artist, or album.

- Show title, artist, album, available duration, artwork/placeholder, and favourite state.
- Trim query whitespace; empty query returns the library. Match case-insensitive literal substrings across the entire library, including unloaded pages. Keep accents distinct; fuzzy/lyrics/accent-insensitive search is excluded.
- Sort consistently by title, artist, then stable track identity. Punctuation, quotes, `%`, `_`, and Unicode do not alter query semantics or break the request.
- Distinguish loading, empty library, no matches, and connection failure. Refresh reflects imports/deletions without interrupting playback.

Acceptance: a 5,000-track metadata dataset can be searched beyond the first page, with stable ordering and literal punctuation handling.

### FR-05 — Playback and seeking (US5)

As the owner, I can stream, play/pause, seek, move previous/next, and see the current track in web and Android clients.

- Selecting a library result or playlist entry creates a queue snapshot in that context's order, beginning at the selected entry. Later search/playlist changes do not replace the active queue.
- Show track metadata, elapsed time, known duration, and playing/paused/buffering/error state. Only one engine produces audio.
- Pause keeps position. Seek clamps to valid bounds and uses byte-range delivery without first downloading the entire track. Validate variable-bitrate samples.
- Proposed previous behavior: restart if position is greater than three seconds; otherwise go to previous entry, or restart at the first entry. Explicit next/previous overrides repeat-one.
- Rapid actions must resolve to the latest intended selection without overlapping audio. Unavailable/unsupported tracks allow bounded retry, next, or removal rather than endless skipping.

Acceptance: seek forward/backward in streamed CBR and VBR MP3s in both clients, inspect actual range traffic, and verify one coherent player per active client under rapid controls.

### FR-06 — Required Android background playback (US5 and spec section 4)

As the owner, I keep listening while the screen is locked or another app is active.

- Continue for at least 15 minutes and two automatic track transitions on the CMF Phone 2 Pro with the Ionic view inactive.
- Notification/lock-screen play, pause, previous, and next work and metadata/artwork update. Bluetooth/headset controls work; disconnection pauses rather than unexpectedly using the speaker.
- Respect calls and competing audio focus. Do not resume after an explicit pause. Proposed behavior after calls/permanent focus loss is manual resume; temporary ducking follows platform behavior.
- Recover from Wi-Fi/mobile-data changes and expired access without needing the UI when the app session remains renewable.
- Reopening the UI attaches to the existing service. Restart after process termination restores paused state and does not autoplay.
- Record task-dismissal and battery-saver behavior separately. Force-stop/reboot/process-kill survival is not guaranteed.

Acceptance: the physical-device gate passes. Foreground-only playback cannot satisfy this release.

### FR-07 — Queue (US6)

As the owner, I inspect and change upcoming tracks without restarting the current song.

- Support play next, append, remove, reorder upcoming entries, clear, and explicit replacement by a new context. Separate queue-entry IDs permit duplicate track occurrences.
- Play next inserts immediately after current, including in shuffle mode. Manual reordering changes actual upcoming order.
- Removing the current entry stops it, selects the next valid entry at zero, and remains paused. Clearing/emptying stops playback.
- Persist current entry, base/effective order, shuffle/repeat, and position separately in browser-local storage and Android app-private storage. Proposed checkpoint interval: ten seconds plus pause/track change; a crash may lose one interval. Each browser tab has an independent active queue; no live cross-tab synchronization or exclusive global player is required.
- Restore paused, discard deleted tracks when validated, and clamp stale positions. No server queue or cross-device position synchronization.

Acceptance: queue edits do not restart unrelated current audio; restart restores a valid paused queue; duplicate occurrences can be removed independently.

### FR-08 — Shuffle/repeat (US7)

- Defaults are shuffle off/repeat off; restore subsequent settings locally. Repeat modes: off, all, one.
- Shuffle retains the current entry and visits each upcoming entry once per cycle. Turning off restores base/manual remaining order without replaying completed entries.
- Repeat off stops at the end. Repeat all wraps; shuffled cycles avoid immediate reuse of the last entry when alternatives exist. Repeat one applies to natural completion.
- Explicit next at the end wraps only in repeat-all; otherwise stop. Previous at the beginning restarts. Empty/one-entry queues and track errors must not cause invalid indexing or infinite retries.

Acceptance: exercise the mode combinations with zero, one, and several entries, including duplicate tracks and failed media.

### FR-09 — Favourites (US8)

- Toggle from library/player and browse favourites as a playback context.
- Server saves are idempotent and persist across client/server restart and sign-out. Show failed mutations as unsaved.
- Unfavouriting does not stop playback or alter an existing queue snapshot.

Acceptance: favourite state is consistent after refresh/restart and repeated save requests do not create duplicates.

### FR-10 — Playlists (US9)

- Create, rename, delete, add/remove tracks, reorder, and play from the beginning or a selected entry.
- Trim names; proposed validation is 1–100 Unicode characters and case-insensitively unique names. No duplicate tracks in a playlist.
- Preserve order and server persistence. A failed/stale save cannot overwrite confirmed order silently.
- Empty playlists cannot start playback. Deleting a playlist does not delete music or stop its existing queue snapshot.

Acceptance: create/reorder/play a playlist, reject duplicates/invalid names, refresh after restart, and delete without deleting tracks.

### FR-11 — Track deletion (US10)

- Confirm deletion, then remove library visibility, favourite, and playlist memberships atomically in MySQL; preserve remaining relative order.
- Update the device queue when deletion is known, stopping a deleted current entry and selecting the next paused.
- Delete cloud audio/artwork or retain retryable cleanup state. Do not re-import orphan objects automatically. Report database failure rather than claiming success.
- No new stream access after logical deletion. Already authorized/in-flight buffered bytes are subject to the documented stream policy.

Acceptance: deleted tracks disappear consistently; storage deletion failure leaves a visible cleanup record without resurrecting the track.

### FR-12 — Connectivity and recovery (US11)

- Use transient streaming buffers, not an offline audio library. Distinguish network, authorization, missing-object, unsupported-format, and quota errors.
- Proposed recovery: three attempts with increasing delay at last position while playback is requested. Explicit pause/logout/deletion cancels recovery.
- Android renews access natively when possible. Web verifies an expired cookie/session and asks for sign-in; restore context paused and require play interaction when necessary. Stop further unauthorized requests rather than looping.
- No offline mutation queue. Failed upload/favourite/playlist operations remain unsaved and retryable.

### FR-13 — No-charge quotas and operational persistence

- Enforce 5,000 tracks, 50 MiB/file, 100 files/batch and configured cloud storage/request/traffic budgets. Proposed initial object cap: 8 GB decimal, lowered below actual free allowance if necessary.
- Count artwork, partial uploads, objects pending deletion, retained versions, and applicable backups. An alert alone does not guarantee ₹0.
- MySQL uses transactional durable storage, private service access, and a documented consistent backup/restore procedure paired with object references.
- Provide installation/deployment/owner-provisioning instructions and preserve APK signing keys. No production data on ephemeral disks.

### FR-14 — Web streaming and browser lifecycle (US5–US7)

- Provide the shared player/queue/shuffle/repeat/favourites/playlist controls using one browser audio controller mounted outside route-level screens. Internal navigation must not stop audio or create a second player.
- Stream original audio through the same private byte-range API using the existing same-origin HttpOnly session cookie. No bearer secret in media URLs, no public object, and no complete-file Blob download before play/seek.
- User-initiated play/selection starts audio when permitted. Handle browser autoplay rejection with a paused/press-play state; restored/reloaded sessions do not autoplay. Browser queue advancement is required while the page runs normally; background/tab-suspension continuation is best effort, not the Android background guarantee.
- Browser reload restores a valid local queue/checkpoint paused; logout clears local listening state and stops current-tab audio. Expired cookies, network loss and missing media have clear bounded recovery/sign-in behavior.
- Propose optional Media Session controls where supported; absence must not prevent ordinary in-page playback. Closing/discarding a tab ends that tab's playback.

Acceptance: on recorded target browsers, sign in, play/seek CBR/VBR files, advance/reorder a queue, use shuffle/repeat, navigate routes while playing, reload paused, and verify blocked play/expired-session handling and anonymous denial. Android background acceptance remains separately mandatory.

## 4. Quality requirements

| ID | Requirement / measurement |
| --- | --- |
| NFR-01 | Public HTTPS; least-privilege cloud/database credentials kept off clients; protected session storage; no tokens or signed media URLs in logs. |
| NFR-02 | Proposed search rendering ≤2 seconds and playback start ≤5 seconds in 19/20 attempts, with 5,000 metadata records, 10 Mbps throughput, ~100 ms RTT, and awake backend. Record test path and cold-start limitations. |
| NFR-03 | Bound upload/parser/image/buffer memory and temporary disk usage. Streaming must not load a whole MP3 into server/client memory. |
| NFR-04 | Restart retains library/favourites/playlists and local paused listening state. Handle partial MySQL/object-storage success with reconciliation and cleanup. |
| NFR-05 | Errors have actionable messages without secrets; controls expose accessible labels and disabled/pressed states. |
| NFR-06 | One backend and one private MySQL service; no high-availability or multi-instance commitment. Free capacity and physical background behavior remain feasibility gates. |

## 5. Explicit exclusions

Offline downloads, transcoding, recommendations, multiple users, other formats/DRM, external cover/tag lookup, manual metadata editing, lyrics, folder scanning/sync, resumable/background uploads, offline mutations, social/shared libraries, collaborative/smart playlists, playlist import/export, live cross-device/cross-tab queue/position sync, iOS native support, casting/Android Auto/voice/Wear OS integration, gapless guarantees/crossfade/equalizer/replay gain/playback speed, public Play release, broad OEM certification, automatic disaster recovery, database replication, guaranteed browser background/closed-tab playback, PWA offline audio, and guaranteed survival after force stop/reboot/process kill. Web streaming is included.

## 6. Unresolved technical interpretation and release gates

- Exact installed CMF Phone 2 Pro OS/build, OEM battery behavior, and current compatible dependency versions must be measured during the experiment.
- Record the web browser matrix and test normal playback, autoplay rejection, authenticated ranges and local checkpoint storage separately from Android lifecycle behavior.
- Determine whether "displayed result set" means every match of the active filter or only loaded rows. Design proposal: snapshot all active-filter matches (up to 5,000), so pagination does not truncate listening. Record this interpretation before UI implementation.
- Exact no-charge Oracle availability, eligible VM/MySQL deployment, public HTTPS hostname acceptable to Google, and account request/traffic enforcement remain unverified.
- VBR seeking accuracy, malformed-tag fallback, and Android picker streaming must be demonstrated with representative real files.
- Proposed artwork limits, byte quota, session lifetimes, retry/checkpoint timings, playlist name limit, and performance thresholds are design defaults rather than additional confirmed owner answers.

Release acceptance is the applicable FR/NFR criteria plus spec section 10. Required background playback and a viable ₹0 public deployment cannot be replaced with assumptions. The next deliverables are the two software design documents, followed by the implementation plan.
