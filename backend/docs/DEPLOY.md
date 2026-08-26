# Deploying to production

Target: a single Linux server running four containers via `docker-compose.prod.yml`.

```
      Internet
         │  :80 / :443
    ┌────▼────┐
    │  caddy  │  TLS termination (Let's Encrypt, automatic)
    └────┬────┘
    ┌────▼────┐
    │   web   │  nginx — static site, /api proxy, URL rewrites
    └────┬────┘
    ┌────▼────┐
    │   api   │  ASP.NET Core — migrations + seeding on boot
    └────┬────┘
    ┌────▼────┐
    │   db    │  PostgreSQL, on the server's disk
    └─────────┘
```

Only `caddy` publishes ports. The API and the database are reachable exclusively on the private
compose network — there is no port to attack.

These instructions use **Oracle Cloud Always Free** (ARM, $0/month). Every file works unchanged
on any Docker host, so the fallback to a small VPS is a provider swap, not a rewrite.

---

## Part 1 — Before you touch a server

### 1.1 Rotate every credential

The development credentials have been in a git-tracked file, in shell history, and in chat
transcripts. Treat all of them as public:

- **Neon database password** — rotate or delete the database if you are moving off it
- **Gmail App Password** — revoke at [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords) and issue a new one
- **Super Admin password** — the local development password must never reach production

Generate production secrets rather than inventing them:

```bash
openssl rand -base64 32
```

### 1.2 Push your code

The server deploys by cloning the repository, so everything must be committed first.

```bash
git add -A && git commit -m "Add production Docker stack" && git push
```

### 1.3 The domain

The site is **digital-invite.net**. Caddy is configured to serve the apex and to 301 `www` to
it, so there is one canonical address rather than two copies of every invitation link.

Deploy on the bare IP first regardless. `SITE_ADDRESS=:80` runs the site over plain HTTP on the
server's address; only switch to the domain once that works. Two reasons: a failure then has one
cause instead of three, and Let's Encrypt validation fails if it reaches the domain before the
server is answering — which counts against a rate limit you cannot reset.

---

## Part 2 — Create the server

### 2.1 Provision an Always Free ARM instance

In the Oracle Cloud console: **Compute → Instances → Create instance**.

- **Shape:** `VM.Standard.A1.Flex` — this is the Always Free ARM shape. The free allowance is
  4 OCPUs and 24 GB RAM total; 2 OCPU / 12 GB is more than enough here and leaves headroom
  for a second instance.
- **Image:** Ubuntu 24.04 (aarch64)
- **Boot volume:** 50 GB is plenty (the free allowance is 200 GB total)
- **SSH key:** upload your public key — you cannot log in without it

> **Expect "Out of host capacity."** Free ARM capacity is heavily contested. Retry, try a
> different availability domain, or pick a less busy home region. This is the single most
> common reason people give up on Oracle's free tier — it is not a mistake on your part.

Nothing in this stack is architecture-specific: .NET 10, nginx-alpine, postgres-alpine and
caddy all publish `arm64` images, and building on the server compiles natively.

### 2.2 Open ports 80 and 443 — *both* places

This is where nearly everyone gets stuck. Oracle blocks traffic at **two independent layers**,
and opening only one leaves the site unreachable with no error message.

**Layer 1 — the virtual cloud network.** Networking → Virtual Cloud Networks → your VCN →
Security Lists → default list → **Add Ingress Rules**:

| Source CIDR | Protocol | Destination port |
|-------------|----------|------------------|
| `0.0.0.0/0` | TCP | 80 |
| `0.0.0.0/0` | TCP | 443 |

**Layer 2 — iptables on the instance itself.** Oracle's Ubuntu images ship with a restrictive
`iptables` ruleset that drops everything except SSH. SSH in and run:

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 80 -j ACCEPT
```

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 443 -j ACCEPT
```

```bash
sudo netfilter-persistent save
```

Do **not** open 5432. The database has no published port and must stay that way.

### 2.3 Install Docker

```bash
curl -fsSL https://get.docker.com | sudo sh
```

```bash
sudo usermod -aG docker $USER && newgrp docker
```

Verify with `docker compose version` — modern Docker bundles Compose v2, no separate install.

---

## Part 3 — Deploy

### 3.1 Clone and configure

```bash
git clone https://github.com/georgesmansour/claude-code.git app && cd app
```

```bash
cp .env.prod.example .env && nano .env
```

Fill in every value. The three that must agree with each other: `POSTGRES_USER`,
`POSTGRES_PASSWORD`, and the matching `Username=` / `Password=` inside
`ConnectionStrings__DefaultConnection`. A mismatch shows up as the API retrying and failing
authentication in a loop.

Leave `SITE_ADDRESS=:80` for now.

### 3.2 Start it

```bash
docker compose -f docker-compose.prod.yml up --build -d
```

The first build takes several minutes — it pulls the .NET SDK image and compiles from source.
Subsequent builds reuse cached layers.

```bash
docker compose -f docker-compose.prod.yml ps
```

