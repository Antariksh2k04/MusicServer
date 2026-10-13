# Owner provisioning implementation

Issue: [#8](https://github.com/Antariksh2k04/MusicServer/issues/8). Branch: `feature/backend/8-owner-provisioning`, based on main after PR #7 merged. Governing scope: task 3.2, design section 2, and `owner-auth`. Root planning documents remain context.

## Scope and boundaries

Application defines Google configuration, verified identity/store contracts, the provisioning use case, and owner readiness. Infrastructure wraps Google.Apis.Auth 1.69.0 from the available cache and implements parameterized EF owner reads/insertion. WebApi exposes only private command-line operator actions and integrates the negative owner-readiness gate. No public registration route or app session issuance was added.

The operator command verifies Google's signed token for the configured web/backend audience, validates nonce/expiry/issuer/subject, canonicalizes the issuer, and inserts owner ID 1. Matching repeat provisioning leaves the existing record unchanged; a different identity is rejected. The database singleton key handles competing operator inserts; exact post-conflict identity comparison prevents replacement. Runtime lookup uses a separate connection and refuses provisioning through its store.

`scripts/provision-owner.ps1` checks public configuration before secret prompts and privately passes the token/connection via process environment, restoring previous values on success/failure. `docs/owner-provisioning.md` records Google client, package/signing/origin configuration and the prerequisite nonce-bound proof acquisition. The retained Android identity is `com.musicserver.shell`; no Android/frontend source or signing key was changed.

## Validation and pending dependencies

The solution builds with zero warnings/errors. Focused backend checks passed for rejected proof/configuration, exact owner matching, idempotency, runtime write rejection, missing-owner HTTP readiness, absent registration routes, bounded/cancelled verifier input, validated-payload claim checks, and redacted SDK/provider errors. These use isolated stores and a test-only SDK delegate; they do not establish real Google signature/network or MySQL provisioning evidence.

The final backend run, `dotnet test MusicServerBackend/MusicServer.Tests/MusicServer.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName!~MySqlSchemaTests'`, passed all 132 selected tests with zero failures/skips. The separate MySQL acceptance test was explicitly excluded under the owner's instruction. No frontend/native source changed, so those builds were not repeated for this backend ticket.

The PowerShell helper contract check passed for hidden prompts, quoted delimiter-containing dummy passwords, fixed database target, secret-free command arguments, config-before-proof sequencing, and environment restoration after successful or failed provisioning. Existing local database helper contracts and offline migration artifact verification passed. Strict OpenSpec validation and whitespace checks passed. Cached restore used command-scoped `NuGetAudit=false`; no successful vulnerability audit is claimed.

The owner explicitly directed development to continue while skipping remaining database acceptance tests. They were excluded from this ticket's automated run and remain unverified under 3.1/#5. Existing owner-reported fresh/repeated migration evidence is retained. No database credentials were used by the agent, no owner row was written, and no MySQL restart or interrupted-DDL drill ran.

The owner confirmed that Google OAuth clients have not been created yet. Actual client IDs, Google origin registration, a preserved APK signing identity/fingerprint, and a nonce-bound Google proof have not been supplied. Task 3.2 remains unchecked until actual configuration/provisioning evidence is available. Owner readiness fails closed when absent; public readiness remains 503 until the later product session/import/storage modules are present. Tasks 3.3–3.6 deliver normal client sign-in, public nonce lifecycle, and durable sessions.
