# Database cleanup — analysis & rationale (Task 1)

Scripts: [`cleanup.sql`](cleanup.sql) (apply) and [`rollback.sql`](rollback.sql) (revert).
Both run in a single transaction; every removed/mutated row is copied into the
`cleanup_backup` schema first, so the operation is reversible.

## How the analysis was done
There is no live database available in this environment, so this is a **static**
analysis of the schema (`schema.sql` + EF migrations) cross-referenced against
every read/write in the backend (`grep` of `src/`). "Unused" below means *no
controller or service reads or writes the object* — verified in code, not guessed.

## Removed — and why

| Item | Type | Why it is safe to remove |
|------|------|--------------------------|
| `admin_accounts` row `admin@invitations.local` | Dev/test data | Default seed account with the publicly-known password `Admin123!`. Since the Super Admin feature shipped it has **no `/admin` access** and is pure attack surface. Ownership it holds (templates/invitations/clients) is re-assigned to the Super Admin first, so `ON DELETE RESTRICT` FKs stay valid. |
| all `audit_log` rows | Old logs | The `AuditLog` entity, config and DbSet exist, but **no code path ever inserts a row**. Any rows are development noise. |
| all `notification_settings` rows | Unused feature data | `NotificationSetting` is mapped but **never read or written**. Rows are dev leftovers. |
| *(opt-in)* demo invitations by slug | Test/demo data | Left commented — the script will not guess which invitations are real. Fill in the slugs you know are demos. Deletion cascades to sections/locations/gifts/rsvps/guests/clients. |

## Preserved (production-required — never touched)
- `system_settings` — **holds the JWT signing key**; touching it would invalidate every session.
- All `admin_accounts` except the dev default, incl. the Super Admin.
- All `client_accounts` (login credentials), `templates` (builtin **and** custom), `invitations`, `invitation_sections`, `locations`, `gift_accounts`, `guests`, `rsvps`, `rsvp_guests`.
- `__EFMigrationsHistory` — required by EF to track applied migrations.

## Dead *schema objects* (structure, not data) — recommended follow-up
These are unused but are **not** dropped by `cleanup.sql`, because dropping a
table/column must be paired with the matching EF model change and a migration
(otherwise the model and database drift). They should be removed in a single
reviewed migration alongside the media-schema work:

- Table `audit_log` (+ its 3 indexes) — no writers.
- Table `notification_settings` — no readers/writers.
- Column `templates.thumbnail_url` — never populated or read.
- Column `invitations.password_hash` — password-protected invitations are not implemented anywhere.

## FK-integrity & behavioural safety
- Ownership re-assignment happens **before** the dev-admin delete, so no
  `RESTRICT` FK is ever violated.
- The script ends with verification that fails the transaction if any orphaned
  `invitation_sections`/`rsvps` exist or if no active Super Admin remains.
- Only unused-feature rows and one dev account are removed, so application
  behaviour is unchanged after cleanup.

## Running
```bash
psql "$CONNECTION_STRING" -v ON_ERROR_STOP=1 -f backend/db/cleanup.sql
# verify the app, then if needed:
psql "$CONNECTION_STRING" -v ON_ERROR_STOP=1 -f backend/db/rollback.sql
```
