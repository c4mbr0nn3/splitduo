# Self-Hosting SplitDuo with Docker

SplitDuo runs as a single Docker container alongside a PostgreSQL database. The container has no reverse proxy and no separate frontend service. The .NET backend serves the compiled frontend directly on one port.

This guide shows how to get SplitDuo running on your own server in about five minutes.

---

## What you need

- A Linux, macOS, or Windows machine with **[Docker](https://docs.docker.com/get-docker/)** and **[Docker Compose](https://docs.docker.com/compose/install/)** installed
- About 200 MB of free RAM (plus whatever PostgreSQL uses)
- A terminal

These are the only requirements. You need no .NET SDK, no Node.js, and no build tools. The project publishes the app image on [Docker Hub](https://hub.docker.com/r/j1mm0/splitduo), and the compose file pulls it automatically.

---

## Quick start (4 commands)

The compose file pulls the app image from [Docker Hub](https://hub.docker.com/r/j1mm0/splitduo) instead of building it. Before starting, generate the admin password and the JWT signing key into a `.env` file next to the compose file. Compose fails fast if the password is missing. If you omit the JWT key, the compose file falls back to a published placeholder.

```bash
git clone https://gitlab.com/j1mm0/splitduo.git
cd splitduo
printf 'SD_INITIAL_USER_PASSWORD=%s\nSD_JWT_SECRET_KEY=%s\n' \
  "$(openssl rand -base64 24)" "$(openssl rand -base64 48)" > .env
docker compose up -d
```

Open `http://localhost:3000` and log in with:

- **Email:** `admin@splitduo.local` by default (change it via `SD_INITIAL_USER_EMAIL` if you like)
- **Password:** the value you put in `.env` as `SD_INITIAL_USER_PASSWORD`

SplitDuo is now running. See the next section for the other production settings.

---

## Before you rely on it: set your secrets

The quick start above writes both secrets into `.env`. Some stacks predate the interpolation of the JWT key from the environment. If you started SplitDuo another way, or you are upgrading such a stack, check your signing key. Confirm that it is not the published placeholder. `SD_INITIAL_USER_PASSWORD` is **required at first boot**. `SD_JWT_SECRET_KEY` protects your JWT tokens and must be set before real use.

Set it in `.env` next to the compose file. The compose file interpolates it from the environment:

```env
SD_JWT_SECRET_KEY=a-long-random-string-at-least-32-chars
```

Generate a strong JWT secret with, for example:

```bash
openssl rand -base64 48
```

> Set this **before** anyone enrols in two-factor authentication. The app derives the TOTP encryption key from `SD_JWT_SECRET_KEY`. Changing it later invalidates existing authenticator enrolments until each user falls back to a backup code or re-enrols. Back up the value together with the database.

The same command (with a shorter length, e.g. `openssl rand -base64 24`) also works for the initial admin password and the database password.

**Database password:** the compose file ships with `SD_DB_PASSWORD: splitduo` (and the matching `POSTGRES_PASSWORD` on the `postgres` service). Change both to the same strong value, for example `openssl rand -base64 24`, **before the first startup**. PostgreSQL applies `POSTGRES_PASSWORD` only when it initializes the data volume. Changing it later requires resetting the database volume (see [Common operations](#common-operations)).

Then recreate the containers so the new values take effect:

```bash
docker compose down
docker compose up -d
```

> The app creates the initial admin user only on the **first** startup. If you already booted and want to change the admin credentials, change them from the in-app admin panel. Alternatively, reset the database volume and start fresh.
>
> In Production the app refuses to seed the initial admin if `SD_INITIAL_USER_PASSWORD` is missing, shorter than 15 characters, or a known-weak value. In Development it falls back to the documented development default (see `docs/readme/development.md`).

---

## Running on a server (not just localhost)

The default compose file exposes the app on port `3000`.

> **Security: always use a reverse proxy.** Never expose SplitDuo directly to the public internet. The app does not terminate TLS and is not hardened to sit on the open web on its own. It does rate-limit sensitive endpoints (login, 2FA, receipt scanning), but that protects only against credential stuffing and abuse of specific routes. It does not stop volumetric traffic, port scanning, or protocol-level attacks. Put a reverse proxy in front and let it handle HTTPS, security headers, and edge-level filtering.

To serve SplitDuo on a real domain over HTTPS, [Caddy](https://caddyserver.com/) is the simplest option. It handles certificates automatically.

Add this to a `Caddyfile`:

```
splitduo.example.com {
    reverse_proxy localhost:3000
}
```

Run Caddy (in a container or as a system service). It fetches and renews Let's Encrypt certificates automatically.

> SplitDuo does not terminate TLS itself. Configure the proxy in front to forward `X-Forwarded-Proto` so the app knows it is behind HTTPS.

### Per-IP rate limiting behind a proxy

SplitDuo rate-limits sensitive endpoints (login, 2FA, password reset) per client IP **and** per account. When a reverse proxy forwards traffic, the app only sees the proxy's IP. Tell the app which proxies to trust:

```yaml
SD_KNOWN_PROXIES: 10.0.0.5          # the proxy's IP, or a CIDR like 10.0.0.0/24
```

- Value: comma-separated IP addresses and/or CIDR ranges of your trusted reverse proxy (e.g. `10.0.0.5,192.168.1.0/24`).
- The proxy must forward `X-Forwarded-For`. For each request, the app takes the **last** hop added by a trusted proxy as the client IP.
- **If `SD_KNOWN_PROXIES` is unset, the app ignores forwarded headers** (fail-closed). Every proxied request then looks like it came from one IP. Per-IP rate limiting collapses to a single shared bucket, and legitimate users can trip 429s together. Per-account limiting still works in that case.

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

SplitDuo can read receipts from a photo and prefill the amount, date, and category. It works with **any OpenAI-compatible endpoint**. Bring your own key and keep your data local.

Uncomment and set these in `docker-compose.yml`:

```yaml
SD_AI_BASE_URL: https://api.openai.com   # or your local LLM endpoint
SD_AI_API_KEY: your-api-key
SD_AI_MODEL: gpt-4o
```

Leave them unset to disable the feature. The app runs fine without it.

### Email (invitations and password reset)

You need these variables only if you want to invite users by email or allow password reset flows:

```yaml
SD_EMAIL_SENDER_NAME: SplitDuo
SD_EMAIL_SENDER_ADDRESS: noreply@yourdomain.com
SD_EMAIL_SMTP_HOST: smtp.yourprovider.com
SD_EMAIL_SMTP_PORT: 587
SD_EMAIL_SMTP_USERNAME: your-smtp-user
SD_EMAIL_SMTP_PASSWORD: your-smtp-password
SD_EMAIL_SSL: "false"   # set "true" for port 465
```

### Push notifications

Users can receive push notifications for group activity (member joined, invitation accepted, recurring expenses). The app disables push unless you provide a VAPID keypair. Generate the keypair **once** and keep it safe. Rotating the keys invalidates all existing subscriptions, and every user must re-enable notifications.

Generate the keypair once with the `web-push` CLI. This requires [Node.js](https://nodejs.org), and you install nothing because `npx` fetches the CLI on the fly. The keys are a plain base64url P-256 keypair. You can also produce them with any other VAPID key generator:

```bash
npx web-push generate-vapid-keys
```

Then set both in `docker-compose.yml`:

```yaml
SD_VAPID_PUBLIC_KEY: your-vapid-public-key
SD_VAPID_PRIVATE_KEY: your-vapid-private-key
# optional contact address push services may use for abuse reports:
# SD_VAPID_SUBJECT: mailto:admin@yourdomain.com
```

Leave them unset to disable the feature. The app runs fine without it.

On startup the app logs one of three messages:

```
Web push enabled (VAPID subject: <subject>)
Web push disabled — set SD_VAPID_PUBLIC_KEY and SD_VAPID_PRIVATE_KEY to enable notifications
Web push misconfigured — SD_VAPID_PUBLIC_KEY/SD_VAPID_PRIVATE_KEY are set but unusable. The public key must be a 65-byte base64url P-256 point; generate a pair with 'npx web-push generate-vapid-keys'. Push stays disabled.
```

A truncated paste or a PEM block usually causes the misconfigured state. Check the message before debugging notifications. The app treats a malformed key as disabled, so the UI shows "disabled by the administrator" rather than offering a toggle that cannot work.

### Public URL

If you serve SplitDuo from a domain (not localhost), set the base URL. Invitation links and emails then point to the right place:

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

Docker Compose keeps the database volume, so your data carries over. EF Core applies any pending migrations automatically on startup.

> **Email-change upgrade check.** The release that adds verified email changes normalizes addresses to lowercase and creates a case-insensitive unique index. If your database contains two live accounts whose addresses differ only by case (`Alice@example.com` and `alice@example.com`), the migration aborts on startup. It never merges such accounts silently. Before upgrading, check with:
>
> ```sql
> SELECT lower(email), count(*) FROM users
> WHERE deleted_at IS NULL
> GROUP BY lower(email)
> HAVING count(*) > 1;
> ```
>
> If it returns rows, rename or merge the duplicates first. The index ignores soft-deleted rows, so you can reuse their addresses.

> If you pinned a specific version tag instead of `latest`, edit the `image:` line in `docker-compose.yml` (e.g. `j1mm0/splitduo:1.11.1`) and rerun the commands above. See the [releases page](https://gitlab.com/j1mm0/splitduo/-/releases) for available tags.

### Testing a release candidate

Release candidates are pre-release builds for testing. They are not stable and not recommended for production.

Edit the `image:` line in `docker-compose.yml` to the `rc` tag and recreate the containers:

```yaml
services:
  splitduo-app:
    image: j1mm0/splitduo:rc
```

```bash
docker compose pull && docker compose up -d
```

The `rc` tag always points at the newest release candidate. To pin a specific one instead, use an immutable tag such as `j1mm0/splitduo:1.17.0-rc.1`. Docker Compose keeps the database volume, so switching an existing instance over keeps your data. To go back to stable, set the `image:` line back to `j1mm0/splitduo:latest` and rerun `docker compose pull && docker compose up -d`.

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

**The app does not start and logs mention the database**
The app waits for PostgreSQL to be healthy before booting. If the healthcheck fails, check `docker compose logs postgres`. The most common cause is a corrupted `postgres_data` volume. Remove it only if you are willing to lose the data: `docker compose down -v && docker compose up -d`.

**I changed `SD_INITIAL_USER_*` but the admin account did not update**
The app creates the initial user only on first startup. To change credentials after that, use the in-app admin panel or reset the database volume.

**Invitation emails are not being sent**
Email is optional, and the default configuration disables it. Set the `SD_EMAIL_*` variables and restart. Check `docker compose logs splitduo-app` for SMTP errors.

**Receipt scanning returns errors**
Confirm that `SD_AI_BASE_URL`, `SD_AI_API_KEY`, and `SD_AI_MODEL` all have values, and that your API key has credit. The app surfaces the upstream error in the UI.

**Port 3000 is already in use**
Edit the `ports` mapping in `docker-compose.yml`. Change `"3000:8080"` to `"YOUR_PORT:8080"`. The app always listens on 8080 inside the container. Only the host port changes.

---

## Going further

- **Full configuration reference**, every environment variable: [`docs/readme/configuration.md`](readme/configuration.md)
- **Backend architecture**, how the API and services are organized: [`docs/architecture/backend-architecture.md`](architecture/backend-architecture.md)
- **Project README**, features, tech stack, and links: [root `README.md`](../README.md)

Questions or issues? Open one on the [GitLab project](https://gitlab.com/j1mm0/splitduo/-/issues) or the [GitHub mirror](https://github.com/c4mbr0nn3/splitduo/issues).