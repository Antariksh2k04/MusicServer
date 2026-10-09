# Owner Authentication Delta

## Purpose

Allow exactly one configured Google identity to access personal music through persistent, revocable web and Android app sessions.

## ADDED Requirements

### Requirement: Sole configured owner sign-in
The system SHALL verify Google's identity proof and authorize only the preconfigured immutable issuer/subject. It MUST reject invalid or replayed proofs and MUST NOT grant ownership to an arbitrary first sign-in. Android sign-in MUST use a supported native or system-browser flow outside the embedded WebView.

#### Scenario: Owner signs in on either client
- **WHEN** the configured owner completes Google sign-in on web or Android with a valid nonce-bound proof
- **THEN** the system establishes an app session and displays the protected library

#### Scenario: Other verified Google identity
- **WHEN** a valid Google proof identifies another subject
- **THEN** sign-in returns `403 ownerForbidden` without an owner session or library information

#### Scenario: Invalid or replayed proof
- **WHEN** signature, issuer, audience, expiry, or nonce validation fails, or a challenge is reused
- **THEN** sign-in is rejected without establishing a session

#### Scenario: Cancelled or unavailable sign-in
- **WHEN** the owner cancels Google sign-in or the provider/network is unavailable
- **THEN** the client remains signed out and offers an appropriate retry without claiming an authorization failure for a network outage

### Requirement: Persistent bounded app sessions
The system SHALL preserve valid app sessions across client and backend restarts, protect persisted credentials, and enforce expiry and revocation. Android SHALL renew permitted app access without foregrounding Ionic or prompting Google. Browser session expiry SHALL require interactive sign-in and leave restored playback paused.

#### Scenario: Restart with valid session
- **WHEN** the client or backend restarts while the saved session remains valid
- **THEN** the owner retains access without a new Google prompt and client restart does not autoplay

#### Scenario: Background Android renewal
- **WHEN** Android needs a new media request after access expiry while the renewable session is valid and Ionic is inactive
- **THEN** credentials renew natively and the requested playback operation can continue

#### Scenario: Session can no longer renew
- **WHEN** a session is revoked or its absolute lifetime expires, or Android refresh is rejected
- **THEN** new protected requests are denied and playback recovery stops with sign-in required

### Requirement: Every protected operation requires owner authority
Library, upload status/content/retry, and every new stream/range request SHALL require valid owner app-session authority. Anonymous or expired-session API requests SHALL return `401 signInRequired` without login redirects. Browser mutations SHALL require valid CSRF protection. Google tokens alone MUST NOT authorize music API requests.

#### Scenario: Anonymous protected requests
- **WHEN** an anonymous client requests library metadata, upload content/status, or any full/range audio response
- **THEN** the request returns `401` without private data or a cloud read/write

#### Scenario: Missing browser CSRF proof
- **WHEN** a cookie-authenticated client submits an upload mutation without valid CSRF proof
- **THEN** the mutation is rejected without changing import or storage state

#### Scenario: Google token used as app bearer
- **WHEN** a client sends a Google identity/access token in place of an app session
- **THEN** the protected request is rejected

### Requirement: Logout revokes local playback and app authority
Logout SHALL stop playback, cancel retries, and clear local credentials and selected-track restoration data. Reachable backend logout SHALL revoke that session and clear the browser cookie. Server tracks SHALL remain. Offline logout SHALL clear Android local credentials and state while accurately reporting unconfirmed server revocation.

#### Scenario: Online logout
- **WHEN** the owner signs out during playback
- **THEN** local playback stops, restoration data is cleared, and the revoked session cannot obtain new protected responses
- **AND** signing in again still lists previously committed tracks

#### Scenario: Offline Android logout
- **WHEN** Android signs out without reaching the backend
- **THEN** local playback and credential use stop and the client does not claim server revocation succeeded
