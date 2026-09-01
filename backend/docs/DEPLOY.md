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

## Sizing, and why the server is small

Measured on the running stack: **the whole application idles at ~190 MB.**

| Container | Idle RAM |
|-----------|----------|
| api | 90 MB |
| db | 54 MB |
| web | 14 MB |
| caddy | ~30 MB |

Compiling is the only heavy step — the .NET SDK build wants ~2 GB of RAM and leaves ~3.5 GB of
images and build cache behind. So **the server does not compile.** `build-images.yml` builds
in GitHub Actions and publishes to GHCR; the server pulls finished images. That removes the
SDK, the build cache and the memory spike from the machine you pay for.

What is actually needed:

| | Minimum | Comfortable |
|---|---|---|
| vCPU | 1 | 2 |
| RAM | 1 GB + swap | **2 GB** |
| Disk | 20 GB | 40 GB |

2 GB / 1 vCPU / 50 GB is the sweet spot on most providers. Add the swap file from §2.5
regardless — it costs nothing and turns a memory spike into a slowdown instead of an
OOM kill.

Disk grows with **uploaded media**, not the database: the entire dataset dumps to well under a
megabyte, while images cap at 8 MB and video at 15 MB each.

Every file works unchanged on any Docker host, so switching provider is a swap, not a rewrite.

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

## Part 2 — Create and harden the server

Target here is a **RackNerd KVM VPS running Ubuntu 24.04** with 2 GB RAM. A budget VPS is not a
managed cloud, and three differences matter before anything is installed.

### 2.1 Confirm the machine can run Docker

RackNerd sells both KVM and container-based plans. Docker needs **KVM** — on OpenVZ or LXC it
has no proper cgroups or overlay filesystem and fails in confusing ways.

```bash
systemd-detect-virt
```

Must print `kvm`. If it prints `openvz` or `lxc`, stop and open a ticket to move to a KVM plan;
nothing below will work reliably otherwise.

```bash
nproc && free -h && df -h /
```

### 2.2 Password login is enabled right now

RackNerd emails a root password rather than provisioning an SSH key, so the machine is
accepting password logins from the whole internet, and automated brute-forcing begins within
hours of an IP going live. This section is time-sensitive.

**Create a non-root user.** Working as root means every mistake and every compromised process
has total control.

```bash
adduser georges
usermod -aG sudo georges
```

**Install your public key.** Generate one on your PC first if you have none
(`ssh-keygen -t ed25519`), then paste the `.pub` contents into the file below:

```bash
mkdir -p /home/georges/.ssh && nano /home/georges/.ssh/authorized_keys
chmod 700 /home/georges/.ssh && chmod 600 /home/georges/.ssh/authorized_keys
chown -R georges:georges /home/georges/.ssh
```

**Test it in a second terminal, keeping the root session open.** After the next step a broken
key means recovery only through RackNerd's VNC console.

```bash
ssh georges@YOUR_SERVER_IP
sudo whoami
```

**Then disable root login and passwords** — the single highest-value change here:

```bash
sudo nano /etc/ssh/sshd_config
```

```
PermitRootLogin no
PasswordAuthentication no
KbdInteractiveAuthentication no
MaxAuthTries 3
```

```bash
sudo systemctl restart ssh
sudo sshd -T | grep -E "permitrootlogin|passwordauthentication"
```

Both must read `no`. Ubuntu 24.04 reads drop-in files from `/etc/ssh/sshd_config.d/`, and
provider images sometimes ship one that re-enables passwords and silently overrides the main
file — so verify the *effective* configuration rather than trusting what you typed.

### 2.3 Firewall — this is your only one

Unlike a managed cloud, there is no provider-level firewall in front of the machine. `ufw` on
the server is the entire perimeter.

```bash
sudo apt install -y ufw
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status verbose
```

Do **not** open 5432. The database has no published port and must stay that way.

> **Docker bypasses ufw.** Docker inserts its own iptables rules *ahead* of ufw's, so a
> container published with `ports:` is reachable from the internet even when ufw says that
> port is denied. With no cloud firewall behind it, there is no second net.
>
> This stack is safe by design — only Caddy publishes, on 80 and 443. But if you ever uncomment
> the database port mapping, write it as `127.0.0.1:5432:5432`. That loopback prefix is what
> stops Docker exposing it publicly.

### 2.4 fail2ban and automatic security updates

fail2ban matters more here than on a managed host, because the IP has already been advertising
password authentication.

```bash
sudo apt install -y fail2ban unattended-upgrades
sudo systemctl enable --now fail2ban
sudo dpkg-reconfigure --priority=low unattended-upgrades
```

### 2.5 Swap

