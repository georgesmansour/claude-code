-- ============================================================================
--  Production database cleanup  (Task 1)
--  Target: PostgreSQL (matches the EF Core schema in schema.sql)
--
--  SAFE TO REVIEW BEFORE RUNNING. Everything runs inside ONE transaction and
--  every row that is deleted or mutated is first copied into the
--  `cleanup_backup` schema, so `rollback.sql` can restore the previous state.
--
--  What this script removes / why (see CLEANUP.md for the full rationale):
--    1. The default development admin `admin@invitations.local` (known default
--       password "Admin123!"). Ownership it holds is re-assigned to the Super
--       Admin first so the RESTRICT foreign keys stay valid.
--    2. All rows in `audit_log` — the audit feature is declared in the model but
--       NOTHING in the application writes to it, so any rows are dev noise.
--    3. Orphaned `notification_settings` — same story: unused feature.
--    4. (OPT-IN) Explicitly-listed demo/sample invitations by slug.
--
--  What this script PRESERVES: every admin/client account, the Super Admin,
--  all builtin & custom templates, all real invitations/guests/RSVPs, and
--  `system_settings` (which holds the JWT signing key — never touch it).
--
--  Run with:  psql "<connection-string>" -v ON_ERROR_STOP=1 -f cleanup.sql
-- ============================================================================

\set ON_ERROR_STOP on
BEGIN;

CREATE SCHEMA IF NOT EXISTS cleanup_backup;

-- ---------------------------------------------------------------------------
-- 1. Remove the default development admin (admin@invitations.local)
--    Re-assign anything it owns to the Super Admin so nothing is orphaned and
--    the ON DELETE RESTRICT foreign keys (templates / invitations / clients)
--    are never violated.
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    dev_admin_id   uuid;
    super_admin_id uuid;
BEGIN
    SELECT id INTO dev_admin_id
      FROM admin_accounts WHERE email = 'admin@invitations.local';

    SELECT id INTO super_admin_id
      FROM admin_accounts WHERE is_super_admin = TRUE
      ORDER BY created_at LIMIT 1;

    IF dev_admin_id IS NULL THEN
        RAISE NOTICE 'No default dev admin found — nothing to remove.';
    ELSIF super_admin_id IS NULL THEN
        RAISE EXCEPTION 'No Super Admin exists to inherit ownership; aborting for safety.';
    ELSIF dev_admin_id = super_admin_id THEN
        RAISE NOTICE 'Default admin IS the Super Admin — keeping it.';
    ELSE
        -- Back up the admin row.
        CREATE TABLE IF NOT EXISTS cleanup_backup.admin_accounts (LIKE admin_accounts INCLUDING ALL);
        INSERT INTO cleanup_backup.admin_accounts SELECT * FROM admin_accounts WHERE id = dev_admin_id;

        -- Back up the ownership we are about to re-point (uuid-keyed tables only).
        CREATE TABLE IF NOT EXISTS cleanup_backup.ownership_reassign (
            entity        text NOT NULL,
            entity_id     uuid NOT NULL,
            old_admin_id  uuid NOT NULL
        );
        INSERT INTO cleanup_backup.ownership_reassign
             SELECT 'templates',       id, created_by FROM templates       WHERE created_by = dev_admin_id
        UNION ALL SELECT 'invitations',    id, created_by FROM invitations    WHERE created_by = dev_admin_id
        UNION ALL SELECT 'client_accounts',id, created_by FROM client_accounts WHERE created_by = dev_admin_id;

        UPDATE templates       SET created_by = super_admin_id WHERE created_by = dev_admin_id;
        UPDATE invitations     SET created_by = super_admin_id WHERE created_by = dev_admin_id;
        UPDATE client_accounts SET created_by = super_admin_id WHERE created_by = dev_admin_id;
        -- audit_log.admin_id is ON DELETE SET NULL, so no re-assignment needed;
        -- the rows are purged wholesale in step 2 anyway.

        DELETE FROM admin_accounts WHERE id = dev_admin_id;
        RAISE NOTICE 'Removed default dev admin % (ownership re-assigned to %).', dev_admin_id, super_admin_id;
    END IF;
END $$;

-- ---------------------------------------------------------------------------
-- 2. Purge the unused audit_log (no code path writes to it).
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cleanup_backup.audit_log (LIKE audit_log INCLUDING ALL);
INSERT INTO cleanup_backup.audit_log SELECT * FROM audit_log;
DELETE FROM audit_log;

-- ---------------------------------------------------------------------------
-- 3. Purge unused notification_settings (feature is declared but never used).
--    FK is ON DELETE CASCADE from client_accounts, so this only clears dev rows.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cleanup_backup.notification_settings (LIKE notification_settings INCLUDING ALL);
INSERT INTO cleanup_backup.notification_settings SELECT * FROM notification_settings;
DELETE FROM notification_settings;

-- ---------------------------------------------------------------------------
-- 4. (OPT-IN) Remove explicitly-listed demo / sample invitations.
--    Deleting an invitation cascades to its sections, locations, gift_accounts,
--    rsvps, rsvp_guests, guests and client_accounts. Only uncomment once you
--    have listed the real demo slugs — this script will NOT guess.
-- ---------------------------------------------------------------------------
-- CREATE TABLE IF NOT EXISTS cleanup_backup.invitations (LIKE invitations INCLUDING ALL);
-- INSERT INTO cleanup_backup.invitations
--     SELECT * FROM invitations WHERE slug IN ('demo-wedding', 'test-birthday');
-- DELETE FROM invitations WHERE slug IN ('demo-wedding', 'test-birthday');

-- ---------------------------------------------------------------------------
-- 5. Verification — must all return 0 / TRUE before COMMIT.
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    orphan_sections int;
    orphan_rsvps    int;
    super_count     int;
BEGIN
    SELECT count(*) INTO orphan_sections
      FROM invitation_sections s LEFT JOIN invitations i ON i.id = s.invitation_id
      WHERE i.id IS NULL;
    SELECT count(*) INTO orphan_rsvps
      FROM rsvps r LEFT JOIN invitations i ON i.id = r.invitation_id
      WHERE i.id IS NULL;
    SELECT count(*) INTO super_count
      FROM admin_accounts WHERE is_super_admin = TRUE AND is_active = TRUE;

    IF orphan_sections <> 0 THEN RAISE EXCEPTION 'Orphaned invitation_sections detected: %', orphan_sections; END IF;
    IF orphan_rsvps    <> 0 THEN RAISE EXCEPTION 'Orphaned rsvps detected: %', orphan_rsvps; END IF;
    IF super_count      < 1 THEN RAISE EXCEPTION 'No active Super Admin remains — refusing to commit.'; END IF;

    RAISE NOTICE 'Verification passed. Active Super Admins: %', super_count;
END $$;

COMMIT;

-- After confirming the application behaves correctly, the backups can be dropped:
--     DROP SCHEMA cleanup_backup CASCADE;
