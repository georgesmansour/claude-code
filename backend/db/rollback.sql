-- ============================================================================
--  Rollback for cleanup.sql  (Task 1)
--
--  Restores everything cleanup.sql removed, from the `cleanup_backup` schema.
--  Run only if cleanup.sql has been applied and the backup schema still exists.
--
--  Run with:  psql "<connection-string>" -v ON_ERROR_STOP=1 -f rollback.sql
-- ============================================================================

\set ON_ERROR_STOP on
BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'cleanup_backup') THEN
        RAISE EXCEPTION 'cleanup_backup schema not found — nothing to roll back.';
    END IF;
END $$;

-- 1. Restore the default development admin.
INSERT INTO admin_accounts
SELECT * FROM cleanup_backup.admin_accounts b
WHERE NOT EXISTS (SELECT 1 FROM admin_accounts a WHERE a.id = b.id);

-- 2. Re-point ownership back to the original (dev) admin.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = 'cleanup_backup' AND table_name = 'ownership_reassign') THEN
        UPDATE templates       t SET created_by = o.old_admin_id
          FROM cleanup_backup.ownership_reassign o
         WHERE o.entity = 'templates'       AND o.entity_id = t.id;
        UPDATE invitations     i SET created_by = o.old_admin_id
          FROM cleanup_backup.ownership_reassign o
         WHERE o.entity = 'invitations'     AND o.entity_id = i.id;
        UPDATE client_accounts c SET created_by = o.old_admin_id
          FROM cleanup_backup.ownership_reassign o
         WHERE o.entity = 'client_accounts' AND o.entity_id = c.id;
    END IF;
END $$;

-- 3. Restore audit_log rows.
INSERT INTO audit_log
SELECT * FROM cleanup_backup.audit_log b
WHERE NOT EXISTS (SELECT 1 FROM audit_log a WHERE a.id = b.id);

-- 4. Restore notification_settings rows.
INSERT INTO notification_settings
SELECT * FROM cleanup_backup.notification_settings b
WHERE NOT EXISTS (SELECT 1 FROM notification_settings a WHERE a.id = b.id);

-- 5. (OPT-IN) Restore demo invitations, if you enabled step 4 in cleanup.sql.
--    NOTE: cascaded children (sections, rsvps, guests, …) are NOT restored by
--    this simple rollback — take a full backup before deleting invitations.
-- INSERT INTO invitations
-- SELECT * FROM cleanup_backup.invitations b
-- WHERE NOT EXISTS (SELECT 1 FROM invitations a WHERE a.id = b.id);

COMMIT;

-- Once satisfied, remove the backup schema:
--     DROP SCHEMA cleanup_backup CASCADE;
