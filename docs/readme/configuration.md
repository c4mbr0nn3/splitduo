# Configuration

Edit `docker-compose.yml` or pass environment variables directly.

## Required for production

```yaml
SD_JWT_SECRET_KEY: your-secret-key
SD_INITIAL_USER_EMAIL: admin@splitduo.local
SD_INITIAL_USER_PASSWORD: changeme123
```

## Database

```yaml
SD_DB_HOST: postgres
SD_DB_NAME: splitduo
SD_DB_USERNAME: splitduo
SD_DB_PASSWORD: splitduo
```

## Application

```yaml
SD_BASE_URL: http://localhost:3000
ASPNETCORE_ENVIRONMENT: Production
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
SD_EMAIL_SMTP_HOST: localhost
SD_EMAIL_SMTP_PORT: 587
SD_EMAIL_SMTP_USERNAME: ""
SD_EMAIL_SMTP_PASSWORD: ""
SD_EMAIL_SSL: "false"
```

---

For the full list of available environment variables, see `sd-backend/SplitDuo.Core/Options/Setup/`.