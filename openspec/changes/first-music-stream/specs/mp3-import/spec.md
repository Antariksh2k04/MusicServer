# MP3 Import Delta

## Purpose

Import a single owner-selected MP3 from web or Android into private cloud storage with visible outcomes and durable recovery from partial failure.

## ADDED Requirements

### Requirement: Single-file submission from either client
The owner SHALL select and submit one MP3 per submission on web and Android, see byte-transfer progress, and distinguish receiving/processing from imported/duplicate/failed. Android SHALL accept system-picker files without assuming a local filesystem path. Cancelling selection SHALL change no library state. Background upload continuation is not guaranteed.

#### Scenario: Successful import on either client
- **WHEN** the owner submits one valid MP3 from a browser file selection or Android picker
- **THEN** the client shows transfer progress, processing, and a terminal imported result with a track identity
- **AND** transfer completion alone does not label the track imported

#### Scenario: Picker cancelled
- **WHEN** the owner cancels file selection before submission
- **THEN** no import is started and the library is unchanged

#### Scenario: One file per submission
- **WHEN** a submission contains zero files or more than one file
- **THEN** the server rejects the manifest without accepting content or writing cloud objects

### Requirement: Bounded MP3 validation
Imports SHALL reject empty files, files larger than 50 MiB (52,428,800 bytes), incomplete transfers, and non-MP3 content regardless of extension or MIME. Valid representative CBR and VBR MP3s SHALL be accepted without conversion. Invalid optional tags SHALL NOT invalidate otherwise supported audio.

#### Scenario: Invalid or oversized input
- **WHEN** a file is empty, oversized, or renamed non-MP3 content
- **THEN** the client receives an actionable validation outcome and no playable track is created

#### Scenario: Truncated transfer
- **WHEN** a transfer disconnects or its actual byte count differs from the declared count
- **THEN** the import fails as incomplete and its partial staging is eventually removed
- **AND** retry starts the transfer from byte zero

#### Scenario: CBR and VBR input
- **WHEN** representative valid constant- or variable-bitrate MP3s are submitted
- **THEN** each can become an imported track without changing its original bytes

### Requirement: Private original-byte storage with admission limits
The system SHALL store unchanged MP3 bytes in private cloud objects and keep cloud credentials/object keys out of client responses and logs. Imports SHALL reserve capacity before cloud writes and enforce the 5,000-track cap and deployment's verified free-storage/request limits, including pending or uncertain writes. A quota failure SHALL preserve existing tracks.

#### Scenario: Original bytes remain private
- **WHEN** a valid import commits
- **THEN** stored audio matches the submitted bytes and anonymous object access is denied
- **AND** the result contains a track identity rather than cloud credentials or a public object URL

#### Scenario: Capacity unavailable
- **WHEN** a new import would exceed configured storage, track, or cloud-operation admission limits
- **THEN** no new cloud write is started, the import reports capacity failure, and existing tracks remain intact

### Requirement: Identical-byte deduplication and idempotent submissions
Identical file bytes SHALL produce one library track, including concurrent imports and retried lost responses. A confirmed duplicate SHALL return a duplicate outcome referring to the existing track. Reusing a submission identity with changed input SHALL be rejected. Same filenames/tags with different bytes SHALL NOT be merged.

#### Scenario: Same bytes submitted twice or concurrently
- **WHEN** two submissions contain identical bytes
- **THEN** at most one track is committed and the other completes as a duplicate once that track exists

#### Scenario: Lost acknowledgement
- **WHEN** the owner repeats an unchanged manifest/content request after losing its response
- **THEN** the same import outcome is recovered without creating another track

#### Scenario: Changed input identity
- **WHEN** a submission identifier is reused with a different manifest
- **THEN** the request returns an idempotency conflict without overwriting the earlier import

### Requirement: Durable failed-import recovery
An import SHALL become visible only after both audio storage and metadata commit succeed. Failed or uncertain writes SHALL retain durable cleanup/accounting until their outcome is confirmed, including after restart. The client SHALL display failure and offer explicit retry when cleanup/admission permits; successful existing tracks SHALL remain available.

#### Scenario: Cloud write or database commit fails
- **WHEN** cloud upload fails or metadata commit fails after an object write
- **THEN** no incomplete track appears in the library and the import retains a safe failure/recovery state
- **AND** any possible orphan object remains tracked for reconciliation and cleanup

#### Scenario: Cloud write acknowledgement is lost
- **WHEN** a cloud PUT times out after it might have stored bytes
- **THEN** the system confirms the object's outcome before freeing reserved capacity or retrying the import

#### Scenario: Backend restarts during import
- **WHEN** the backend restarts with incomplete import or cleanup work
- **THEN** durable state is reconciled without exposing a phantom track or forgetting outstanding cloud usage

#### Scenario: Explicit retry
- **WHEN** the owner retries a failed item after cleanup and admission permit a new attempt
- **THEN** the client can retransmit from zero and observe its new attempt's outcome without deleting other tracks
