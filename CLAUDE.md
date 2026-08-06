# CLAUDE.md

## Project: SplitDuo

Expense splitting app for small groups — couples, housemates, travel companions, or anyone sharing costs. .NET 10 backend + Nuxt 4 frontend, PostgreSQL, deployed as single Docker container.

See `docs/agents/backend.md` and `docs/agents/frontend.md` for detailed codebase docs.

## Quick Start

- **Backend**: `cd sd-backend && dotnet run --project SplitDuo.Api` → http://localhost:8080
- **Frontend**: `cd sd-frontend && pnpm install && pnpm dev` → http://localhost:3000 (proxies API to :8080)
- **Docker**: `docker compose up -d` → http://localhost:3000
- Default login: `admin@splitduo.local` / `changeme123` (docker-compose); `admin@localhost` / `changeme` (bare dotnet run)

## Repo Layout

- `sd-backend/` — .NET 10: `SplitDuo.Api` (controllers/DTOs) + `SplitDuo.Core` (entities/data/services) + test projects
- `sd-frontend/` — Nuxt 4 SPA: Vue 3, Nuxt UI v4, TailwindCSS v4, TypeScript (strict)
- `Dockerfile` — multi-stage: frontend `pnpm generate` → static files into backend `wwwroot` → single container on :8080
- `docker-compose.yml` — app + PostgreSQL 17
- `docs/agents/` — detailed guides loaded on demand (see Task Routing below)
- Release tooling (`scripts/`, `.versionrc`, `cliff.toml`, `VERSION`, `CHANGELOG.md`) — see `docs/agents/release.md`

## Required Env Vars (production)

`SD_DB_HOST/PORT/NAME/USERNAME/PASSWORD`, `SD_JWT_SECRET_KEY`, `SD_INITIAL_USER_EMAIL/PASSWORD`.
All others optional — see `sd-backend/SplitDuo.Core/Options/Setup/` for full list.

## Gotchas

- Frontend dev proxies to `:8080`, not `:5000` — backend runs on 8080 (see `launchSettings.json`)
- EF migrations: run from `SplitDuo.Api` project (`--startup-project SplitDuo.Api`)
- Test projects exist (`SplitDuo.Tests.Unit`, `SplitDuo.Tests.Integration`) but have no tests yet
- DB hostname in Docker: `postgres` (Docker network), not localhost
- Release + CI gotchas: see `docs/agents/release.md` and `docs/agents/ci.md` — read before releasing or touching CI.

## Task Routing

Before modifying these areas, read the corresponding guide:

| Work area | Read first |
|---|---|
| `sd-backend/**` | `docs/agents/backend.md` |
| `sd-frontend/**` | `docs/agents/frontend.md` |
| releases, `VERSION`, `CHANGELOG.md` | `docs/agents/release.md` |
| `ci/*.yml`, `.gitlab-ci.yml` | `docs/agents/ci.md` |

- Before modifying `sd-backend/`, read `docs/agents/backend.md`.
- Before modifying `sd-frontend/`, read `docs/agents/frontend.md`.
- Before releasing or bumping versions, read `docs/agents/release.md`.
- Before touching CI, read `docs/agents/ci.md`.

---

## Rules

### Core Rules

- If a task matches a skill, you MUST invoke it
- Skills are located in `skills/<skill-name>/SKILL.md`
- Never implement directly if a skill applies
- Always follow the skill instructions exactly (do not partially apply them)

### Intent → Skill Mapping

The agent should automatically map user intent to skills:

- Feature / new functionality → `spec-driven-development`, then `incremental-implementation`, `test-driven-development`
- Planning / breakdown → `planning-and-task-breakdown`
- Bug / failure / unexpected behavior → `debugging-and-error-recovery`
- Code review → `code-review-and-quality`
- Refactoring / simplification → `code-simplification`
- API or interface design → `api-and-interface-design`
- UI work → `frontend-ui-engineering`

### Lifecycle Mapping (Implicit Commands)

OpenCode does not support slash commands like `/spec` or `/plan`.

Instead, the agent must internally follow this lifecycle:

- DEFINE → `spec-driven-development`
- PLAN → `planning-and-task-breakdown`
- BUILD → `incremental-implementation` + `test-driven-development`
- VERIFY → `debugging-and-error-recovery`
- REVIEW → `code-review-and-quality`
- SHIP → `shipping-and-launch`

### Execution Model

For every request:

1. Determine if any skill applies (even 1% chance)
2. Invoke the appropriate skill using the `skill` tool
3. Follow the skill workflow strictly
4. Only proceed to implementation after required steps (spec, plan, etc.) are complete

### Anti-Rationalization

The following thoughts are incorrect and must be ignored:

- "This is too small for a skill"
- "I can just quickly implement this"
- "I’ll gather context first"

Correct behavior:

- Always check for and use skills first

### Commit Guidelines

- Conventional Commits, subject only: `type(scope): message` (≤72 chars, no trailing period)
- Types: `feat`, `fix`, `refactor`, `docs`, `chore`, `style`, `perf`, `test`, `ci`, `build`
- No body, no footer. **Never commit or push without explicit user approval.**

### Issue Guidelines

- Use the template at `.gitlab/issue_templates/Default.md` (available in GitLab's "Open" dropdown)
- Title: `type: concise description` (same types as commits)
- Sections: **What** (1-3 sentences) → **Why** (1 sentence, omit if obvious) → **Scope** (bullets) → **Done when** (checklist)
- Side project — keep it short. Delete sections that aren't needed. One-liners are fine.

### GitLab MCP

- The `gitlab` MCP server (`@zereight/mcp-gitlab`) is configured in `opencode.json` and authenticated via the `GITLAB_PERSONAL_ACCESS_TOKEN` env var (project access token, `api` scope)
- Use GitLab MCP tools for issues, MRs, pipelines, branches, files, labels — not the CLI
- MRs that close an issue use `Closes #<iid>` in the description; prefer squash-on-merge for feature branches

### Keep CLAUDE.md in Sync

- After implementing new conventions, patterns, composables, helpers, or reusable project guidelines, check `CLAUDE.md` (root + `sd-backend/` + `sd-frontend/`) and add a rule/hint if the change should be repeated by future work.
- Only add a rule when the change establishes a reusable guideline — not for one-off feature code.
- Keep entries brief: one line per rule, matching the existing style of the relevant section.