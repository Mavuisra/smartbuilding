-- Tables cloud-only (sync magasin, documents, portail PDG) — conservées après parité desktop

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

CREATE TABLE IF NOT EXISTS synced_entity_store (
    id CHAR(36) NOT NULL PRIMARY KEY,
    entity_type VARCHAR(64) NOT NULL,
    json_data JSON NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    deleted_at DATETIME NULL,
    INDEX idx_sync_type_updated (entity_type, updated_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS server_sync_events (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(150) NOT NULL DEFAULT '',
    user_role VARCHAR(32) NOT NULL DEFAULT '',
    entity_type VARCHAR(64) NOT NULL,
    direction VARCHAR(16) NOT NULL DEFAULT 'push',
    records_count INT NOT NULL DEFAULT 0,
    success TINYINT(1) NOT NULL DEFAULT 1,
    error_message TEXT NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_sync_events_created (created_at),
    INDEX idx_sync_events_type (entity_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS synced_documents (
    id CHAR(36) NOT NULL PRIMARY KEY,
    entity_type VARCHAR(64) NOT NULL,
    entity_id CHAR(36) NOT NULL,
    category VARCHAR(32) NOT NULL DEFAULT 'rapports',
    file_name VARCHAR(260) NOT NULL,
    mime_type VARCHAR(120) NOT NULL DEFAULT 'application/pdf',
    file_data LONGBLOB NOT NULL,
    file_size BIGINT NOT NULL DEFAULT 0,
    content_sha256 VARCHAR(64) NOT NULL DEFAULT '',
    added_by VARCHAR(150) NOT NULL DEFAULT '',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_doc_entity (entity_type, entity_id),
    INDEX idx_doc_category (category, updated_at),
    INDEX idx_doc_sha (content_sha256)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS executive_notifications (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    title VARCHAR(200) NOT NULL,
    message TEXT NOT NULL,
    severity VARCHAR(16) NOT NULL DEFAULT 'Info',
    source VARCHAR(80) NOT NULL DEFAULT '',
    action_type VARCHAR(80) NOT NULL DEFAULT '',
    entity_type VARCHAR(64) NOT NULL DEFAULT '',
    entity_count INT NOT NULL DEFAULT 0,
    created_by VARCHAR(150) NOT NULL DEFAULT '',
    is_read TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_notif_created (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;