With 2 GB of RAM there is little headroom, and Linux kills the largest process rather than
slowing down when memory runs out. Swap turns that cliff into a gentle degradation. Skip if
`free -h` already shows some.

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
free -h
```

### 2.6 Timezone

So log timestamps mean something when you are debugging under pressure.

```bash
sudo timedatectl set-timezone Asia/Beirut
```

### 2.7 Install git and Docker

```bash
sudo apt install -y git
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER && newgrp docker
```

```bash
docker compose version && docker run --rm hello-world
```

Ensure Docker starts on boot, so a reboot brings the site back without you:

```bash
sudo systemctl enable docker
```

### 2.8 Give the server read access to the private repository

The repository is private, so the server needs two credentials — and they are separate things
solving separate problems. Neither grants write access to anything.

**A deploy key, so it can clone the code.** Generated on the server; only the public half
leaves it. A deploy key is scoped to this one repository, unlike a personal SSH key which would
grant access to everything you own.

```bash
ssh-keygen -t ed25519 -C "digital-invite-server" -f ~/.ssh/id_ed25519 -N ""
cat ~/.ssh/id_ed25519.pub
```

Copy that line into GitHub → the repository → **Settings → Deploy keys → Add deploy key**.
Leave "Allow write access" **unchecked**. Then confirm it works:

```bash
ssh -T git@github.com
```

Expect `Hi georgesmansour/claude-code! You've successfully authenticated, but GitHub does not
provide shell access.` — that is success, not an error.

**A token, so it can pull images from GHCR.** Deploy keys do not work for the container
registry; it authenticates over HTTPS. Create a **classic** personal access token at
github.com/settings/tokens with **only** the `read:packages` scope ticked — no `repo`, no
write. Then, on the server:

```bash
echo "ghp_YOUR_TOKEN_HERE" | docker login ghcr.io -u georgesmansour --password-stdin
```

The credential is stored base64-encoded in `~/.docker/config.json` and survives reboots, so
this is a one-time step. Lock the file down, since it is a credential at rest:

```bash
chmod 600 ~/.docker/config.json
```

> **Use a classic token, not a fine-grained one.** Fine-grained token support for GHCR has been
> inconsistent; a classic token limited to `read:packages` is the reliably working path and is
> no broader in what it can do.

Because the repository is private, the published packages are private too and inherit its
access. Nobody without that token can pull your images.

---


## Part 3 — Deploy

### 3.1 Clone and configure

Clone over **SSH**, using the deploy key from §2.8 — HTTPS would prompt for credentials the
server cannot supply. Clone the deployment branch, not the default branch, which does not yet
contain this stack:

```bash
git clone -b feat/docker-deployment git@github.com:georgesmansour/claude-code.git app && cd app
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

Run the **Build images** workflow from the Actions tab, selecting `feat/docker-deployment` as
the branch, and wait for it to go green. There is no push trigger: publishing an image is a
step towards production, so it is always a deliberate action. Then, on the server:

```bash
docker compose -f docker-compose.prod.yml pull
```

```bash
docker compose -f docker-compose.prod.yml up -d
```

This downloads roughly 500 MB of finished images and starts them — no compiler, no SDK, no
build cache. It takes about a minute.

> **Fallback if the registry is unavailable** — or if you deliberately want to build on the
> machine — `docker compose -f docker-compose.prod.yml up --build -d` still works, because the
> services declare both `image` and `build`. It needs ~2 GB of free RAM, so use the swap file
> from §2.5 on a small instance.

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
on a budget VPS with no managed snapshots, this is your only copy.

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

### 4.3 Querying the production database

The database has **no published port** — it listens only on the private compose network. That is
deliberate: there is nothing on the server for anyone to connect to, scan, or brute-force.

**The normal way** is the `psql` that already ships inside the container. Nothing to install, no
port to open:

```bash
ssh ubuntu@your-server
```

```bash
cd app && docker compose -f docker-compose.prod.yml exec db psql -U invitation -d invitationplatform
```

A single query without opening a session:

```bash
docker compose -f docker-compose.prod.yml exec db psql -U invitation -d invitationplatform -c "SELECT count(*) FROM invitations;"
```

**To use pgAdmin from your PC**, two steps are needed, and the tunnel alone is not one of them.
First bind the port to the server's loopback by uncommenting this in `docker-compose.prod.yml`:

```yaml
    ports:
      - "127.0.0.1:5432:5432"
```

then `docker compose -f docker-compose.prod.yml up -d db` and open the tunnel:

```bash
ssh -L 5432:localhost:5432 ubuntu@your-server
```

While that session is open, pgAdmin connects to `localhost:5432` on your PC and reaches the
server's database. Close the SSH session and the route disappears.

> The `127.0.0.1:` prefix is the whole security story. It binds the port to the server's own
> loopback, reachable through SSH and from nowhere else. A bare `5432:5432` would publish your
> production database to the internet.
>
> Skipping the binding and opening only the tunnel does not work: the tunnel forwards to the
> server's `localhost:5432`, where nothing is listening until the port is bound.

**Never** open 5432 in `ufw`, and never publish it without the `127.0.0.1:` prefix.

### 4.4 Deploy an update

```bash
git pull && docker compose -f docker-compose.prod.yml pull && docker compose -f docker-compose.prod.yml up -d
```

Migrations run automatically on boot. Take a database backup first — this project has no
down-migration path.

### 4.5 Never run `down -v` in production

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

**Site unreachable, containers all healthy.** Almost always `ufw`. Confirm with `sudo ufw status
verbose` that 80 and 443 are allowed, and that the containers are actually listening with
`docker compose -f docker-compose.prod.yml ps`.

**Caddy cannot get a certificate.** DNS is not resolving to this server yet, or port 80 is not
reachable from the internet — Let's Encrypt validates over HTTP. Confirm with
`curl -I http://digital-invite.net` from somewhere other than the server.

**API restarting in a loop.** `docker compose -f docker-compose.prod.yml logs api`. Nearly
always a credential mismatch between the `POSTGRES_*` variables and the connection string. The
startup code retries ten times with backoff and logs the real error each time.

**Out of disk.** `docker system prune -af` reclaims old build layers. It does not touch named
volumes, so your data and certificates are safe.
