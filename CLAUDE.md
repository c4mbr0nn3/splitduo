# CLAUDE.md

## Project: SplitDuo

Expense splitting app for couples. .NET 10 backend + Vue.js/Nuxt frontend, PostgreSQL database, deployed as single Docker container.

## Quick Start

**Development (local)**:

```bash
# Backend (.NET 10)
cd sd-backend
dotnet restore
dotnet run --project SplitDuo.Api  # Runs on http://localhost:5000

# Frontend (Nuxt)
cd sd-frontend
npm install
npm run dev  # Runs on http://localhost:3000

# Database migrations
cd sd-backend
dotnet ef migrations add <MigrationName> --project SplitDuo.Api
dotnet ef database update --project SplitDuo.Api
```

**Docker (production)**:

```bash
docker compose up -d        # Start app + postgres
docker compose down         # Stop services
docker compose logs -f      # View logs
```

Access at `http://localhost:3000`. Default login: `admin@splitduo.local` / `changeme123`

## Architecture

**Two-project structure**:

- `SplitDuo.Api` - REST API with vertical slice architecture (Features/)
- `SplitDuo.Core` - Domain models, services, data access

**Vertical slices in Features/**:

- Each feature has Controllers/ and Dto/ subdirectories
- Authentication, Users, Groups, Expenses, Settlements, Categories, PaymentModes, Import, Export

**Deployment**:

- Multi-stage Docker build: frontend → backend → runtime
- Frontend builds to static files served from wwwroot by .NET
- Single container exposes port 8080 (maps to host 3000)

## Environment Variables

**Required in production**:

- `SD_JWT_SECRET_KEY` - Change from default!
- `SD_INITIAL_USER_PASSWORD` - Change from default!

**Database** (docker-compose.yml):

- `SD_DB_HOST`, `SD_DB_PORT`, `SD_DB_NAME`, `SD_DB_USERNAME`, `SD_DB_PASSWORD`

**Optional**:

- Email SMTP settings for notifications

## Common Gotchas

- **Port mapping**: Container runs on 8080, host maps to 3000
- **Frontend builds into backend**: `npm run generate` creates static files copied to wwwroot
- **Database connection**: .NET connects to `postgres` hostname (Docker network), not localhost
- **No tests yet**: No test projects exist in the codebase
- **EF migrations**: Always run from SplitDuo.Api project, not Core
- **Version management**: VERSION file read by GitLab CI for Docker tags

## Docs

Extensive docs in `docs/`:

- `backend_architecture.md` - Detailed architecture patterns
- `splitduo_project_spec.md` - Full specification
- `rest_api_structure.md` - API endpoints
- `frontend_architecture.md` - Vue/Nuxt structure

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
