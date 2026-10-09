# Software Design Specification — Frontend and Android

Document: SDS-FE. Status: proposed design; not implemented.
Date: 2026-10-08. Requirements: [FRS.md](FRS.md), [spec.md](spec.md).
Canonical server contracts and MySQL schema: [SDS-BE.md](SDS-BE.md).

## 1. Design decision

Use one Ionic React codebase with platform-specific capabilities. Web and Android share sign-in, upload, library/search, player/queue/shuffle/repeat, favourites and playlists. Web streams through a browser audio adapter. Android packages bundled web assets in Capacitor and adds a thin native integration layer for Google sign-in, protected credentials, file-picker uploads, and a Media3 playback service.

On Android, the native service owns playback and the entire queue state machine; React presents/controls it and does not advance songs in the background. On web, a singleton browser controller owns one HTML audio element and queue state, independently of route components. Both adapters expose the same logical playback interface and product semantics. A single native authentication broker renews Android foreground/background requests; web media uses a same-origin session cookie. MySQL stores server data; queues remain local client snapshots.

Android's recommended service/session arrangement supports this ownership model. A candidate third-party audio plugin documents native playback but does not establish our required autonomous queue, request headers, and refresh behavior; evaluate it against those requirements before adopting it. Default design if it cannot meet them is a small app-owned Capacitor bridge around Media3, not a WebView audio fallback. [Android service guidance](https://developer.android.com/media/media3/session/background-playback), [candidate plugin API](https://github.com/mediagrid/capacitor-native-audio).

## 2. Modules and platform boundary

| Module | Responsibilities |
| --- | --- |
| App shell/routes | Ionic navigation, platform capability checks, login gating, persistent mini-player on web and Android. |
| Library | Paged rows, debounced literal search, favourites filter, artwork lifecycle, selection/context snapshot. |
| Upload manager | Selection, stable batch/item IDs, sequential file transfer, progress, status polling, retry/cancel. |
| Player/queue UI | Render authoritative selected-adapter snapshot and send explicit commands; show effective playback order. |
| Browser audio adapter | Singleton HTML audio element, JavaScript queue controller, cookie-authenticated source, play-policy/error handling, browser checkpoints. |
| Playlist editor | Server-confirmed order/name, revision-aware mutations, unsaved/error states. |
| API adapter | Typed SDS-BE requests/responses; browser cookies/CSRF or Android short-lived bearer; error translation. |
| Native auth broker | Credential Manager sign-in, backend token exchange, protected refresh secret, serialized renewal, logout. |
| Native playback service | Media3 player/session, authenticated data source, queue/shuffle/repeat, focus/route handling, checkpoints. |
| Native file bridge | Selected content URI inspection, bounded source streaming, foreground upload cancellation/progress. |

Use small feature modules and standard React state/context. A server-data cache library is optional; avoid a second playback store or a new state-management framework just for this MVP. Do not introduce a separate desktop app or Android-native library UI.

## 3. Screen and navigation contract

| Surface | Screens/actions |
| --- | --- |
| Both | Google sign-in, session error/retry, upload selection/status/results. |
| Both uploads | Signed-in upload dashboard, current limits/capacity, recent batch results, sign out. |
| Web and Android library | Library/search, favourites, artwork, row actions: play, play next, append, favourite, playlist add, delete. |
| Web and Android player | Full player and mini-player; elapsed/duration, buffering/error, seek, play/pause, previous/next, shuffle/repeat, favourite. |
| Web and Android queue | Current entry and effective upcoming order; remove/reorder, clear, play-next/append via track actions. |
| Web and Android playlists | List/create, detail/play from entry, rename/delete, add/remove/reorder tracks. |

Delete library tracks/playlists and clear/replace an active queue through explicit actions with suitable confirmation when destructive. Distinguish a transfer reaching 100% from a successfully imported track. Do not expose database/cloud implementation details in the normal listening flow.

## 4. Frontend models

IDs are opaque strings; byte counts and timestamps follow SDS-BE. Keep API DTOs separate from view-specific state.

| Model | Fields / invariants |
| --- | --- |
| TrackSummary | `id`, `title`, `artist`, `album`, nullable `durationMs`, `hasArtwork`, `isFavourite`; no cloud object key or credential. |
| SearchState | Trimmed query, favourite filter, page/limit, latest request ID, current rows/total; stale responses cannot replace newer query results. |
| Playlist | `id`, `name`, `revision`, ordered track IDs/summaries; duplicates forbidden. |
| UploadSelection | Stable `clientItemId`, filename, known source length, desktop File or private native handle, progress/status/error. |
| QueueEntry | Unique `entryId` per occurrence, `trackId`, small metadata snapshot; duplicate tracks have different entry IDs. |
| PlaybackSnapshot | `revision`, base/effective entry order, current entry, completed-cycle entries/history, position, known duration, shuffle/repeat, state/error, playback intent. |

