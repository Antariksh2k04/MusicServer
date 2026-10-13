# Google owner provisioning

Task 3.2 of `first-music-stream` governs this operator setup; its `owner-auth` delta owns the requirements. Ordinary browser/native login and persistent app sessions are subsequent tasks. Provisioning never issues an app session or starts an HTTP registration endpoint.

## Client configuration

Create Google OAuth clients in your own project. Configure the consent screen for your personal account and only the sign-in scopes required by Google Identity Services. Create a **Web application** client for the actual HTTPS browser origin. Create an **Android** client for the existing package `com.musicserver.shell` and the SHA-1 of the certificate that signs the installed APK. Native Credential Manager requests ID tokens for the **web/backend client ID**, rather than using the Android client ID as the server audience.

Keep the signing key outside source control and reuse it for subsequent private APKs. Obtain its SHA-1 with `keytool -list -v -keystore <private-key-path> -alias <alias>` and enter the password interactively. A temporary runner debug certificate does not establish the final signing identity. HTTPS development setup and the public hostname remain separate configuration work; this ticket does not turn the HTTP diagnostic server into production authentication.

Public configuration uses these process environment names:

| Variable | Value |
| --- | --- |
| `Auth__Google__WebClientId` | Actual web OAuth client ID ending in `.apps.googleusercontent.com` |
| `Auth__Google__AndroidClientId` | Actual Android OAuth client ID; different from the web ID |
| `Auth__Google__BrowserOrigin` | Exact HTTPS origin, such as `https://localhost:5173`, without path/trailing slash |
| `Auth__Google__AndroidPackage` | `com.musicserver.shell` |
| `Auth__Google__AndroidCertificateSha1` | Twenty hexadecimal byte pairs separated by colons |
| `Auth__Google__NativeOrigin` | `https://localhost`, matching the retained Capacitor Android origin |

The provisioning command requires the web ID and browser origin. Owner readiness also requires the Android configuration. These checks verify shape and the repository's package/origin, not the existence of registrations in Google Console or the signing certificate inside an APK. Actual client/signing evidence must be recorded separately, without identifiers or credentials in committed evidence. No OAuth client secret is required by this ID-token verification command; do not put one in configuration.

## Operator procedure

Build from the repository root:

```powershell
dotnet build MusicServerBackend/MusicServer.sln --no-restore
dotnet run --project MusicServerBackend/MusicServer.WebApi --no-build --no-restore -- owner configuration
dotnet run --project MusicServerBackend/MusicServer.WebApi --no-build --no-restore -- owner nonce
```

The last command generates a random 256-bit nonce, encoded as 43 base64url characters. Use it in a fresh Google Identity Services or native Credential Manager request with the configured web/backend audience. Obtain the resulting ID token locally through that configured Google flow. The app's ordinary sign-in UI is not delivered in this ticket; real provisioning requires this operator proof-acquisition prerequisite. A token from an unrelated client, an access token, decoded claims, or a hand-entered email/subject is insufficient.

Use the fresh token immediately and run:

```powershell
./scripts/provision-owner.ps1 -Nonce <the-generated-nonce>
```

The helper first checks client configuration, then prompts for the local MySQL password and ID token using hidden prompts. It targets `music_server_db`, writes no credential file, uses process environment rather than secret command arguments, and restores the prior environment on exit. An operator/migration identity must have rights to insert the singleton owner row; `-UserName <operator-user>` selects that identity. The target schema must already be migrated. The command does not apply migrations.

The Google SDK validates signature, Google's issuers, the configured audience, and expiry using its certificate cache. The wrapper checks nonce and required claims, canonicalizes Google's two accepted issuer spellings to `https://accounts.google.com`, and uses immutable `sub` for ownership. Email is never an ownership key. The verified identity is saved as owner ID 1 with UTC creation time; no ID token is stored. A repeated matching identity returns `AlreadyConfigured` without updating names or ownership. Another identity returns `ownerAlreadyConfigured`; changing owners is outside this release.

To inspect owner existence privately, set `ConnectionStrings__MusicServerMigration` using the hidden connection prompt described in `database-migrations.md`, then run `dotnet run --project MusicServerBackend/MusicServer.WebApi --no-build --no-restore -- owner status`. Output reports presence only, without issuer/subject/name/token. For direct operator automation, `owner provision` reads `MUSICSERVER_OWNER_ID_TOKEN` and `MUSICSERVER_OWNER_NONCE` from its environment; prefer the helper so secrets are cleared after use.

The operator nonce binds the proof supplied by the privileged caller; this command is not the public one-use challenge protocol. That protocol, its TTL/replay protection, CSRF, and app-session issuance belong to tasks 3.3–3.6. Do not pass ID tokens into logs, media URLs, or app-bearer headers.

## Runtime boundary and verification

Runtime owner lookup uses `ConnectionStrings__MusicServer` independently of migration credentials. Use a restricted identity with SELECT access to `owners`; runtime provisioning is rejected even if broader database privileges are accidentally granted. Missing/invalid Google configuration, missing owner, or database failure causes owner readiness to fail. Cancellation is propagated. Public readiness remains 503 until the later app-session/import/storage modules are implemented as well; a configured owner alone cannot mark the slice ready. No startup migration or owner creation occurs.

Use `powershell -NoProfile -File tests/scripts/provision-owner.test.ps1` for the helper contract check. Backend tests cover proof/configuration rejection, exact owner comparison, idempotency, runtime write rejection, missing-owner readiness, absent registration endpoints, and safe errors. Provider tests use isolated validated-payload doubles and do not establish a real Google exchange or MySQL write. Live Google proof, actual signing/origin registration, and durable provisioning remain dependent on the owner's external configuration.

Primary references: [Google ID-token verification](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token), [Google OpenID Connect](https://developers.google.com/identity/openid-connect/openid-connect), [Android Credential Manager](https://developers.google.com/identity/android-credential-manager), and [Google .NET validation settings](https://googleapis.dev/dotnet/Google.Apis.Auth/latest/api/Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings.html).
