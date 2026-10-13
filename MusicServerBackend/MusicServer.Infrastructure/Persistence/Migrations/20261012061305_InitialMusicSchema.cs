using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMusicSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL DDL implicitly commits. Use InnoDB regardless of server defaults.
            migrationBuilder.Sql("SET SESSION default_storage_engine = 'InnoDB';");
            migrationBuilder.CreateTable(
                name: "owners",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    google_issuer = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    google_subject = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owners", x => x.id);
                    table.CheckConstraint("ck_owner_singleton", "`id` = 1");
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "quota_counters",
                columns: table => new
                {
                    metric = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    period_start = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    used = table.Column<long>(type: "bigint", nullable: false),
                    reserved = table.Column<long>(type: "bigint", nullable: false),
                    limit = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quota_counters", x => new { x.metric, x.period_start });
                    table.CheckConstraint("ck_quota_accounting", "`used` >= 0 AND `reserved` >= 0 AND `limit` >= 0 AND `used` <= `limit` AND `reserved` <= `limit` - `used`");
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "schema_migrations",
                columns: table => new
                {
                    version = table.Column<int>(type: "int", nullable: false),
                    checksum = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    applied_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schema_migrations", x => x.version);
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    client_kind = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    access_hash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    access_expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.CheckConstraint("ck_session_kind", "`client_kind` IN ('browser','android')");
                    table.ForeignKey(
                        name: "FK_sessions_owners_owner_id",
                        column: x => x.owner_id,
                        principalTable: "owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "upload_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    client_batch_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    manifest_hash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_upload_batches_owners_owner_id",
                        column: x => x.owner_id,
                        principalTable: "owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    session_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    token_hash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    replacement_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_refresh_tokens_replacement_id",
                        column: x => x.replacement_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "import_claims",
                columns: table => new
                {
                    sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    item_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    lease_expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_claims", x => x.sha256);
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "storage_objects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    object_key = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    provider_etag = table.Column<string>(type: "text", nullable: true),
                    item_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    state = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    error_code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_objects", x => x.id);
                    table.CheckConstraint("ck_object_accounting", "`size_bytes` > 0 AND `attempts` >= 0");
                    table.CheckConstraint("ck_object_state", "`state` IN ('planned','uploading','stored','delete_pending','deleted')");
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    original_file_name = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    artist = table.Column<string>(type: "text", nullable: false),
                    album = table.Column<string>(type: "text", nullable: false),
                    album_artist = table.Column<string>(type: "text", nullable: true),
                    track_number = table.Column<int>(type: "int", nullable: true),
                    disc_number = table.Column<int>(type: "int", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    audio_object_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.id);
                    table.CheckConstraint("ck_track_metadata", "(`duration_ms` IS NULL OR `duration_ms` >= 0) AND (`track_number` IS NULL OR `track_number` >= 0) AND (`disc_number` IS NULL OR `disc_number` >= 0)");
                    table.ForeignKey(
                        name: "FK_tracks_owners_owner_id",
                        column: x => x.owner_id,
                        principalTable: "owners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tracks_storage_objects_audio_object_id",
                        column: x => x.audio_object_id,
                        principalTable: "storage_objects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateTable(
                name: "upload_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    batch_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    client_item_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    declared_bytes = table.Column<long>(type: "bigint", nullable: false),
                    received_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: true),
                    temp_path = table.Column<string>(type: "text", nullable: true),
                    track_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    error_code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    state = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    attempt = table.Column<int>(type: "int", nullable: false),
                    lease_expires_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reserved_bytes = table.Column<long>(type: "bigint", nullable: false),
                    reserved_tracks = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_upload_items", x => x.id);
                    table.CheckConstraint("ck_item_accounting", "`declared_bytes` > 0 AND `declared_bytes` <= 52428800 AND `received_bytes` >= 0 AND `received_bytes` <= `declared_bytes` AND `reserved_bytes` >= 0 AND `reserved_tracks` >= 0 AND `attempt` > 0");
                    table.CheckConstraint("ck_item_state", "`state` IN ('selected','receiving','staged','processing','waiting_duplicate','imported','duplicate','failed')");
                    table.ForeignKey(
                        name: "FK_upload_items_tracks_track_id",
                        column: x => x.track_id,
                        principalTable: "tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_upload_items_upload_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "upload_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4")
                .Annotation("MySQL:Collation", "utf8mb4_0900_as_ci")
                .Annotation("Relational:Collation", "utf8mb4_0900_as_ci");

            migrationBuilder.CreateIndex(
                name: "IX_import_claims_item_id",
                table: "import_claims",
                column: "item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_owners_google_issuer_google_subject",
                table: "owners",
                columns: new[] { "google_issuer", "google_subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_replacement_id",
                table: "refresh_tokens",
                column: "replacement_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_session_id",
                table: "refresh_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_access_hash",
                table: "sessions",
                column: "access_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_expires_at_revoked_at",
                table: "sessions",
                columns: new[] { "expires_at", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_owner_id",
                table: "sessions",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_storage_objects_item_id",
                table: "storage_objects",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_storage_objects_object_key",
                table: "storage_objects",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_objects_state_next_attempt_at",
                table: "storage_objects",
                columns: new[] { "state", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tracks_audio_object_id",
                table: "tracks",
                column: "audio_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tracks_owner_id_created_at",
                table: "tracks",
                columns: new[] { "owner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tracks_sha256",
                table: "tracks",
                column: "sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upload_batches_owner_id_client_batch_id",
                table: "upload_batches",
                columns: new[] { "owner_id", "client_batch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upload_items_batch_id_client_item_id",
                table: "upload_items",
                columns: new[] { "batch_id", "client_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_upload_items_state_lease_expires_at",
                table: "upload_items",
                columns: new[] { "state", "lease_expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_upload_items_track_id",
                table: "upload_items",
                column: "track_id");

            migrationBuilder.AddForeignKey(
                name: "FK_import_claims_upload_items_item_id",
                table: "import_claims",
                column: "item_id",
                principalTable: "upload_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_storage_objects_upload_items_item_id",
                table: "storage_objects",
                column: "item_id",
                principalTable: "upload_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_storage_objects_upload_items_item_id",
                table: "storage_objects");

            migrationBuilder.DropTable(
                name: "import_claims");

            migrationBuilder.DropTable(
                name: "quota_counters");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "schema_migrations");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "upload_items");

            migrationBuilder.DropTable(
                name: "tracks");

            migrationBuilder.DropTable(
                name: "upload_batches");

            migrationBuilder.DropTable(
                name: "storage_objects");

            migrationBuilder.DropTable(
                name: "owners");
        }
    }
}
