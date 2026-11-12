-- Apply once before deploying the API version that writes sync events.
-- A per-user sequence row is locked until the business transaction commits, so event cursors
-- cannot skip an event due to concurrent transactions committing out of auto-increment order.

CREATE TABLE IF NOT EXISTS `sync_event_sequence` (
    `user_id` BIGINT NOT NULL,
    `last_event_id` BIGINT NOT NULL DEFAULT 0,
    PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE IF NOT EXISTS `sync_event` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `user_id` BIGINT NOT NULL,
    `user_event_id` BIGINT NOT NULL,
    `event_type` VARCHAR(48) NOT NULL,
    `entity_id` BIGINT NOT NULL DEFAULT 0,
    `related_id` BIGINT NOT NULL DEFAULT 0,
    `created_at` DATETIME NOT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_event` (`user_id`, `user_event_id`),
    INDEX `idx_sync_event_user_id_id` (`user_id`, `user_event_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
