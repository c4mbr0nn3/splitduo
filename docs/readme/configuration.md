# Configuration

Edit `.env` next to the compose file (Compose interpolates it), or pass environment variables directly.

## Required for production

Generate secrets with, e.g., `openssl rand -base64 24`.

```yaml
SD_JWT_SECRET_KEY: <generate a strong random string>
SD_INITIAL_USER_EMAIL: admin@splitduo.local
SD_INITIAL_USER_PASSWORD: <generate: openssl rand -base64 24>   # required, min 15 chars
```

Set `SD_JWT_SECRET_KEY` **before** anyone enrols in two-factor authentication: the TOTP encryption key is derived from it, so changing it later invalidates existing authenticator enrolments. Keep it with your backups.

## JWT tuning (optional)

Defaults come from `appsettings.json`. Override only if you need custom claims or a different expiry.

```yaml
SD_JWT_ISSUER: ""        # default: empty
SD_JWT_AUDIENCE: ""      # default: empty
SD_JWT_EXPIRES: 15       # minutes, default: 15
```

## Database

```yaml
SD_DB_HOST: postgres
SD_DB_PORT: 5432
SD_DB_NAME: splitduo
SD_DB_USERNAME: splitduo
SD_DB_PASSWORD: splitduo
```

## Application

```yaml
SD_BASE_URL: http://localhost:3000
ASPNETCORE_ENVIRONMENT: Production
```

## Reverse proxy (optional, but required for per-IP rate limiting behind a proxy)

```yaml
SD_KNOWN_PROXIES: 10.0.0.5,192.168.1.0/24   # trusted proxy IPs and/or CIDRs, comma-separated
```

Unset (default): `X-Forwarded-For` is ignored (fail-closed). Behind a reverse proxy you **must** set this (and the proxy must forward `X-Forwarded-For`), or per-IP rate limiting on auth endpoints collapses to one shared bucket.

## Initial admin user

Created on first startup only. In Production, `SD_INITIAL_USER_PASSWORD` is **required** — it must be at least 15 characters and not a known-weak value, otherwise the app refuses to seed the admin. In Development it falls back to the documented development default (see `docs/readme/development.md`). First/last name and demo-data seeding are optional.

```yaml
SD_INITIAL_USER_FIRSTNAME: Admin      # default: Super
SD_INITIAL_USER_LASTNAME: User       # default: Admin
SD_SEED_DEMO_DATA: "false"           # set "true" to seed sample data on startup
```

## AI / Receipt Scanning (optional)

Any OpenAI-compatible endpoint works.

```yaml
SD_AI_BASE_URL: https://api.openai.com
SD_AI_API_KEY: your-api-key
SD_AI_MODEL: gpt-4o
```

## Email (optional)

Required for invitation emails and password reset.

```yaml
SD_EMAIL_SENDER_NAME: SplitDuo
SD_EMAIL_SENDER_ADDRESS: noreply@splitduo.app
SD_EMAIL_SMTP_HOST: localhost
SD_EMAIL_SMTP_PORT: 1025
SD_EMAIL_SMTP_USERNAME: ""
SD_EMAIL_SMTP_PASSWORD: ""
SD_EMAIL_SSL: "false"
```

---

For the full list of available environment variables, see `sd-backend/SplitDuo.Core/Options/Setup/`.