Never persist Google ID tokens, native refresh secrets, access tokens, or media credentials inside React stores, localStorage, or queue JSON. The native broker can return a short-lived API bearer into memory for ordinary fetch requests; its refresh secret stays native. Browser sessions use HttpOnly cookies.

## 5. Authentication flows

### Desktop

1. Call the auth bootstrap contract to establish pre-login CSRF state and a short-lived Google nonce/challenge.
2. Google Identity Services obtains an ID token using the configured web client and nonce. Submit it with the challenge and CSRF proof.
3. The backend validates Google claims and the sole configured owner, then sets the secure app-session cookie. React stores only owner display state and a CSRF token.
4. Cookie-authenticated mutations include `X-CSRF-Token`; same-origin media GET/Range requests use the HttpOnly cookie without a JavaScript bearer. Session expiry shows sign-in. Account switches must be checked on the server, not inferred from the Google UI.
5. Browser logout stops/clears the current audio controller/checkpoint, revokes the backend session if reachable, and clears the cookie. It does not delete library/favourites/playlists. Other sessions/tabs are not a synchronized remote player.

### Android

1. Use Credential Manager Sign in with Google through a supported Capacitor wrapper or a small native bridge. Configure the Android package/signing certificate and backend/web client ID correctly.
2. Send the nonce-bound Google ID token to the backend token exchange. The backend issues its own short-lived bearer and renewable session.
3. Store the refresh secret using Android Keystore-backed encryption and app-private storage. Google UI is invoked only for interactive identity verification; background listening uses the app session.
4. Both UI/API and playback use one native renewal coordinator. On expiry/401, serialize one renewal, retry the original request once with the renewed credential, then expose `signInRequired` if renewal is no longer valid.
5. Logout first stops/clears playback, then revokes the backend session if reachable and clears local credentials/checkpoints. If offline, clear locally and record that server revocation could not be confirmed; no automatic replay of logout or mutation queue is added.

Credential Manager's documented Google-ID-token path still requires backend validation. [Android implementation guidance](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation), [Google backend verification](https://developers.google.com/identity/sign-in/android/backend-auth).

## 6. Playback adapters and interface

### Browser adapter

