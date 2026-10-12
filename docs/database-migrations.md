# EF Core database migrations

Task 3.1 in `openspec/changes/first-music-stream/tasks.md` governs acceptance. This ticket prepares code-first entities, mappings, the initial migration/snapshot, reviewed SQL, and operator tooling. Database execution is deferred until the owner supplies configuration. Local target: existing MySQL 8.0.46, empty `music_server_db`; deployment still requires separate MySQL 8.4 verification.

## Layout and tools

Entities belong in `MusicServerBackend/MusicServer.Domain/Entities/`. Context, factory, migration history, and SQL artifacts belong in `MusicServer.Infrastructure/Persistence/`. EF Core/Relational/Design and the local `dotnet-ef` tool are pinned to 10.0.11; official `MySql.EntityFrameworkCore` is 10.0.9. Domain has no EF references. The provider uses its native CHAR(36) GUID mapping, UTC DATETIME(6), fixed 32-byte digests, and explicit display/identity collations.

From the repository root:

```powershell
dotnet tool restore
dotnet restore MusicServerBackend/MusicServer.Tests/MusicServer.Tests.csproj
dotnet build MusicServerBackend/MusicServer.sln --no-restore
dotnet test MusicServerBackend/MusicServer.Tests/MusicServer.Tests.csproj --no-build --no-restore
dotnet run --project MusicServerBackend/MusicServer.WebApi --no-build --no-restore -- database verify-artifact
```

`verify-artifact` works offline and never opens MySQL. Tests compare embedded SQL with the provider's generated migration and detect model/snapshot drift. Real MySQL checks are explicitly skipped without test configuration. API startup never applies migrations or opens the product database yet; diagnostic fixtures remain temporary.

## Add your connection later

Use a separate migration identity with DDL/DML rights on this dedicated database. The later runtime identity will have DML rights only. Do not put a password in tracked appsettings, command arguments, Git, or chat. `ConnectionStrings__MusicServerMigration` is the operator configuration variable; runtime registration is a later task.

Enter the complete connection string at a hidden prompt, for example with these fields: `Server=127.0.0.1;Port=3306;Database=music_server_db;User ID=<migration-user>;Password=<password>;Old Guids=false`.

```powershell
$migrationSecret = Read-Host 'Migration connection string' -AsSecureString
try {
    $env:ConnectionStrings__MusicServerMigration = ([PSCredential]::new('migration', $migrationSecret)).GetNetworkCredential().Password
    dotnet run --project MusicServerBackend/MusicServer.WebApi --no-build --no-restore -- database inspect
    # Run deliberately when inspect reports an empty database:
    dotnet run --project MusicServerBackend/MusicServer.WebApi --no-build --no-restore -- database apply
} finally {
    Remove-Item Env:ConnectionStrings__MusicServerMigration -ErrorAction SilentlyContinue
}
```

`inspect` reads schema/history under the migration advisory lock. `apply` accepts an empty database, or verifies and skips the already applied initial migration. Missing configuration, non-InnoDB defaults, unsupported servers, unknown tables, incomplete histories, and checksum mismatches fail closed. It never creates/drops a database, deletes data, or repairs history automatically. Both EF history and the SQL checksum journal must agree. Use this operator command rather than `dotnet ef database update`, which bypasses checksum journaling. The operator command currently supports the initial migration only; extend ordered version verification before adding a second release migration.

## Generate and review future changes

```powershell
dotnet ef migrations add <MeaningfulName> --project MusicServerBackend/MusicServer.Infrastructure --startup-project MusicServerBackend/MusicServer.Infrastructure --output-dir Persistence/Migrations
dotnet ef migrations script 0 InitialMusicSchema --project MusicServerBackend/MusicServer.Infrastructure --startup-project MusicServerBackend/MusicServer.Infrastructure --output .local/generated-schema.sql
```

Generation uses an unusable offline connection when no migration configuration exists. Build first and use `--no-build` to avoid another restore in offline environments. The current provider scaffolds calls to an inaccessible `MySQLEntityTypeBuilderExtensions.HasCharSet`; replace those generated calls with equivalent `HasAnnotation("MySQL:Charset", "utf8mb4")` and retain the explicit table collation annotation. Review regenerated files rather than adding a shim for the provider's internal API. The initial migration also sets session storage engine to InnoDB. Keep SQL UTF-8 without BOM/LF, regenerate its SHA-256 manifest, and run SQL equality/model drift tests. Never edit an applied migration or assume provider idempotent SQL repairs partial DDL.

## Interrupted migration recovery

MySQL DDL implicitly commits. An interruption can leave tables or EF history without the checksum journal, including a lost acknowledgement after successful execution. A repeated operator command refuses this state before further DDL. Save the error, table inventory, and histories; stop product writers and retain a backup. Do not insert journal/history rows merely to silence the check.

For the initial **empty development database only**, recover using an operator-reviewed restore of its verified pre-migration empty backup, or a new isolated empty database. Point the migration identity at that restored/new database, inspect, apply, and inspect again. No automatic drop/repair command is provided. For any database containing data, restore a verified compatible backup with its retained objects and rehearse in isolation before resuming. Recovery and real server restart remain acceptance checks to perform later.

## Real MySQL checks when ready

Create a separate empty `music_server_schema_test` database and explicitly set `MUSICSERVER_MYSQL_TEST_CONNECTION` to its connection string. Run `dotnet test MusicServerBackend/MusicServer.Tests/MusicServer.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~MySqlSchemaTests`. The test refuses other database names, applies/verifies migrations, tests singleton-owner and duplicate submission/hash constraints, Unicode and UTC round trips, and rolls back its test rows; schema tables remain. It must not target `music_server_db`.

Still record actual server/version results for fresh/repeated application, an interrupted-DDL/empty-backup recovery drill, and committed data surviving a real MySQL restart. The automated transaction test cannot establish restart durability. Repeat provider-sensitive checks on MySQL 8.4 before deployment.
