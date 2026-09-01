# Native PostgreSQL on Windows — a dev and a prod database

Optional setup for running two real databases on your PC, inspectable with pgAdmin, separate
from each other and from the container's database:

| Database | Used by | Connects as |
|----------|---------|-------------|
| `invitation_dev`  | Visual Studio (F5) | `localhost:5432` |
| `invitation_prod` | the local container stack | `host.docker.internal:5432` |

Install **PostgreSQL 18** — Neon runs 18.6, and `pg_dump` refuses to dump from a server newer
than itself, so an older local install cannot read your Neon backups.

---

## 1 · Install

Download the Windows x86-64 installer for **PostgreSQL 18** from
[enterprisedb.com/downloads/postgres-postgresql-downloads](https://www.enterprisedb.com/downloads/postgres-postgresql-downloads).

During setup:

- Keep **PostgreSQL Server**, **pgAdmin 4** and **Command Line Tools** selected. Command Line
  Tools is what gives you `psql`, `pg_dump` and `pg_restore` on Windows.
- Set a password for the `postgres` superuser and record it.
- Leave the port at **5432**.

Then add the binaries to your `PATH` so `psql` works in any terminal — in PowerShell, as
Administrator:

```powershell
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\Program Files\PostgreSQL\18\bin", "Machine")
```

Open a **new** terminal and confirm:

```bash
psql --version
```

---

## 2 · Create the role and both databases

Edit `backend/db/setup-local-postgres.sql` and replace `CHANGE_ME_BEFORE_RUNNING` with a real
password, then run it as the superuser:

```bash
psql -U postgres -f backend/db/setup-local-postgres.sql
```

That creates the `invitation` login role, both databases, and — importantly — grants `CREATE`
on the `public` schema. Since PostgreSQL 15 that grant is no longer implicit, and without it the
first EF migration fails with *permission denied for schema public*.

Verify:

```bash
psql -U invitation -d invitation_dev -c "\conninfo"
```

---

## 3 · Let the containers reach it

This is the part that catches people. A container's `localhost` is the *container*, not your PC,
and PostgreSQL's defaults reject connections from outside the machine. Three changes:

**3.1 — Listen on all interfaces.** Open
`C:\Program Files\PostgreSQL\18\data\postgresql.conf` and set:

```
listen_addresses = '*'
```

**3.2 — Allow the Docker network.** Open
`C:\Program Files\PostgreSQL\18\data\pg_hba.conf` and add this line at the end. The range covers
the private subnets Docker Desktop uses for its bridge networks:

```
host    all    all    172.16.0.0/12    scram-sha-256
```

**3.3 — Restart the service.** In PowerShell as Administrator:

```powershell
Restart-Service postgresql-x64-18
```

If the containers still cannot connect, allow the port through Windows Defender Firewall:

```powershell
New-NetFirewallRule -DisplayName "PostgreSQL 5432" -Direction Inbound -Protocol TCP -LocalPort 5432 -Action Allow
```

---

## 4 · Point each environment at its database

**Visual Studio → `invitation_dev`.** In
`backend/src/InvitationPlatform.Api/appsettings.Development.json` (gitignored):

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost; Port=5432; Database=invitation_dev; Username=invitation; Password=YOUR_PASSWORD; SSL Mode=Disable;"
}
```

**Containers → `invitation_prod`.** In `.env` at the repository root:

```
ConnectionStrings__DefaultConnection="Host=host.docker.internal; Port=5432; Database=invitation_prod; Username=invitation; Password=YOUR_PASSWORD; SSL Mode=Disable;"
```

`host.docker.internal` is the hostname Docker Desktop resolves to the Windows host. `localhost`
there would point the container at itself and fail with connection refused.

`SSL Mode=Disable` in both: a default local install serves no TLS, and the traffic never leaves
your machine.

### What about the `db` container?

It is still defined in `docker-compose.yml` and will keep starting. That is harmless — the API
simply ignores it once the connection string points elsewhere, and it costs a few MB of RAM. Its
host port is already **5433**, so it does not fight the native install for 5432.

To stop it entirely, comment out the `db:` service block **and** the `depends_on: db:` block in
the `api` service. Both are clearly marked.

---

## 5 · Load your real data into both

Your Neon dump is already at `backend/db/backups/neon.dump` (gitignored). Restore it into each
database — the dump carries the EF migration history, so the API sees a fully migrated schema
and skips straight to seeding:

```bash
pg_restore -U invitation -d invitation_dev --no-owner --no-privileges backend/db/backups/neon.dump
```

```bash
pg_restore -U invitation -d invitation_prod --no-owner --no-privileges backend/db/backups/neon.dump
```

To refresh the dump from Neon later:

```bash
pg_dump "postgresql://USER:PASSWORD@YOUR-HOST.neon.tech/neondb?sslmode=require" --no-owner --no-privileges -Fc -f backend/db/backups/neon.dump
```

The real host and user are in your gitignored `appsettings.Development.json` — this file is
committed, so it carries placeholders only.

Leaving a database empty is also fine — migrations and seeding provision it on first boot.

### Uploaded media does not live in the database

`user_media` rows reference files on disk. Visual Studio writes them to
`backend/src/InvitationPlatform.Api/media_storage/`; the containers keep them in the
`invitation-platform_api_media` volume. Restoring a dump into a fresh environment gives you rows
whose files are missing, until you copy the files across:

```bash
docker run --rm -v invitation-platform_api_media:/dest -v "$(pwd)/backend/src/InvitationPlatform.Api/media_storage:/src:ro" alpine sh -c "cp -r /src/. /dest/ && chown -R 1654 /dest"
```

---

## 6 · Verify

Start Visual Studio with F5 and confirm the log line naming the database, then:

```bash
docker compose up --build -d && docker compose ps
```

Both should be healthy and reading different data. A quick way to prove they are separate — add
a demo request on one and confirm the count differs:

```bash
psql -U invitation -d invitation_dev  -t -c "SELECT count(*) FROM demo_requests;"
```

```bash
psql -U invitation -d invitation_prod -t -c "SELECT count(*) FROM demo_requests;"
```

---

## Troubleshooting

**`could not connect to server` from the API container.** Almost always step 3. Check all three:
`listen_addresses = '*'`, the `pg_hba.conf` line, and that the service was restarted. Test from
inside the container:

```bash
docker compose exec api curl -sv telnet://host.docker.internal:5432
```

**`password authentication failed for user "invitation"`.** The password in the connection string
does not match the one in the SQL script. Reset it:

```bash
psql -U postgres -c "ALTER ROLE invitation PASSWORD 'NEW_PASSWORD';"
```

**`permission denied for schema public`** on the first migration. The `GRANT ALL ON SCHEMA
public` from step 2 did not run against that database. Re-run it while connected to that
database specifically.

**`server version mismatch` from pg_dump.** Your local PostgreSQL is older than Neon's 18.6.
Install PostgreSQL 18.

**Port 5432 already in use during install.** Something else holds it — often an older
PostgreSQL service or a previously published container port. Check with
`netstat -ano | findstr :5432`.
