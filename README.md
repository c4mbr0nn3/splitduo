# SplitDuo

Shared finances, without the noise. SplitDuo is a self-hosted expense splitting app for small groups — couples, housemates, travel companions, or anyone sharing costs. No subscription, no third-party servers, no data you don't control.

A focused alternative to Splitwise and Cospend, deployable in one `docker compose up`.

---

<details>
<summary>Screenshots</summary>

<br>

![Login](docs/screenshots/login.png)

![Dashboard](docs/screenshots/dashboard.png)

![Groups](docs/screenshots/group_list.png)

![Group Detail](docs/screenshots/group_page.png)

![Add Expense](docs/screenshots/add_expense.png)

</details>

---

## Features

**Expense tracking that stays out of your way**
Add expenses, pick a split, move on. SplitDuo handles the arithmetic — proportional splits, unequal amounts, custom breakdowns — and keeps a live balance so everyone always knows where they stand.

**Settlement-aware balances**
Rather than a raw transaction log, SplitDuo computes net balances across the whole group and surfaces clear settlement suggestions — minimizing the number of transfers needed to settle up.

**AI-powered receipt scanning**
Point your camera at a receipt and SplitDuo prefills the amount, date, and category. Works with any OpenAI-compatible endpoint — bring your own key, keep your data local.

**Mobile-first, installable**
The interface is designed for a phone screen first. Add it to your home screen as a PWA and it behaves like a native app — no app store required.

**Invite by email**
Add your partner via a secure, time-limited invitation link. No account needed on their end until they accept.

**Data portability**
Import existing data from Cospend or a SplitDuo CSV export. Export at any time. No lock-in.

**Two-factor authentication**
Each user can independently enable TOTP-based 2FA on their account.

**Fully self-hosted**
Single Docker container. Your server, your database, your rules.

---

## Quick Start

Requires Docker and Docker Compose.

```bash
git clone https://github.com/yourusername/splitduo.git
cd splitduo
docker compose up -d
```

Open `http://localhost:3000` — default login is `admin@splitduo.local` / `changeme123`.

> **Before going to production**: set `SD_JWT_SECRET_KEY` and `SD_INITIAL_USER_PASSWORD` to something you control.

---

## Configuration

Edit `docker-compose.yml` or pass environment variables directly.

### Required for production

```yaml
SD_JWT_SECRET_KEY: your-secret-key
SD_INITIAL_USER_EMAIL: admin@splitduo.local
SD_INITIAL_USER_PASSWORD: changeme123
```

### Database

```yaml
SD_DB_HOST: postgres
SD_DB_NAME: splitduo
SD_DB_USERNAME: splitduo
SD_DB_PASSWORD: splitduo
```

### Application

```yaml
SD_BASE_URL: http://localhost:3000
SD_ENVIRONMENT: Production
```

### AI / Receipt Scanning (optional)

Any OpenAI-compatible endpoint works.

```yaml
SD_AI_BASE_URL: https://api.openai.com
SD_AI_API_KEY: your-api-key
SD_AI_MODEL: gpt-4o
```

### Email (optional)

Required for invitation emails and password reset.

```yaml
SD_EMAIL_SMTP_HOST: localhost
SD_EMAIL_SMTP_PORT: 587
SD_EMAIL_SMTP_USERNAME: ""
SD_EMAIL_SMTP_PASSWORD: ""
SD_EMAIL_SSL: "false"
```

---

## Tech Stack

| Layer      | Technology                                  |
| ---------- | ------------------------------------------- |
| Backend    | .NET 10, Entity Framework Core              |
| Frontend   | Vue 3, Nuxt 4, Nuxt UI, TailwindCSS         |
| Database   | PostgreSQL 17                               |
| Deployment | Single Docker container (multi-stage build) |

The frontend is compiled to static files and served directly by the .NET backend — one container, one port, no reverse proxy needed.

---

## Development

### Backend

```bash
cd sd-backend
dotnet restore
dotnet run --project SplitDuo.Api
# → http://localhost:5000
```

### Frontend

```bash
cd sd-frontend
pnpm install
pnpm dev
# → http://localhost:3000 (proxies API to :8080)
```

### Dev services (PostgreSQL + Mailpit)

```bash
./scripts/dev.sh           # Start both
./scripts/dev.sh postgres  # PostgreSQL only
./scripts/dev.sh mailpit   # Mailpit only
./scripts/dev.sh -d        # Drop and recreate volumes first
```

Mailpit (email preview): `http://localhost:8025`

### Database migrations

```bash
cd sd-backend
dotnet ef migrations add <MigrationName> --project SplitDuo.Core --startup-project SplitDuo.Api
dotnet ef database update --project SplitDuo.Core --startup-project SplitDuo.Api
```

---

## API

Base URL: `http://localhost:3000/api/v1`

Full spec: [OpenAPI YAML](docs/api/splitduoapi-v1.yaml)

---

## Documentation

- [Project Specification](docs/project-spec.md)
- [System Architecture](docs/architecture/system-architecture.md)
- [Backend Architecture](docs/architecture/backend-architecture.md)
- [Frontend Architecture](docs/architecture/frontend-architecture.md)
- [Database Schema](docs/database/database-schema.dbml)

---

## License

[MIT](LICENSE)
