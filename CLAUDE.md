# CLAUDE.md

## Project: SplitDuo

Expense splitting app for couples. .NET 10 backend + Nuxt 4 frontend, PostgreSQL, deployed as single Docker container.

See `sd-backend/CLAUDE.md` and `sd-frontend/CLAUDE.md` for detailed codebase docs.

## Quick Start

```bash
# Backend — http://localhost:5000
cd sd-backend && dotnet restore && dotnet run --project SplitDuo.Api

# Frontend — http://localhost:3000 (proxies API to backend on :8080)
cd sd-frontend && pnpm install && pnpm dev

# Docker (production) — http://localhost:3000
docker compose up -d
```

Default login: `admin@splitduo.local` / `changeme123`

## Repo Layout

```
sd-backend/          # .NET 10 — SplitDuo.Api + SplitDuo.Core
sd-frontend/         # Nuxt 4 SPA — Vue 3, Nuxt UI v4, TailwindCSS v4
docs/                # Architecture, API, database, feature, and migration docs
docker-compose.yml   # App + PostgreSQL
Dockerfile           # Multi-stage: frontend build → backend build → runtime
VERSION              # Read by CI for Docker tags
```

## Deployment

- Multi-stage Docker build: frontend `npm run generate` → static files copied to backend `wwwroot` → single .NET container
- Container runs on port 8080, host maps to 3000
- .NET serves both API (`/api/v1/`) and frontend static files (fallback to `index.html`)
- Auto-migrates database on startup
- Seeds initial admin user if DB is empty

## Environment Variables

| Variable | Required | Purpose |
|---|---|---|
| `SD_JWT_SECRET_KEY` | Yes (prod) | JWT signing key |
| `SD_INITIAL_USER_PASSWORD` | Yes (prod) | Initial admin password |
| `SD_DB_HOST/PORT/NAME/USERNAME/PASSWORD` | Yes | PostgreSQL connection |
| `SD_SMTP_*` | No | Email notifications (SMTP) |
| `SD_AI_BASE_URL` | No | AI provider base URL (enables receipt scanning) |
| `SD_AI_API_KEY` | No | AI provider API key |
| `SD_AI_MODEL` | No | AI model name (e.g. `gpt-4o`) |
| `NUXT_PUBLIC_APP_VERSION` | No | Version shown in frontend UI |

## Common Gotchas

- **Frontend → backend in dev**: Frontend dev server hits `http://localhost:8080/api/v1`, not 5000
- **EF migrations**: Always run from `SplitDuo.Api` project, not Core
- **No tests**: No test projects exist yet
- **DB hostname in Docker**: .NET connects to `postgres` (Docker network), not localhost

## Docs

```
docs/
├── architecture/    # System, backend, frontend architecture
├── api/             # REST endpoints, DTOs, OpenAPI spec, composables
├── database/        # Schema (DBML), index suggestions
├── features/        # 2FA, CSV import implementation docs
├── migration/       # Cospend mapping
└── project-spec.md  # Full project specification
```

---

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:

- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.
