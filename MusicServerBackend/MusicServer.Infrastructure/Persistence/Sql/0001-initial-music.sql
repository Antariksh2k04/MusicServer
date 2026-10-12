CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
SET SESSION default_storage_engine = 'InnoDB';

CREATE TABLE `owners` (
    `id` int NOT NULL,
    `google_issuer` varchar(255) COLLATE utf8mb4_0900_bin NOT NULL,
    `google_subject` varchar(255) COLLATE utf8mb4_0900_bin NOT NULL,
    `display_name` text NULL,
    `created_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `ck_owner_singleton` CHECK (`id` = 1)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `quota_counters` (
    `metric` varchar(64) COLLATE utf8mb4_0900_bin NOT NULL,
    `period_start` datetime(6) NOT NULL,
    `used` bigint NOT NULL,
    `reserved` bigint NOT NULL,
    `limit` bigint NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    PRIMARY KEY (`metric`, `period_start`),
    CONSTRAINT `ck_quota_accounting` CHECK (`used` >= 0 AND `reserved` >= 0 AND `limit` >= 0 AND `used` <= `limit` AND `reserved` <= `limit` - `used`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `schema_migrations` (
    `version` int NOT NULL,
    `checksum` binary(32) NOT NULL,
    `applied_at` datetime(6) NOT NULL,
    PRIMARY KEY (`version`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `sessions` (
    `id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `owner_id` int NOT NULL,
    `client_kind` varchar(16) COLLATE utf8mb4_0900_bin NOT NULL,
    `access_hash` binary(32) NOT NULL,
    `access_expires_at` datetime(6) NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    `revoked_at` datetime(6) NULL,
    `created_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `ck_session_kind` CHECK (`client_kind` IN ('browser','android')),
    CONSTRAINT `FK_sessions_owners_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `owners` (`id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `upload_batches` (
    `id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `owner_id` int NOT NULL,
    `client_batch_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `manifest_hash` binary(32) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `FK_upload_batches_owners_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `owners` (`id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `refresh_tokens` (
    `id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `session_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `token_hash` binary(32) NOT NULL,
    `expires_at` datetime(6) NOT NULL,
    `consumed_at` datetime(6) NULL,
    `replacement_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    `created_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `FK_refresh_tokens_refresh_tokens_replacement_id` FOREIGN KEY (`replacement_id`) REFERENCES `refresh_tokens` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_refresh_tokens_sessions_session_id` FOREIGN KEY (`session_id`) REFERENCES `sessions` (`id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `import_claims` (
    `sha256` binary(32) NOT NULL,
    `item_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `lease_expires_at` datetime(6) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    PRIMARY KEY (`sha256`)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `storage_objects` (
    `id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `object_key` varchar(128) COLLATE utf8mb4_0900_bin NOT NULL,
    `size_bytes` bigint NOT NULL,
    `content_type` varchar(64) COLLATE utf8mb4_0900_bin NOT NULL,
    `provider_etag` text NULL,
    `item_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    `state` varchar(32) COLLATE utf8mb4_0900_bin NOT NULL,
    `attempts` int NOT NULL,
    `next_attempt_at` datetime(6) NULL,
    `error_code` varchar(64) COLLATE utf8mb4_0900_bin NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `ck_object_accounting` CHECK (`size_bytes` > 0 AND `attempts` >= 0),
    CONSTRAINT `ck_object_state` CHECK (`state` IN ('planned','uploading','stored','delete_pending','deleted'))
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `tracks` (
    `id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `owner_id` int NOT NULL,
    `sha256` binary(32) NOT NULL,
    `original_file_name` text NOT NULL,
    `title` text NOT NULL,
    `artist` text NOT NULL,
    `album` text NOT NULL,
    `album_artist` text NULL,
    `track_number` int NULL,
    `disc_number` int NULL,
    `duration_ms` bigint NULL,
    `audio_object_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `created_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `ck_track_metadata` CHECK ((`duration_ms` IS NULL OR `duration_ms` >= 0) AND (`track_number` IS NULL OR `track_number` >= 0) AND (`disc_number` IS NULL OR `disc_number` >= 0)),
    CONSTRAINT `FK_tracks_owners_owner_id` FOREIGN KEY (`owner_id`) REFERENCES `owners` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_tracks_storage_objects_audio_object_id` FOREIGN KEY (`audio_object_id`) REFERENCES `storage_objects` (`id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE TABLE `upload_items` (
    `id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `batch_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `client_item_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `file_name` text NOT NULL,
    `declared_bytes` bigint NOT NULL,
    `received_bytes` bigint NOT NULL,
    `sha256` binary(32) NULL,
    `temp_path` text NULL,
    `track_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NULL,
    `error_code` varchar(64) COLLATE utf8mb4_0900_bin NULL,
    `state` varchar(32) COLLATE utf8mb4_0900_bin NOT NULL,
    `attempt` int NOT NULL,
    `lease_expires_at` datetime(6) NULL,
    `reserved_bytes` bigint NOT NULL,
    `reserved_tracks` int NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `ck_item_accounting` CHECK (`declared_bytes` > 0 AND `declared_bytes` <= 52428800 AND `received_bytes` >= 0 AND `received_bytes` <= `declared_bytes` AND `reserved_bytes` >= 0 AND `reserved_tracks` >= 0 AND `attempt` > 0),
    CONSTRAINT `ck_item_state` CHECK (`state` IN ('selected','receiving','staged','processing','waiting_duplicate','imported','duplicate','failed')),
    CONSTRAINT `FK_upload_items_tracks_track_id` FOREIGN KEY (`track_id`) REFERENCES `tracks` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_upload_items_upload_batches_batch_id` FOREIGN KEY (`batch_id`) REFERENCES `upload_batches` (`id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_ci;

CREATE UNIQUE INDEX `IX_import_claims_item_id` ON `import_claims` (`item_id`);

CREATE UNIQUE INDEX `IX_owners_google_issuer_google_subject` ON `owners` (`google_issuer`, `google_subject`);

CREATE INDEX `IX_refresh_tokens_replacement_id` ON `refresh_tokens` (`replacement_id`);

CREATE INDEX `IX_refresh_tokens_session_id` ON `refresh_tokens` (`session_id`);

CREATE UNIQUE INDEX `IX_refresh_tokens_token_hash` ON `refresh_tokens` (`token_hash`);

CREATE UNIQUE INDEX `IX_sessions_access_hash` ON `sessions` (`access_hash`);

CREATE INDEX `IX_sessions_expires_at_revoked_at` ON `sessions` (`expires_at`, `revoked_at`);

CREATE INDEX `IX_sessions_owner_id` ON `sessions` (`owner_id`);

CREATE INDEX `IX_storage_objects_item_id` ON `storage_objects` (`item_id`);

CREATE UNIQUE INDEX `IX_storage_objects_object_key` ON `storage_objects` (`object_key`);

CREATE INDEX `IX_storage_objects_state_next_attempt_at` ON `storage_objects` (`state`, `next_attempt_at`);

CREATE UNIQUE INDEX `IX_tracks_audio_object_id` ON `tracks` (`audio_object_id`);

CREATE INDEX `IX_tracks_owner_id_created_at` ON `tracks` (`owner_id`, `created_at`);

CREATE UNIQUE INDEX `IX_tracks_sha256` ON `tracks` (`sha256`);

CREATE UNIQUE INDEX `IX_upload_batches_owner_id_client_batch_id` ON `upload_batches` (`owner_id`, `client_batch_id`);

CREATE UNIQUE INDEX `IX_upload_items_batch_id_client_item_id` ON `upload_items` (`batch_id`, `client_item_id`);

CREATE INDEX `IX_upload_items_state_lease_expires_at` ON `upload_items` (`state`, `lease_expires_at`);

CREATE INDEX `IX_upload_items_track_id` ON `upload_items` (`track_id`);

ALTER TABLE `import_claims` ADD CONSTRAINT `FK_import_claims_upload_items_item_id` FOREIGN KEY (`item_id`) REFERENCES `upload_items` (`id`) ON DELETE RESTRICT;

ALTER TABLE `storage_objects` ADD CONSTRAINT `FK_storage_objects_upload_items_item_id` FOREIGN KEY (`item_id`) REFERENCES `upload_items` (`id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261012061305_InitialMusicSchema', '10.0.11');

COMMIT;

