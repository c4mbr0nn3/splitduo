# Configuration

Edit `docker-compose.yml` or pass environment variables directly.

## Required for production

```yaml
SD_JWT_SECRET_KEY: your-secret-key
SD_INITIAL_USER_EMAIL: admin@splitduo.local
SD_INITIAL_USER_PASSWORD: changeme123
```

## JWT (optional)

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

## Initial admin user

Created on first startup only. First/last name and demo-data seeding are optional.

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