-- Unified per-user change log used to recover changes missed while a client is offline.
-- Run once against the existing HY database before deploying the API version that writes SyncEvent rows.

CREATE TABLE IF NOT EXISTS `sync_event` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `user_id` BIGINT NOT NULL,
    `event_type` VARCHAR(48) NOT NULL,
    `entity_id` BIGINT NOT NULL DEFAULT 0,
    `related_id` BIGINT NOT NULL DEFAULT 0,
    `created_at` DATETIME NOT NULL,
    PRIMARY KEY (`id`),
    INDEX `idx_sync_event_user_id_id` (`user_id`, `id`),
    INDEX `idx_sync_event_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
