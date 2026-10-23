# Development

## Backend

```bash
cd sd-backend
dotnet restore
dotnet run --project SplitDuo.Api
# → http://localhost:8080
```

## Frontend

```bash
cd sd-frontend
pnpm install
pnpm dev
# → http://localhost:3000 (proxies API to :8080)
```

## Dev services (PostgreSQL + Mailpit)

```bash
./scripts/dev.sh           # Start both
./scripts/dev.sh postgres  # PostgreSQL only
./scripts/dev.sh mailpit   # Mailpit only
./scripts/dev.sh -d        # Drop and recreate volumes first
```

Mailpit (email preview): `http://localhost:8025`

## Database migrations

```bash
cd sd-backend
dotnet ef migrations add <MigrationName> --project SplitDuo.Core --startup-project SplitDuo.Api
dotnet ef database update --project SplitDuo.Core --startup-project SplitDuo.Api
```

> Migrations must be run from the `SplitDuo.Api` project (`--startup-project SplitDuo.Api`).

## Default logins

**Development only** — these credentials are not valid with the Docker Compose quick start, which runs in Production and therefore requires `SD_INITIAL_USER_PASSWORD` (set in a `.env` file next to the compose file).

- Bare `dotnet run` (Development): `admin@splitduo.local` / `changeme123`