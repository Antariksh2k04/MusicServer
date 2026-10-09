# Repository Guidelines

## Project Structure & Module Organization

The local experiment lives alongside project context: `spec.md`, `FRS.md`, `SDS-FE.md`, `SDS-BE.md`, and `implementation_plan.md`. Production integrations remain pending; `plan.md` is absent.

- `src/web/`: Ionic React UI, browser audio controller, and Vitest tests beside source.
- `src/web/android/`: reviewed Capacitor Android project; generated assets/build outputs are ignored.
- `src/web/android/app/src/debug/`: local Media3 service/broker/plugin and loopback network allowance; excluded from release.
- `src/server/MusicServer/`: ASP.NET Core diagnostics, bounded streaming, and provider contracts/adapters under `Storage/`.
- `tests/server/MusicServer.Tests/`: xUnit HTTP integration and cancellation tests.
- `scripts/dev-server.ps1`: explicitly configured loopback startup; `.local/` holds ignored temporary media.
- `.github/workflows/android-shell.yml`: manual remote Android bootstrap; `docs/android-shell-build.md` explains its artifacts and installation check.

Production uploads/artwork belong in private object storage, never frontend assets.

## OpenSpec Governance

`openspec/specs/<capability>/spec.md` governs completed capabilities after synchronization/archive. For active changes under `openspec/changes/<change>/`, read `proposal.md` (scope), `specs/**/spec.md` (requirements/scenarios), `design.md` (decisions), and `tasks.md` (implementation status), alongside the baseline. No baseline exists yet; planning does not establish completed behavior.

Maintain detailed requirements in OpenSpec; preserve root documents as context. Surface conflicts and record resolutions in the change. Proposal approval alone does not authorize implementation.

Follow the active task groups for execution order: Oracle setup/integration is deferred to the final phase. Local storage evidence does not complete cloud acceptance; retain early physical Android investigation.

The owner cannot use USB with this laptop and confirmed shared Wi-Fi. Use paired wireless ADB with `adb -s <connection-endpoint> reverse tcp:5080 tcp:5080` for the local phone experiment; keep the diagnostic backend on loopback. See `docs/android-local-playback.md`; pairing and physical checks remain pending.

## Build, Test, and Development Commands

Run from the root; see `README.md` for prerequisites:

- `dotnet restore tests/server/MusicServer.Tests/MusicServer.Tests.csproj`: restore backend/test dependencies.
- `dotnet build tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore`: compile both projects.
- `dotnet test tests/server/MusicServer.Tests/MusicServer.Tests.csproj --no-restore`: backend checks.
- `npm --prefix src/web ci`: install locked frontend dependencies.
- `npm --prefix src/web run build` / `npm --prefix src/web test`: typecheck/build or Vitest.
- `node --test tests/scripts/prerequisites.test.cjs`: read-only prerequisite tool contracts.
- `./scripts/check-android-toolchain.ps1` / `node --test tests/scripts/android-toolchain.test.cjs`: native tool inventory/contracts; no APK build or phone verification.
- `npm --prefix src/web run build:native-shell` / `node --test tests/scripts/android-bootstrap.test.cjs`: isolated shell assets/bootstrap contracts; no native playback evidence.
- `npm --prefix src/web run android:sync`: build shell assets and sync the existing native project; never regenerate it with `cap add`.
- From `src/web/android`, `./gradlew.bat testDebugUnitTest assembleDebug assembleDebugAndroidTest assembleRelease`: compile native variants/tests with installed tooling. Physical checks follow `docs/android-local-playback.md`.
- `./scripts/dev-server.ps1` and `npm --prefix src/web run dev`: start backend and frontend in separate terminals.

No lint command is configured. Never report unexecuted checks as passed.

## Coding Style & Naming Conventions

Use two spaces for TypeScript/TSX; four for C#/Kotlin. Use PascalCase components/types, camelCase variables, and C# Async suffixes. Parameterize SQL; keep transactions short. TypeScript is strict; .NET warnings are errors. Formatters are not configured.

## Testing Guidelines

Use Vitest and xUnit; no coverage percentage is required. Name tests by behavior, such as `ExpiredSession_ReturnsUnauthorized`. Prioritize authorization, upload failures, ranges, cancellation, and player races. Simulated events cannot verify audible seeking; test Android background playback on the physical CMF Phone 2 Pro.

## Commit & Pull Request Guidelines

Read and follow [gitworkflow.md](gitworkflow.md) before every use of the GitHub MCP integration and before Git operations. It governs branch naming, Conventional Commits, checks, and pull requests. Its GitLab issue examples also apply to GitHub issue numbers; use `frontend`, `backend`, or `fullstack` for this repository. Keep read-only GitHub inspection read-only; branch preparation applies when making changes. Do not push directly to `main` or `dev`.

PRs describe behavior, requirement IDs, validation, and limitations; link issues and include UI screenshots when applicable.

After the prototype phase, always create a GitHub issue in `Antariksh2k04/MusicServer` before starting each product implementation task, or reuse an existing issue covering that task. Record its scope, links to governing OpenSpec requirements/tasks, and validation checklist; keep detailed requirements in OpenSpec. Include the issue number in the branch name, link it in the PR, and close it when its acceptance criteria are verified and the change is merged.

## Security & Scope

Keep credentials, signing keys, tokens, and personal media out of source control and logs. Diagnostic authentication/storage are Development-only substitutes and never establish production acceptance. Preserve one Google owner, MySQL, private storage, web streaming, native Android background playback, and the ₹0 budget. Documentation-only requests must not scaffold or provision applications.
