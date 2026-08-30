-- Multi-tenant cloud extensions (aligné Desktop v1.0.98)

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

CREATE TABLE IF NOT EXISTS organizations (
    id CHAR(36) NOT NULL PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    slug VARCHAR(80) NOT NULL,
    database_name VARCHAR(120) NOT NULL DEFAULT '',
    city VARCHAR(120) NOT NULL DEFAULT '',
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_by_username VARCHAR(150) NOT NULL DEFAULT '',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    deleted_at DATETIME NULL,
    UNIQUE KEY uq_organizations_slug (slug),
    INDEX idx_organizations_active (is_active, deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO organizations (id, name, slug, database_name, city, is_active, created_by_username, created_at, updated_at)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    'Organisation principale',
    'organisation-principale',
    'sbms_local',
    '',
    1,
    'admin',
    NOW(),
    NOW()
);

ALTER TABLE synced_entity_store ADD COLUMN organization_id CHAR(36) NULL AFTER entity_type;
ALTER TABLE synced_entity_store ADD INDEX idx_sync_org_type_updated (organization_id, entity_type, updated_at);

ALTER TABLE server_sync_events ADD COLUMN organization_id CHAR(36) NULL AFTER user_role;
ALTER TABLE server_sync_events ADD INDEX idx_sync_events_org (organization_id, created_at);

ALTER TABLE synced_documents ADD COLUMN organization_id CHAR(36) NULL AFTER entity_type;
ALTER TABLE synced_documents ADD INDEX idx_doc_org (organization_id, category, updated_at);

UPDATE synced_entity_store SET organization_id = '00000000-0000-0000-0000-000000000001'
WHERE organization_id IS NULL;

UPDATE server_sync_events SET organization_id = '00000000-0000-0000-0000-000000000001'
WHERE organization_id IS NULL;

UPDATE synced_documents SET organization_id = '00000000-0000-0000-0000-000000000001'
WHERE organization_id IS NULL;

SET FOREIGN_KEY_CHECKS = 1;
