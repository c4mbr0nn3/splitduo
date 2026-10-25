# Self-Hosting SplitDuo with Docker

SplitDuo runs as a single Docker container alongside a PostgreSQL database. No reverse proxy, no separate frontend service — the .NET backend serves the compiled frontend directly on one port.

This guide walks you through getting it running on your own server in about five minutes.

---

## What you need

- A Linux, macOS, or Windows machine with **[Docker](https://docs.docker.com/get-docker/)** and **[Docker Compose](https://docs.docker.com/compose/install/)** installed
- About 200 MB of free RAM (plus whatever PostgreSQL uses)
- A terminal

That's it. No .NET SDK, no Node.js, no build tools — the app image is [published on Docker Hub](https://hub.docker.com/r/j1mm0/splitduo) and pulled automatically.

---

## Quick start (4 commands)

The compose file pulls the app image from [Docker Hub](https://hub.docker.com/r/j1mm0/splitduo) — no build step. Before starting, generate the admin password and the JWT signing key into a `.env` file next to the compose file — compose fails fast if the password is missing, and the JWT key otherwise falls back to a published placeholder.

```bash
git clone https://gitlab.com/j1mm0/splitduo.git
cd splitduo
printf 'SD_INITIAL_USER_PASSWORD=%s\nSD_JWT_SECRET_KEY=%s\n' \
  "$(openssl rand -base64 24)" "$(openssl rand -base64 48)" > .env
docker compose up -d
```

Open `http://localhost:3000` and log in with:

- **Email:** `admin@splitduo.local` (default — change it via `SD_INITIAL_USER_EMAIL` if you like)
- **Password:** the value you put in `.env` as `SD_INITIAL_USER_PASSWORD`

You're running SplitDuo. See the next section for the other production settings.

---

## Before you rely on it: set your secrets

The quick start above writes both secrets into `.env`. If you started SplitDuo another way, or you are upgrading a stack created before the JWT key was interpolated from the environment, check that your signing key is not the published placeholder. `SD_INITIAL_USER_PASSWORD` is **required at first boot**; `SD_JWT_SECRET_KEY` protects your JWT tokens and must be set before real use.

Set it in `.env` next to the compose file — the compose file interpolates it from the environment:

```env
SD_JWT_SECRET_KEY=a-long-random-string-at-least-32-chars
```

Generate a strong JWT secret with, for example:

```bash
openssl rand -base64 48
```

> Set this **before** anyone enrols in two-factor authentication. The TOTP encryption key is derived from `SD_JWT_SECRET_KEY`, so changing it later invalidates existing authenticator enrolments until each user falls back to a backup code or re-enrols. Back up the value together with the database.

Then recreate the containers so the new values take effect:

```bash
docker compose down
docker compose up -d
```

> The initial admin user is only created on the **first** startup. If you already booted and want to change the admin credentials, change them from the in-app admin panel (or reset the database volume and start fresh).
>
> In Production the app refuses to seed the initial admin if `SD_INITIAL_USER_PASSWORD` is missing, shorter than 15 characters, or a known-weak value. In Development it falls back to the documented development default (see `docs/readme/development.md`).

---

## Running on a server (not just localhost)

The default compose file exposes the app on port `3000`.

> **Security: always use a reverse proxy.** Never expose SplitDuo directly to the public internet. The app does not terminate TLS and is not hardened to sit on the open web on its own. It does rate-limit sensitive endpoints (login, 2FA, receipt scanning), but that only protects against credential stuffing and abuse of specific routes — it won't stop volumetric traffic, port scanning, or protocol-level attacks. Put a reverse proxy in front and let it handle HTTPS, security headers, and edge-level filtering.

To serve SplitDuo on a real domain over HTTPS, [Caddy](https://caddyserver.com/) is the simplest option — it handles certificates automatically.

Add this to a `Caddyfile`:

```
splitduo.example.com {
    reverse_proxy localhost:3000
}
```

Run Caddy (in a container or as a system service). It will fetch and renew Let's Encrypt certificates automatically.

> SplitDuo does not terminate TLS itself. Whatever proxy you put in front should forward `X-Forwarded-Proto` so the app knows it's behind HTTPS.

### Per-IP rate limiting behind a proxy

SplitDuo rate-limits sensitive endpoints (login, 2FA, password reset) per client IP **and** per account. When a reverse proxy forwards traffic, the app only sees the proxy's IP — unless you tell it which proxies to trust:

```yaml
SD_KNOWN_PROXIES: 10.0.0.5          # the proxy's IP, or a CIDR like 10.0.0.0/24
```

- Value: comma-separated IP addresses and/or CIDR ranges of your trusted reverse proxy (e.g. `10.0.0.5,192.168.1.0/24`).
- The proxy must forward `X-Forwarded-For`; for each request, the **last** hop added by a trusted proxy is taken as the client IP.
- **If `SD_KNOWN_PROXIES` is unset, forwarded headers are ignored** (fail-closed) — every proxied request then looks like it came from one IP, so per-IP rate limiting collapses to a single shared bucket and legitimate users can trip 429s together. Per-account limiting still works in that case.

### Health endpoints

| Endpoint | Auth | Purpose | Notes |
|---|---|---|---|
| `/health` | none | Readiness (app + database) | `200` healthy, `503` unhealthy; status-only body; DB check cached 10 s; 60/min per IP |
| `/health/live` | none | Process liveness | Always `200` while the process runs; no database access; no rate limit |
| `/health/details` | SystemAdmin bearer token | Full per-check report for operators | `401` anonymous, `403` non-admin |

Use `/health` for an uptime monitor and `/health/live` for a container or
orchestrator liveness probe. The container healthcheck in
`docker-compose.yml` already uses `/health/live`, so `docker compose ps` shows
the app's health.

> **Behind a proxy:** if `SD_KNOWN_PROXIES` is unset, every client shares one
> per-IP rate-limit bucket. A request flood can therefore make `/health`
> return `429` to your monitor as well as the attacker. Set
> `SD_KNOWN_PROXIES` to your proxy's address and prefer a dedicated monitor
> IP. `/health/live` is not rate-limited.

---

## Optional features

### Receipt scanning (AI)

SplitDuo can read receipts from a photo and prefill the amount, date, and category. It works with **any OpenAI-compatible endpoint** — bring your own key, keep your data local.

Uncomment and set these in `docker-compose.yml`:

```yaml
SD_AI_BASE_URL: https://api.openai.com   # or your local LLM endpoint
SD_AI_API_KEY: your-api-key
SD_AI_MODEL: gpt-4o
```

Leave them unset to disable the feature — the app runs fine without it.

### Email (invitations and password reset)

Required only if you want to invite users by email or allow password reset flows:

```yaml
SD_EMAIL_SENDER_NAME: SplitDuo
SD_EMAIL_SENDER_ADDRESS: noreply@yourdomain.com
SD_EMAIL_SMTP_HOST: smtp.yourprovider.com
SD_EMAIL_SMTP_PORT: 587
SD_EMAIL_SMTP_USERNAME: your-smtp-user
SD_EMAIL_SMTP_PASSWORD: your-smtp-password
SD_EMAIL_SSL: "false"   # set "true" for port 465
```

### Public URL

If you serve SplitDuo from a domain (not localhost), set the base URL so invitation links and emails point to the right place:

```yaml
SD_BASE_URL: https://splitduo.example.com
```

---

## Where your data lives

Two Docker volumes hold everything:

| Volume | Contents | What it means for you |
|---|---|---|
| `postgres_data` | The PostgreSQL database | All your groups, expenses, balances, users |
| `app_logs` | Application logs | Useful for debugging; safe to delete |

These persist across container restarts and upgrades. **If you delete them, your data is gone.** Back them up regularly:

```bash
# Back up the database
docker compose exec postgres pg_dump -U splitduo splitduo > backup.sql

# Restore
docker compose exec -T postgres psql -U splitduo splitduo < backup.sql
```

---

## Updating

Pull the latest image and recreate the containers:

```bash
docker compose pull
docker compose up -d
```

The database volume is preserved, so your data carries over. EF Core applies any pending migrations automatically on startup.

> If you pinned a specific version tag instead of `latest`, edit the `image:` line in `docker-compose.yml` (e.g. `j1mm0/splitduo:1.11.1`) and rerun the commands above. See the [releases page](https://gitlab.com/j1mm0/splitduo/-/releases) for available tags.

### Testing a release candidate

Release candidates are pre-release builds for testing; they are not stable and not recommended for production.

Edit the `image:` line in `docker-compose.yml` to the `rc` tag and recreate the containers:

```yaml
services:
  splitduo-app:
    image: j1mm0/splitduo:rc
```

```bash
docker compose pull && docker compose up -d
```

The `rc` tag always points at the newest release candidate. To pin a specific one instead, use an immutable tag such as `j1mm0/splitduo:1.17.0-rc.1`. The database volume is preserved, so switching an existing instance over keeps your data; to go back to stable, set the `image:` line back to `j1mm0/splitduo:latest` and rerun `docker compose pull && docker compose up -d`.

---

## Common operations

| Task | Command |
|---|---|
| View live logs | `docker compose logs -f splitduo-app` |
| View database logs | `docker compose logs -f postgres` |
| Stop everything | `docker compose down` |
| Stop and delete data | `docker compose down -v` ⚠️ |
| Restart the app | `docker compose restart splitduo-app` |
| Check container status | `docker compose ps` |
| Check app health | `curl -fsS http://localhost:3000/health` |

---

## Troubleshooting

**The app won't start and logs mention the database**
The app waits for PostgreSQL to be healthy before booting. If the healthcheck fails, check `docker compose logs postgres`. The most common cause is a corrupted `postgres_data` volume — remove it only if you're willing to lose the data: `docker compose down -v && docker compose up -d`.

**I changed `SD_INITIAL_USER_*` but the admin account didn't update**
The initial user is created only on first startup. To change credentials after that, use the in-app admin panel or reset the database volume.

**Invitation emails aren't being sent**
Email is optional and disabled by default. Set the `SD_EMAIL_*` variables and restart. Check `docker compose logs splitduo-app` for SMTP errors.

**Receipt scanning returns errors**
Confirm `SD_AI_BASE_URL`, `SD_AI_API_KEY`, and `SD_AI_MODEL` are all set and that your API key has credit. The app surfaces the upstream error in the UI.

**Port 3000 is already in use**
Edit the `ports` mapping in `docker-compose.yml` — change `"3000:8080"` to `"YOUR_PORT:8080"`. The app always listens on 8080 inside the container; only the host port changes.

---

## Going further

- **Full configuration reference** — every environment variable: [`docs/readme/configuration.md`](readme/configuration.md)
- **Architecture overview** — how the single-container design works: [`docs/architecture/system-architecture.md`](architecture/system-architecture.md)
- **Project README** — features, tech stack, and links: [root `README.md`](../README.md)

Questions or issues? Open one on the [GitLab project](https://gitlab.com/j1mm0/splitduo/-/issues) or the [GitHub mirror](https://github.com/c4mbr0nn3/splitduo/issues).