# EF Core schema code phase

Issue: [#5](https://github.com/Antariksh2k04/MusicServer/issues/5). Branch: `feature/backend/5-ef-core-migrations`, stacked on the repository-layout branch while PR #4 is open. Governing task: 3.1; design section 3 and the `owner-auth`, `mp3-import`, and `track-library` delta specs remain authoritative.

## Owner decisions and boundary

The owner selected the existing MySQL 8.0.46 instance, identified empty `music_server_db`, confirmed code-first, and asked for code while supplying the connection later. No credentials were requested or committed, and no database connection, creation, application, service restart, or real constraint verification occurred. MySQL 8.4 remains the deployment verification target.

## Prepared code

- Domain entities and Infrastructure Fluent mappings cover the ten slice tables, Restrict foreign keys, singleton owner, exact identities, fixed digests, unique submissions/audio/hash keys, quota accounting, UTC timestamps, and Unicode metadata.
- Initial EF migration and model snapshot were generated using the real design tool. Cyclic import/storage relationships are added after tables exist. Provider-native GUID mapping generates CHAR(36) with ASCII binary collation.
- Reviewed SQL and SHA-256 manifest are embedded in the build. Offline tests compare SQL against provider-generated migration output and reject model drift/tampering. The provider's inaccessible generated charset-extension calls were replaced with equivalent annotations; no internal-API shim was introduced.
- Explicit WebApi operator commands verify artifacts offline, inspect the selected database, or apply the initial schema under a database-scoped advisory lock. API startup does not invoke them. Full matching history/checksum skips a repeat; unknown/partial schemas fail closed. No automatic destructive repair is implemented.
- Operator setup, generation, and interrupted-DDL restore procedure are in `docs/database-migrations.md`. One opt-in test targets only `music_server_schema_test`; it leaves the schema and rolls back test rows.

## Executed checks (2026-10-12)

| Check | Result |
| --- | --- |
| Cached NuGet restore, explicit local package source, `-p:NuGetAudit=false` | Passed; no vulnerability audit claimed |
| `dotnet build MusicServerBackend/MusicServer.sln --no-restore` | Passed; zero warnings/errors |
| `dotnet test MusicServerBackend/MusicServer.Tests/MusicServer.Tests.csproj --no-build --no-restore` | 89 passed, one real-MySQL test skipped |
| Operator `database verify-artifact` | Passed offline |
| `node --test tests/scripts/review-checkpoint.test.cjs` | Passed; includes the new `.config` tool manifest |
| `openspec validate first-music-stream --strict` | Passed |
| `git -c core.safecrlf=false diff --check` | Passed |

The ordinary migration generation attempted a NuGet audit restore and failed because network access was unavailable. Cached restore plus `--no-build` enabled generation. EF Relational is explicitly pinned to prevent a lower transitive provider dependency in consuming projects. Schema tests retain all existing HTTP/storage tests. No frontend/native source changed, so existing frontend/native checks were not repeated for this ticket.

## Pending acceptance

Task 3.1 remains unchecked. Run fresh/repeated migration and real uniqueness/FK/check/collation tests with explicit credentials; rehearse interrupted DDL and empty-backup recovery; verify committed data across an actual MySQL restart. The opt-in transaction test cannot establish restart durability. Repeat against MySQL 8.4 before deployment. No Google sign-in, product import endpoint, cloud write, or product library runtime is implied by this schema work.

## Local configuration follow-up

The owner approved reusing their existing local MySQL login for `music_server_db`, while the other application's database remains outside scope. `scripts/local-database.ps1` securely prompts locally, targets only the MusicServer database, passes the connection via process environment, inspects before applying, and restores configuration on success/failure. Its isolated PowerShell test checks delimiter-containing dummy passwords, argument secrecy, fixed target, sequencing, and restoration without accessing MySQL. No personal credentials are recorded here.

GitHub inspection confirmed PR #4 merged into main, while PR #6 merged into the restructuring branch. The follow-up issue-#5 branch targets main and includes the previously reviewed schema plus this helper; the merged PR #6 alone does not establish main-branch availability or database acceptance.

## Repeat-inspection correction (2026-10-13)

The owner reported a successful empty-database migration followed by a failed repeat inspection, with no second migration requested. The old ordinal table comparison rejects a Windows-style inventory containing `__efmigrationshistory`. MySQL documents lowercase storage and case-insensitive lookup for `lower_case_table_names=1`, the Windows default; server setting/owner database contents have not been independently queried by the agent.

The inspector now reads the server identifier mode and compares table names accordingly, retaining exact checks for migration IDs/checksums and rejection of missing/extra/duplicate tables. No generated migration, checksum artifact, schema, or database data was changed. Safe error reporting distinguishes failed inspection gates and emits only a numeric provider error code without exception text or credentials.

Regression tests cover lowercase history names in modes 1/2, exact Unix names in mode 0, missing/extra/duplicate inventories, unknown modes, and redacted diagnostic output. The updated solution builds with zero warnings/errors; 100 backend tests passed and the separately configured real-MySQL test was skipped. Local helper contracts, offline artifact verification, strict OpenSpec validation, and diff whitespace checks passed.

The owner reran `./scripts/local-database.ps1 -Action apply` with the corrected build and reported two matching `Initial migration already applied; history and checksum match. No changes made.` lines, one for the preliminary inspection and one for apply. This establishes owner-reported fresh application and successful repeat verification of the local database. Real constraint/recovery/restart checks and task 3.1 remain incomplete.
