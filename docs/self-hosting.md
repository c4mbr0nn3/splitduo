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

## Quick start (3 commands)

The compose file pulls the app image from [Docker Hub](https://hub.docker.com/r/j1mm0/splitduo) — no build step.

```bash
git clone https://gitlab.com/j1mm0/splitduo.git
cd splitduo
docker compose up -d
```

Open `http://localhost:3000` and log in with:

- **Email:** `admin@splitduo.local`
- **Password:** `changeme123`

You're running SplitDuo. The next section shows how to make it production-safe before you actually use it.

---

## Before you rely on it: change the defaults

The bundled `docker-compose.yml` ships with placeholder secrets. **Change these before real use** — they protect your JWT tokens and your admin account.

Open `docker-compose.yml` and edit the `splitduo-app` environment block:

```yaml
  splitduo-app:
    environment:
      # ...database config above...

      # Initial admin user — created on first startup only
      SD_INITIAL_USER_EMAIL: you@yourdomain.com
      SD_INITIAL_USER_PASSWORD: a-long-random-password

      # JWT signing key — generate a strong random string
      SD_JWT_SECRET_KEY: a-long-random-string-at-least-32-chars
```

Generate a strong JWT secret with, for example:

```bash
openssl rand -base64 48
```

Then recreate the containers so the new values take effect:

```bash
docker compose down
docker compose up -d
```

> The initial admin user is only created on the **first** startup. If you already booted with the defaults and want to change the admin credentials, change them from the in-app admin panel (or reset the database volume and start fresh).

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