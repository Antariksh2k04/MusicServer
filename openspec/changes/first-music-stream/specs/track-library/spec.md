# Track Library Delta

## Purpose

Give the owner a durable list of successfully imported tracks with recognizable embedded metadata and safe fallbacks for missing tags.

## ADDED Requirements

### Requirement: Embedded text metadata and duration
The system SHALL extract available title, artist, album, album artist, track/disc numbers, and duration without external lookup or manual editing. Missing title SHALL use the filename without extension; missing artist/album SHALL display `Unknown artist` / `Unknown album`. Unknown duration SHALL remain null. Unicode SHALL be preserved and metadata SHALL render as text.

#### Scenario: Tagged MP3
- **WHEN** a supported MP3 contains readable embedded metadata and duration
- **THEN** the committed track preserves those values, including non-Latin text, and displays available duration

#### Scenario: Missing or unreadable optional tags
- **WHEN** valid audio has missing or unreadable optional tags
- **THEN** the import succeeds with defined title/artist/album fallbacks and absent optional values
- **AND** an unavailable duration is not fabricated as zero

#### Scenario: Markup-like metadata
- **WHEN** embedded metadata contains HTML/script-like text
- **THEN** the library displays text without executing markup or scripts

### Requirement: Owner-only committed library listing
The library SHALL list only successfully committed tracks in stable title/artist/id order, with track identity, title, artist, album, and nullable duration. It SHALL support bounded pagination and refresh after import. Empty, loading, and failed-list states SHALL be distinguishable. No cloud object key, local staging path, or credential SHALL appear in metadata responses.

#### Scenario: Empty library
- **WHEN** the owner lists a library with no committed tracks
- **THEN** the response contains an empty list and zero total and the client displays an empty state

#### Scenario: Import becomes visible
- **WHEN** an import commits and the owner refreshes either client
- **THEN** the new track appears once and can be selected for playback
- **AND** staged, processing, failed, and uncertain imports do not appear as playable tracks

#### Scenario: Cross-client access
- **WHEN** a track imported from web is listed on Android, or an Android import is listed on web
- **THEN** both clients receive the same committed metadata and track identity

#### Scenario: Listing failure
- **WHEN** a library request fails because of connectivity or backend unavailability
- **THEN** the client reports failure and offers refresh rather than claiming the library is empty

### Requirement: Durable metadata persistence
Committed track metadata and private audio references SHALL persist in MySQL on durable storage across backend/database restarts and owner sign-out/sign-in. The system SHALL NOT store full audio in the relational database or use SQLite for this capability.

#### Scenario: Backend and database restart
- **WHEN** the backend and MySQL restart after a successful import
- **THEN** the owner can list the same track metadata and obtain authorized access to its referenced audio

#### Scenario: Owner signs in again
- **WHEN** the owner signs out and subsequently signs in again
- **THEN** committed tracks remain in the server library
