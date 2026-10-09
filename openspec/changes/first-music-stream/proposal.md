# Proposal

## Why

The local browser/backend streaming experiment is running, but production integrations and Android delivery remain pending. A small protected upload-to-playback path will establish authentication, seeking, and physical Android background feasibility locally, followed by cloud validation in the final phase, before completing the broader MVP.

## What Changes

- Introduce Google sign-in for the configured sole owner, persistent browser/native app sessions, logout, and consistent anonymous/non-owner rejection.
- Allow one selected MP3 per submission from Ionic web and Android, store original bytes privately, and expose transfer/import outcomes and explicit retry. Preserve identical-byte deduplication and failure cleanup.
- Persist embedded text metadata and duration with fallbacks in MySQL and list committed tracks. One file per submission does not mean a one-track library.
- Stream the selected track through authenticated byte-range delivery with play/pause and working seeking in both clients.
- Provide native Android background playback, system play/pause/seek, audio-focus handling, route-disconnection pause, and background credential renewal. The owner explicitly confirmed web plus Android, including background playback, for this change.
- Update `AGENTS.md` to distinguish active OpenSpec change artifacts from completed capability specs. Preserve root planning documents as project context.

This slice excludes batch selection, library search, artwork extraction, queue editing/advancement, shuffle/repeat, favourites, playlists, and user-facing track deletion. Those remain later MVP work. Offline downloads, transcoding, recommendations, and multiple users remain excluded from the release. Single-track background acceptance here does not satisfy the later full-MVP multi-track transition gate.

## Capabilities

### New Capabilities

- `owner-auth`: Google owner identity, persistent app sessions, protected access, renewal, and logout on web and Android.
- `mp3-import`: Single-file submission, private original-byte storage, validation, deduplication, import results, and recoverable failure handling.
- `track-library`: Durable extracted track metadata, defined fallbacks, and owner-only listing of committed imports.
- `audio-playback`: Authenticated original-byte streaming, seeking, single-track client controls, and native Android background listening.

### Modified Capabilities

None. The capability inventory is empty; existing root documents are context, not OpenSpec capability baselines.

## Impact

Build-location update approved by the owner on 2026-10-09: use a manually dispatched GitHub Actions Android bootstrap instead of requiring Android Studio/JDK/SDK installation on this PC. Generate the official pinned Capacitor template and genuine npm lockfile on the runner, return them with the debug APK for review, and incorporate the reviewed native sources before service development. The physical phone, installation evidence, and local USB/adb transport remain independent requirements. A prepared workflow or successful compilation does not prove background playback. Check the account's free allowance and block paid usage before dispatch; retain the ₹0 budget.

Execution update approved by the owner on 2026-10-08: defer Oracle account checks, provisioning, OCI integration, public deployment, and real-cloud validation to the final phase. Complete the Android toolchain/shell and local native background experiment early, then develop the slice against local MySQL and a Development-only private filesystem storage adapter behind the production storage boundary. Oracle signup is not a prerequisite for this local work. Local evidence does not satisfy real-cloud acceptance; Google identity, durable MySQL, and physical Android results each still require their actual dependencies and tests.

The existing local implementation lives under `src/web/`, `src/server/`, and `tests/`. Pending integrations include Capacitor/Media3, Google identity, durable MySQL, and private OCI object storage. The production architecture retains same-origin browser cookies, native renewable bearer sessions, and authenticated range delivery; a local storage adapter is a development substitute, not a deployment option.

`plan.md` is missing. Use the existing `implementation_plan.md` with `spec.md`, `FRS.md`, and the two SDS documents as context; do not create a second root plan. Carry their MySQL/private-range-proxy architecture into the change. Detailed active requirements belong only in this change's delta specs; design and tasks reference them.

Implementation gates remain open: owner/client identity provisioning, exact phone/browser builds, native bridge compatibility, and CBR/VBR seek behavior; the final phase additionally requires eligible Oracle resources and enforceable ₹0 usage plus a no-purchase trusted public HTTPS hostname accepted by Google. Deferring Oracle accepts later discovery of cloud capacity, compatibility, or budget problems and possible rework. All release requirements remain in force. Proposed root defaults remain defaults in `design.md`; public media, paid hosting, SQLite, and foreground-only Android are not automatic fallbacks.
