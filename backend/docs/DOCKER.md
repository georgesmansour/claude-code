# Running the platform in Docker

Three containers, one network:

| Container        | Image                        | Role                                                        | Published on |
|------------------|------------------------------|-------------------------------------------------------------|--------------|
| `invitation-web` | `invitation-platform/web:local` | nginx — serves `frontend/`, proxies `/api/*` to the API      | `localhost:8080` |
| `invitation-api` | `invitation-platform/api:local` | ASP.NET Core backend, EF migrations + seeding on startup     | `localhost:5035` |
| `invitation-db`  | `postgres:18-alpine`            | PostgreSQL — same image and startup ordering as production   | `localhost:5432` |

The local stack uses **its own Postgres container**, deliberately matching production, so what
you test here exercises the real deployment path: an empty volume, migrations on boot, seeding
on boot. Production publishes no database port; locally it is exposed so you can attach pgAdmin.

The browser only ever talks to `localhost:8080`. `frontend/shared/config.js` resolves the API
to the **same origin**, and nginx forwards `/api/*` to the API container over the internal
compose network. Port `5035` is published purely so you can curl the API directly while
debugging — nothing depends on it.

It is deliberately **5035, not 5034**: Visual Studio's launch profiles bind 5034, so leaving
that port free lets the container stack and an F5 debugging session run at the same time.
See [The three environments](#the-three-environments).

---

## The three environments

They are fully separate: different config files, different ports, and no file shared between
them. All three can be running at once.

| | Visual Studio (F5) | Local containers | Production |
|---|---|---|---|
| **Started by** | F5 / `dotnet run` | `docker compose up --build -d` | `docker compose -f docker-compose.prod.yml up --build -d` |
| **ASPNETCORE_ENVIRONMENT** | `Development` | `Production` | `Production` |
| **Config comes from** | `appsettings.json` + `appsettings.Development.json` | `.env` + `docker-compose.yml` | `.env` on the server + `docker-compose.prod.yml` |
| **Secrets file** | `appsettings.Development.json` *(gitignored)* | `.env` *(gitignored)* | `.env` on the server, from `.env.prod.example` |
| **Reach it at** | `localhost:5034` | `localhost:8080` | your domain |
| **Scalar API reference** | yes, `/scalar/v1` | no — Production hides it | no |
| **Serves the frontend** | yes, from `frontend/` on disk | no — the web container does | no — the web container does |

### Why Visual Studio keeps working

The launch profiles in `Properties/launchSettings.json` all set
`ASPNETCORE_ENVIRONMENT=Development`, and .NET automatically layers
`appsettings.Development.json` over `appsettings.json` in that environment. That file holds
your real development credentials, is listed in `backend/.gitignore`, and is excluded from the
Docker image by `backend/.dockerignore` — so it is available to the debugger and to nothing
else. Nothing extra to configure; F5 just works.

Editing files changes the debugger's behaviour immediately. It does **not** change the
containers, which serve what was baked into the image at build time.

### Three databases, one per environment

Nothing is shared. Visual Studio talks to Neon (your existing development data). The local
containers talk to their own `invitation-db` container. Production talks to its own `db`
container on the server.

That separation is what makes the local stack a genuine rehearsal: it starts from an empty
volume exactly as production will, so a migration that fails on a clean database fails on your
machine rather than on the server.

To point the local containers back at Neon instead, swap the commented connection string in
`.env` — both lines are there.

---

## One-time setup

1. **Start Docker Desktop** and wait for the whale icon to stop animating. Everything below
   fails with `cannot connect to the Docker daemon` until it is running.

2. **Create your `.env`** in the repository root:

   ```bash
   cp .env.example .env
   ```

   Then fill in `ConnectionStrings__DefaultConnection`, `SuperAdmin__*`, and — only if you
   want email notifications — the `Smtp__*` values. `.env` is gitignored.

   > The API image ships **without** `appsettings.json` (see `backend/.dockerignore`). That is
   > deliberate: a file copied into a layer stays in the image forever, so credentials must
   > never be baked in. `.env` and `docker-compose.yml` are the container's entire config.

---

## Daily commands

Run these from the repository root (the folder holding `docker-compose.yml`).

```bash
docker compose up --build -d
```

Builds both images and starts them detached. First run takes a few minutes (NuGet restore);
later runs reuse cached layers and take seconds. Then open **http://localhost:8080**.

```bash
docker compose ps
```

Shows both containers and their health. `invitation-api` must reach `healthy` before
`invitation-web` starts — that gate is `depends_on: condition: service_healthy`.

```bash
docker compose logs -f api
```

Follow the API log. On a healthy first boot you will see the migration output, then
`Now listening on: http://[::]:8080`.

```bash
docker compose down
```

Stops and removes the containers. The `api_media` volume survives, so uploaded media is
still there next time. To wipe it too: `docker compose down -v`.

---

## Using Docker Desktop

Because `docker-compose.yml` sets `name: invitation-platform`, the two containers appear in
Docker Desktop as **one collapsible group**, not two loose entries.

- **Containers** tab → expand `invitation-platform` → `invitation-api` and `invitation-web`.
  Each row shows the health status reported by the `HEALTHCHECK` in its Dockerfile.
- Click a container for **Logs**, **Inspect**, **Bind mounts / volumes**, **Exec** (a shell
  inside the container), and **Files** (browse its filesystem — useful for confirming that
  `/app/media_storage` really holds uploads).
- **Images** tab → `invitation-platform/api` and `invitation-platform/web`, both tagged
  `local`, because each service declares an explicit `image:` name. Without that, Docker
  Desktop would show generated names like `invitation-platform-api`.
- **Volumes** tab → `invitation-platform_api_media` — the uploaded media, safe across rebuilds.
- The **Builds** tab keeps every build with its logs, which is where to look when a build fails.

You can start and stop the whole stack from the group's ▶/■ buttons instead of the CLI.

---

## How the containers are built

### `backend/Dockerfile`

Two stages. The build stage copies **only the `.csproj` files first** and runs
`dotnet restore`, so editing C# does not re-download NuGet packages — that layer is cached
until a project file actually changes. It then publishes Release output.

The runtime stage starts from `aspnet:10.0` (no SDK, much smaller), adds `curl` for the
healthcheck, runs as the non-root `$APP_UID` user baked into the Microsoft image, and sets
`Storage__BasePath=/app/media_storage` as an absolute path so it never depends on the working
directory.

The healthcheck uses `--start-period=90s` because EF migrations and seeding run *before* the
app starts listening. That start period is what stops Docker from reporting a false failure
on the very first boot against an empty database.

### `frontend/Dockerfile`

No build step — the site is hand-written HTML/CSS/JS. A tiny `alpine` staging stage copies the
folder and deletes `Dockerfile`, `.dockerignore` and `nginx.conf`, then the nginx stage copies
the cleaned result into the web root. (The plumbing has to be *in* the build context for
`COPY` to reach it, so `.dockerignore` can't be what removes it from the web root.)

### `frontend/nginx.conf` — what replaced Netlify

`netlify.toml` is gone. Its two redirect rules are now nginx locations:

| Was (netlify.toml)                      | Is now (nginx.conf)                                 |
|-----------------------------------------|-----------------------------------------------------|
| `/api/*` → backend URL                  | `location /api/ { proxy_pass http://api:8080; }`    |
| `/invite/*` → `/invitation.html`        | `location ^~ /invite/ { try_files $uri /invitation.html; }` |

`api` there is the compose **service name**, resolved by Docker's internal DNS — which is why
the backend URL no longer has to be edited per deployment.

Two rules mirror behaviour the API's own `RewriteOptions` provides in local `dotnet run`:
`try_files $uri $uri.html` turns `/admin` into `/admin.html`, and `$uri/` lets a template
folder resolve to its `index.html`.

Caching is deliberate: assets get 7 days, but HTML and `shared/config.js` are `no-cache` so a
redeploy is never masked by a stale page — the old "only works after Ctrl+F5" symptom.

---

## Test locally, then deploy

The local stack and production run the same images, the same nginx config and the same database
engine. The differences are deliberate and small: production adds Caddy for TLS, publishes no
port except 80/443, and builds for ARM if you deploy to Oracle.

**1 — Test the change locally.**

```bash
docker compose up --build -d
```

**2 — Test it against a clean database**, the way production will start. This is the step that
catches a migration which only works because your existing database already has the data:

```bash
docker compose down -v && docker compose up --build -d
```

`-v` destroys the local volumes. Safe here, never in production.

**3 — Verify.** Both containers healthy, the site loads, and you can log in at `/admin` with
the seeded Super Admin.

```bash
docker compose ps
```

**4 — Commit and push.** The server deploys by cloning, so nothing uncommitted reaches it.

**5 — Deploy.** On the server, per `DEPLOY.md`:

```bash
git pull && docker compose -f docker-compose.prod.yml up --build -d
```

Take a database backup first — this project has no down-migration path.

### Working with the local database

```bash
docker compose exec db psql -U invitation -d invitationplatform
```

Or attach pgAdmin to `localhost:5432` with the credentials from `.env`. To reset to a clean
database without touching uploaded media, remove just that volume:

```bash
docker compose down && docker volume rm invitation-platform_db_data
```

### Carrying your Neon data across

Migrations and seeding build the schema, the Super Admin and the built-in templates — but not
your real invitations, clients or landing-page settings. To bring those into a container:

```bash
pg_dump "<your-neon-connection-string>" --no-owner --no-privileges -Fc -f neon.dump
```

```bash
docker compose exec -T db pg_restore -U invitation -d invitationplatform --no-owner < neon.dump
```

`backend/schema.sql` is reference documentation, not an import step.

---

## Troubleshooting

**`invitation-api` is unhealthy or restarting.** `docker compose logs api`. Almost always the
connection string. The startup code retries a failing database for ~10 attempts with backoff and
logs `WARN Database not reachable (attempt n/10)` each time, so the real error is in that line.

**The site loads but every action fails with a network error.** nginx cannot reach the API.
Confirm with `docker compose exec web wget -qO- http://api:8080/health` — that must print
`{"status":"ok",...}` from inside the web container.

**`invitation-web` says unhealthy but the site works.** The healthcheck reaches nginx over
127.0.0.1, so this means nginx is not listening where the probe looks. Check that the `listen
[::]:80;` line is still in `frontend/nginx.conf` — the base image's
`10-listen-on-ipv6-by-default.sh` refuses to add it to a customised config, and without it
anything resolving `localhost` to `::1` inside the container gets connection refused while
external IPv4 traffic keeps working.

**Port already in use.** Something else holds 8080 or 5035. Change the *left* half of the port
mapping in `docker-compose.yml` (e.g. `"9090:80"`); the right half is the container's own port
and must not change.

**Changed a file but the container still serves the old one.** Static content is baked into the
image at build time. Re-run `docker compose up --build -d`.

**`warn: ... No XML encryptor configured. Key ... may be persisted in unencrypted form.`**
Harmless here. That is ASP.NET Data Protection, which nothing in this app consumes — there are
no cookies, antiforgery tokens or TempData. Authentication uses the JWT signing key persisted in
the `system_settings` table, which is unaffected by container restarts.

**`/scalar/v1` returns 404.** Expected. The containers run with
`ASPNETCORE_ENVIRONMENT=Production`, and the OpenAPI/Scalar UI is Development-only. Use
`dotnet run` locally to browse the API reference.
