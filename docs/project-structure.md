# Project structure

The owner approved this eLearning-style layout on 2026-10-09. It reorganizes the working prototype for product development; Google authentication, EF Core/MySQL persistence, and cloud integration retain their OpenSpec tasks.

```text
MusicServer/
  MusicServerBackend/
    MusicServer.sln
    MusicServer.WebApi/          HTTP endpoints, host configuration, diagnostics
    MusicServer.Application/     Use cases, contracts, streaming operations
    MusicServer.Domain/          Domain data and rules
    MusicServer.Infrastructure/  Persistence and external provider implementations
    MusicServer.Tests/
      Unit/
      Integration/
      Shared/
  MusicServerFrontend/
    android/                    Existing Capacitor Android project
    src/
      app/                      Browser/native screen composition and UI tests
      features/playback/        Browser and native playback controllers
      lib/diagnostics/          Development-only diagnostic API
      types/                    Shared track contract
      testing/                  Shared test doubles
      assets/                   UI styles and application assets
    native-shell/               Isolated Android web entry
  docs/                         Developer/operator guides
  openspec/                     Active changes and completed capability specs
  scripts/                      Startup, build, inventory and review tooling
  tests/scripts/                Tooling contract tests
  .github/workflows/            Hosted builds
```

Keep `spec.md`, FRS/SDS documents, and `implementation_plan.md` as project context in the root. Detailed active architecture and requirements remain in the OpenSpec change; this guide describes code placement, not another requirements source. Create auth/upload/library feature folders as those tickets add code; avoid empty scaffolding for the entire MVP.

## Backend dependencies

Application references Domain. Infrastructure references Application and Domain. WebApi references Application and Infrastructure for composition. Domain and Application have no ASP.NET Core, EF Core, or provider dependencies. Infrastructure currently references the shared ASP.NET framework for the existing environment-guarded local storage adapter; this does not introduce an HTTP host into that layer.

Product EF Core `MusicServerDbContext`, Fluent API mappings, migrations/model snapshot, and repositories belong in Infrastructure. Use cases and integration interfaces belong in Application; HTTP binding stays in WebApi. Preserve existing `MusicServer.Diagnostics`, `MusicServer.Storage`, and `MusicServer.Streaming` namespaces during this move. Diagnostic components remain explicitly Development-only and do not become product authentication or a durable library.

## Frontend dependencies

Application composition imports features and shared code. Features import shared code; shared code must not import screens/features. Use direct imports and keep feature tests in nearby `__tests__/` folders. The combined diagnostic API is a temporary integration module; later product feature API code belongs with its feature. Browser and native entries continue to select their own playback engines. The native entry does not import the browser player.

Keep Android source sets, package ID `com.musicserver.shell`, Gradle wrapper, and relative Capacitor paths intact. Debug diagnostics/network allowance remain excluded from release. Public frontend assets contain application images/fonts only; owner MP3s and extracted artwork belong in private storage.

## Prototype path mapping

| Previous location | Current location |
| --- | --- |
| `src/web/` | `MusicServerFrontend/` |
| `src/server/MusicServer/Program.cs` and `Diagnostics/` | `MusicServerBackend/MusicServer.WebApi/` |
| `src/server/MusicServer/Streaming/` and storage contract | `MusicServerBackend/MusicServer.Application/` |
| Storage metadata record | `MusicServerBackend/MusicServer.Domain/Storage/` |
| Private filesystem adapter/range stream | `MusicServerBackend/MusicServer.Infrastructure/Storage/` |
| `tests/server/MusicServer.Tests/` | `MusicServerBackend/MusicServer.Tests/` |

Historical OpenSpec evidence keeps the commands/paths actually used at the time. Current commands are in README and AGENTS. Ignored local build/dependency files can remain under old paths; never copy them into a PR. Restart development servers from the repository root with the current commands after the move.