Use one persistent `HTMLAudioElement` (`preload="metadata"` proposed) outside Ionic route components. Set its source to the same-origin protected `/api/v1/tracks/{id}/stream`; do not fetch the full MP3 into a Blob or add an access secret to the URL. Observe actual media events, buffered/seekable ranges and `currentTime`; browser-decided range requests must pass the server experiment. The HTML media loading/playback API is the browser boundary, not the Android playback mechanism. [HTML media standard](https://html.spec.whatwg.org/multipage/media.html).

- Implement the logical operations below in a browser controller and expose the same snapshot model to the shared UI. Queue/shuffle/repeat semantics match the native implementation; use shared conformance fixtures to prevent drift, while keeping engines platform-specific.
- Route navigation subscribes/unsubscribes UI observers without replacing the controller/audio element. Each active tab has one controller and an independent queue; no global exclusive player or live cross-tab synchronization is promised.
- Start from owner play/selection actions. Check the `play()` promise and real events before showing playing state; rejection leaves paused state and a usable Play action. Source changes/automatic advancement may also encounter policy blocks. Never bypass policy with muted playback or an autoplay retry loop. [Chrome autoplay guidance](https://developer.chrome.com/blog/autoplay/).
- Drive natural completion from media `ended` events and the queue controller. Browser JavaScript is allowed for web playback; Android's native transitions must remain independent of it. Background/tab suspension is best effort and is not a substitute for required Android background playback.
- Browser media errors do not reliably expose HTTP status to JavaScript. Probe `/auth/me` on an authorization-suspected media failure, distinguish session expiry from connectivity, and stop retrying when sign-in is needed. Do not call the Android-only refresh endpoint. After interactive login, restore paused and let the owner resume.
- Save credential-free queue/position/mode snapshots in bounded browser-local storage every ten seconds and on pause/track/context changes, plus pagehide when available. Namespace by owner/origin/client instance; active tabs retain separate snapshots, and a new instance may restore the most recent checkpoint paused. Handle corrupt/unavailable/quota-limited storage without breaking server data or playing automatically.
- Offer browser Media Session metadata/actions when feature detection succeeds; handlers dispatch to the same controller. Basic in-page listening remains available without this enhancement. [Media Session specification](https://www.w3.org/TR/mediasession/).
- Before playback restoration, use track lookup to discard missing entries. Confirmed current-track deletion, logout, and explicit clear stop the browser element and cancel recovery. Closing/discarding the page ends its playback.

### Native adapter

Proposed native bridge contract, also the logical command/snapshot vocabulary for the web adapter; this is not an HTTP API and is not implemented here:

| Operation | Payload/result |
| --- | --- |
| `getSnapshot()` | Complete current service snapshot; attach to existing playback without starting another player. |
| `replaceQueue()` | Entries, selected entry, shuffle/repeat settings, `commandId`, expected revision; explicit replacement can start playback. |
| `play()` / `pause()` | Explicit intent; pause cancels pending recovery. |
| `seek()` | `positionMs`; clamp when duration is known; discard stale seeks after track replacement. |
| `next()` / `previous()` | Apply FRS boundary/three-second rules natively. |
| `insertNext()` / `append()` | Entries with unique occurrence IDs. |
| `removeEntry()` / `reorderUpcoming()` / `clear()` | Revision-aware edits; exact upcoming entry set required for reorder. |
| `setShuffle()` / `setRepeat()` | Preserve current audio; update effective order and checkpoint. |
| `reconcileTracks()` | Known deleted/valid IDs after refresh; stop deleted current entry and select next paused. |
| `playbackChanged` event | Monotonically increasing revision plus snapshot/state, error and track/position changes. |

On Android, use the service's serial command executor/main player thread to apply edits. One Media3 `Player` and one `MediaSession` exist per service. Notification, headset, and UI commands all enter the same state machine. Web serializes commands in its singleton controller. Register event listeners before reading the initial snapshot; ignore older snapshots/events by revision. Each adapter acknowledges applied commands so UI optimistic state cannot become a second authority.

### Queue algorithm

- Keep base/manual order, effective remaining order, current occurrence, completed occurrences for this cycle, and playback history. All operations identify entries, not just track IDs.
- On shuffle enable, randomize remaining entries and leave current unchanged. On disable, restore base/manual remaining order while retaining completed/current identities.
- Play next inserts after current in both effective and base/manual order. Reordering targets only the displayed remaining entries and changes the base order as well.
- On natural completion, apply repeat-one first; otherwise consume the next effective entry. Repeat-all starts a new cycle, avoiding the last occurrence first when alternatives exist. Explicit next bypasses repeat-one and follows the FRS end boundary.
- Removing current pauses and selects next at zero; removing upcoming leaves current untouched. Explicit context replacement creates new occurrence IDs and discards old cycle history.
- Restore from an atomic local snapshot into paused state after process death; a surviving playing service remains authoritative when the activity returns.

Neither platform uses a durable media cache or server-side queue. Android uses no HTML audio element or JavaScript end-of-track dependency; web deliberately uses its single browser audio element/controller.

## 7. Android streaming integration and system behavior

- Queue items use stable backend media endpoints with native `Authorization` headers. Do not put bearer tokens in URLs. The native data source supplies a current credential for each open/range request and renews after authorization failure without a JS callback.
- Validate ranges/seeks against the deployed proxy. Queue advancement and auth renewal must work while the WebView is suspended.
- Load protected artwork through an authenticated native HTTP client into a bounded bitmap cache, then publish media metadata to the session. Failure uses the placeholder and must not interrupt audio.
- Handle audio focus and route changes natively. Calls/permanent focus loss pause; manual pause prevents later automatic recovery. Route disconnection pauses. Notification/lock-screen controls reflect actual player state.
- Recovery uses three increasing-delay attempts at the last position while `playRequested` is true. Distinguish a one-time 401 refresh from general network retries; invalid session, deleted track, and invalid format are not infinite retry candidates.
- Checkpoint at least every ten seconds while playing plus pause, track/queue/mode changes. Use an app-private atomic file with schema version; credentials are stored separately. Sanitize invalid/corrupt snapshots rather than crashing.

## 8. Upload flow

Desktop uses an `XMLHttpRequest` raw-file PUT for transfer progress and cancellation. Android uses a native source stream for a selected content URI, avoiding a base64 copy through the JavaScript bridge. The bridge exposes opaque handles/progress, not arbitrary filesystem access.

1. Validate selection count and declared sizes; obtain actual source length where possible. For unknown Android length, spool one selected source to a bounded temporary file (≤50 MiB), measure it, and remove it after transfer/cancellation. This local upload staging is not an offline cloud download.
2. Create a batch manifest using stable batch/item identifiers, then submit each item's raw MP3 bytes according to SDS-BE. Initially transfer one file at a time per client; the server bounds aggregate concurrency.
3. Mark transfer complete only after server receipt acknowledgment (`202`). Poll batch/item status while the upload surface is active until imported/duplicate/failed. Processing can complete server-side after the client closes.
4. A network-lost acknowledgment is resolved by item-status lookup. Retry an incomplete transfer from zero using its documented retry operation, rather than blindly creating another item.
5. A backgrounded/closed Android upload surface cancels active foreground transfer; the independent playback service continues. Release URI grants/temporary staging when no longer needed. Do not promise arbitrary provider uploads survive process death.

Picker checks include filename with path-like characters, unavailable cloud-backed URI, unknown/misreported size, permission loss, cancellation, and selection exceeding 100 files. Backend validation remains authoritative.

## 9. Server data, search, and mutation handling

- Debounce typing approximately 250 ms, cancel/supersede old search requests, page 50 records by default, and render bounded rows/artwork. Empty/error/no-result states differ.
- Use the queue-context endpoint to get a consistent ordered listening snapshot. Proposed interpretation is all active-filter matches, not just the currently loaded page; this interpretation is flagged in FRS for review.
- Favourite PUT/DELETE is idempotent. Update displays only on server-confirmed success or clearly mark/revert optimistic changes on error.
- Playlist changes carry a server revision. A stale revision triggers refresh/reapply rather than overwriting another owner session's order. Reorder sends the complete ordered entry ID set.
- Deletion removes known queue entries immediately after confirmed logical server deletion. A later refresh reconciles other-session deletions. No push synchronization is required.
- Artwork in web views is fetched with the applicable credentials, converted to bounded object URLs as needed, and revoked on eviction/logout. Ordinary `<img>` URLs must not rely on missing Android bearer headers.

## 10. Error states and accessibility

Map SDS-BE error codes to short messages: sign-in required, access denied, offline/retry, invalid MP3, file too large, duplicate, quota reached, upload interrupted, track unavailable, or playlist changed elsewhere. Never expose raw provider errors or tokens.

Provide control labels, focusable actions, current/pressed mode state, accessible track text, visible transfer results, and disabled controls for empty contexts. Seeking should preview a requested position without claiming playback has reached it until the service confirms. Drag reorder needs an alternative move-up/down interaction.

## 11. Dependencies and verification

| Dependency | Decision / gate |
| --- | --- |
| Ionic React + compatible React/router | Pin a supported stable combination; do not assume the latest router major matches Ionic navigation. |
| Capacitor Android | Candidate major 8 with supported Node/Android Studio/SDK toolchain; verify every native integration on the selected versions. [Capacitor 8 requirements](https://capacitorjs.com/docs/updating/8-0). |
| Browser HTML media + optional Media Session | Web engine with cookie-based media access; record target browser versions, autoplay handling, range behavior and storage/lifecycle limitations. |
| Media3 ExoPlayer/session/data source | Primary Android playback engine; choose a maintained pinned stable release. |
| Credential Manager + Google ID library | Native interactive identity; app signing/package/backend audience are early setup dependencies. |
| Keystore / atomic app-private storage / system picker | Platform facilities; avoid a paid storage/file/audio plugin requirement. |
| HTTP/native bridge | Must stream source bytes, supply headers, cancel requests, and renew sessions outside JS. |

Meaningful checks for later implementation: API adapter/error contract checks; queue mode/boundary/duplicate-occurrence conformance against both engines; stale-command/lifecycle snapshots; cross-client uploads; web CBR/VBR seeking, cookie expiry, autoplay rejection, route continuity and reload restoration; and the physical-device cloud/background experiment. Browser/emulator playback does not prove Android background feasibility.

Open risks: autonomous refresh/queue support in any reused plugin, VBR seek accuracy on both engines, browser policy/storage/background differences, native artwork credentials, content URI behavior, signing-bound Google login, battery restrictions, and two playback adapters/custom bridge work exceeding the five-day window. Stop at the early gate rather than implementing a full UI on an unproven player.
