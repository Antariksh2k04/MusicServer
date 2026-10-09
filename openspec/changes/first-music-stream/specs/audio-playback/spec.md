# Audio Playback Delta

## Purpose

Let the owner stream and seek within a selected private MP3 in Ionic web and Android, including required single-track native background listening.

## ADDED Requirements

### Requirement: Protected original-byte range delivery
Authorized media requests SHALL return original MP3 bytes with `audio/mpeg`, known length, stable content ETag, and byte-range support. Full GET SHALL return `200`; a satisfiable single range SHALL return `206` with matching bytes/length/Content-Range. HEAD SHALL return full representation headers without a body. Streaming SHALL use bounded memory and cancel upstream work when the client disconnects.

#### Scenario: Full or HEAD request
- **WHEN** the owner requests a full track GET or HEAD
- **THEN** the response returns full representation length, ETag, and `Accept-Ranges: bytes`
- **AND** GET streams original bytes while HEAD sends no body

#### Scenario: Single closed, open-ended, or suffix range
- **WHEN** the owner requests a satisfiable single closed, open-ended, or suffix byte range
- **THEN** the response returns `206` with exactly the selected original bytes, selected length, and correct inclusive Content-Range

#### Scenario: Invalid or unsatisfiable range
- **WHEN** the owner requests a malformed or unsatisfiable byte range
- **THEN** the endpoint returns `416` with `Content-Range: bytes */{length}` without reading cloud audio

#### Scenario: Multiple ranges
- **WHEN** a request contains multiple byte ranges
- **THEN** the endpoint returns `400 multipleRangesUnsupported` rather than an incorrect partial response

#### Scenario: If-Range validation
- **WHEN** an If-Range content ETag matches
- **THEN** the requested range is served
- **AND** a nonmatching If-Range results in a full `200` response

#### Scenario: Cancelled stream
- **WHEN** the player disconnects before consuming the response
- **THEN** upstream reading is cancelled and resources are released without buffering the complete file

### Requirement: One selected-track player with accurate seeking
Both clients SHALL offer play/pause, current metadata, elapsed/known duration, buffering/error state, and forward/backward seek for the selected track. Pause SHALL preserve position; seeks SHALL clamp to known bounds. Seeking SHALL work without complete-file-first retrieval. One engine per client SHALL emit audio, and rapid controls SHALL not allow stale selection/seek results to replace newer intent.

#### Scenario: Play, pause, and seek
- **WHEN** the owner starts a supported track, pauses/resumes, and seeks to about 25%, 75%, then backward
- **THEN** playback reflects those actions, pause preserves position, and range requests permit seeking before the full track has downloaded

#### Scenario: Representative CBR and indexed VBR accuracy
- **WHEN** seek acceptance is tested with CBR and Xing/VBRI-indexed VBR fixtures of known duration
- **THEN** audible playback resumes within two seconds of each requested target on both clients
- **AND** results for representative unindexed VBR are recorded; an unindexed seek failure remains unresolved rather than being reported as support

#### Scenario: Rapid selection and seek
- **WHEN** the owner rapidly changes the selected track or seek position
- **THEN** the latest intended selection/position wins and overlapping players do not emit audio

### Requirement: Browser playback follows interaction and session state
Browser playback SHALL retain one player across Ionic route changes, respect user-interaction policies, and restore the saved selected track/position paused after reload. It MUST NOT put credentials in media URLs or retrieve a complete audio Blob before play. Expired browser sessions SHALL stop new media requests and require sign-in; restored context after sign-in SHALL remain paused.

#### Scenario: Browser refuses play
- **WHEN** the browser rejects a play request under its interaction policy
- **THEN** the UI shows paused/press-play state without a retry loop or false playing status

#### Scenario: Route change and reload
- **WHEN** the owner navigates between Ionic routes during playback
- **THEN** the same player continues
- **AND** a later page reload restores the saved selection/position without autoplay

### Requirement: Native single-track Android background playback
Android SHALL continue selected-track playback while the screen is locked or another app is active for at least 15 minutes under normal battery settings on the CMF Phone 2 Pro. Playback and permitted credential renewal SHALL remain independent of Ionic JavaScript. System play/pause and seek where exposed, plus Bluetooth/headset play/pause, SHALL control the same player and show current title/artist.

#### Scenario: Screen lock and app switching
- **WHEN** a sufficiently long track plays with the phone locked or another app foregrounded for at least 15 minutes and Ionic callbacks inactive
- **THEN** audible playback continues and reopening Ionic attaches to the existing player without duplication

#### Scenario: System and accessory controls
- **WHEN** the owner uses notification/lock-screen play/pause or exposed seek controls, or Bluetooth/headset play/pause
- **THEN** the native player and Ionic's reattached state reflect those commands and current track metadata

#### Scenario: Audio focus and output disconnection
- **WHEN** a call or competing audio interrupts playback, or headphones/Bluetooth disconnect
- **THEN** the player respects audio focus and pauses on output disconnection
- **AND** explicit user pause is preserved; a call or permanent focus loss requires manual resume

#### Scenario: Process restart
- **WHEN** the Android process has terminated and the owner relaunches it
- **THEN** saved selection/position restores paused
- **AND** uninterrupted survival of force stop, reboot, or process termination is not promised

### Requirement: Bounded media failure recovery
Missing audio SHALL return `404 trackUnavailable`, cloud unavailability SHALL return a safe `503`, and expired/revoked app authority SHALL deny new media requests. Recoverable network failures SHALL use at most three delayed retries at the last position while play remains requested. Pause/logout/new selection SHALL cancel stale recovery. Unrecoverable authorization SHALL require sign-in without endless background retries.

#### Scenario: Expired credential on seek
- **WHEN** a new seek request occurs after app access expires
- **THEN** Android renews once and reopens when permitted, while an expired browser session requires sign-in and paused restoration
- **AND** revoked or nonrenewable authority receives no new cloud audio

#### Scenario: Missing object or unavailable cloud
- **WHEN** the referenced object is missing or cloud access fails
- **THEN** the player shows a distinguishable unavailable/retry state without corrupting saved metadata

#### Scenario: Network returns or retries are exhausted
- **WHEN** connectivity returns within the retry budget and playback is still requested
- **THEN** the player can recover at the last position
- **AND** exhaustion leaves an explicit retry action; pause/logout/new selection prevents a stale automatic restart