All four containers should reach `healthy` or `running`, in order: `db` → `api` → `web` → `caddy`.

```bash
docker compose -f docker-compose.prod.yml logs -f api
```

On first boot you should see EF migrations creating the schema, the Super Admin being seeded,
then `Now listening on: http://[::]:8080`.

Visit `http://<server-ip>` and log in at `/admin` with your `SuperAdmin__` credentials.

### 3.3 Add the domain and HTTPS

Only once the site answers on the bare IP.

**1 — Create both DNS records** at your registrar, pointing at the server's public IP:

| Type | Name | Value |
|------|------|-------|
| `A` | `@` | your server's public IP |
| `A` | `www` | your server's public IP |

Both are needed. Caddy requests a certificate for **every** name in `SITE_ADDRESS`, so if `www`
does not resolve, issuance for it fails repeatedly even though the apex works. A `CNAME` for
`www` pointing at the apex is equally fine.

**2 — Wait for DNS to propagate.** Both must return the server's IP before continuing:

```bash
dig +short digital-invite.net && dig +short www.digital-invite.net
```

**3 — Switch the address** in `.env` on the server:

```bash
sed -i 's|^SITE_ADDRESS=.*|SITE_ADDRESS=digital-invite.net, www.digital-invite.net|' .env
```

**4 — Restart the edge:**

```bash
docker compose -f docker-compose.prod.yml up -d caddy
```

Caddy requests both certificates within seconds and renews them automatically from then on.
Watch it: `docker compose -f docker-compose.prod.yml logs -f caddy`.

**5 — Verify all three behaviours:**

```bash
curl -I https://digital-invite.net
curl -I http://digital-invite.net
curl -I https://www.digital-invite.net
```

Expect `200`, then a `308` redirect to HTTPS (Caddy adds that automatically), then a `301` to
the apex.

**6 — Tighten the host filter** now that real hostnames exist. In `.env`:

```
AllowedHosts=digital-invite.net;www.digital-invite.net
```

Then `docker compose -f docker-compose.prod.yml up -d api`.

**7 — Consider HSTS**, but not immediately. Once the certificate has been renewing cleanly for a
week or two, uncomment the `Strict-Transport-Security` header in the `Caddyfile`. Browsers cache
it for its full `max-age`, so enabling it early means a TLS problem locks visitors out of a site
you cannot quickly fix.

---

## Part 4 — Keep it alive

### 4.1 Back up the database

Nothing is backed up by default. The `db_data` volume lives on one disk on one machine — and
on Oracle's free tier, on an instance the provider may reclaim.

```bash
docker compose -f docker-compose.prod.yml exec -T db \
  pg_dump -U invitation invitationplatform | gzip > backup-$(date +%F).sql.gz
```

Put that in a daily cron job and copy the result **off the server**. A dump that only exists on
the machine it came from is not a backup.

### 4.2 Back up uploaded media

The `api_media` volume holds every uploaded photo and video. It is not in the database dump:

```bash
docker run --rm -v invitation-platform_api_media:/data -v $(pwd):/out alpine \
  tar czf /out/media-$(date +%F).tar.gz -C /data .
```

### 4.3 Deploy an update

```bash
git pull && docker compose -f docker-compose.prod.yml up --build -d
```

Migrations run automatically on boot. Take a database backup first — this project has no
down-migration path.

### 4.4 Never run `down -v` in production

`docker compose down` is safe: it stops containers and keeps volumes. Adding `-v` destroys
`db_data`, `api_media` *and* `caddy_data` — that is your database, your users' uploads, and
your TLS certificates, in one command. Re-issuing certificates also counts against Let's
Encrypt rate limits.

---

## Known limitation: visitor IP addresses

`DemoRequest.IpAddress` records the address that ASP.NET sees on the connection. Behind Caddy
and nginx, that is an internal container IP, not the visitor's.

Everything else is unaffected — the platform generates no absolute URLs server-side, which is
why no `ForwardedHeaders` configuration is needed for links, redirects, or authentication.

If you want the real address on demo requests, add `UseForwardedHeaders` in `Program.cs` with
`KnownNetworks` scoped to the docker bridge subnet. Configure the known networks explicitly —
enabling forwarded headers without that restriction lets any caller spoof the recorded IP.

---

## Troubleshooting

**Site unreachable, containers all healthy.** One of the two Oracle firewall layers. Verify the
VCN ingress rules exist *and* that `sudo iptables -L INPUT -n --line-numbers` shows your ACCEPT
rules for 80/443 above the REJECT rule.

**Caddy cannot get a certificate.** DNS is not resolving to this server yet, or port 80 is not
reachable from the internet — Let's Encrypt validates over HTTP. Confirm with
`curl -I http://digital-invite.net` from somewhere other than the server.

**API restarting in a loop.** `docker compose -f docker-compose.prod.yml logs api`. Nearly
always a credential mismatch between the `POSTGRES_*` variables and the connection string. The
startup code retries ten times with backoff and logs the real error each time.

**Out of disk.** `docker system prune -af` reclaims old build layers. It does not touch named
volumes, so your data and certificates are safe.
