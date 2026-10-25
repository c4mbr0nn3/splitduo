# Contributing to SplitDuo

Thanks for taking the time to contribute. Bug reports, documentation fixes, translations, and code are all welcome.

Development happens on **[GitLab](https://gitlab.com/j1mm0/splitduo)**. The GitHub repository is a read-only mirror — pull requests opened there cannot be merged. File issues and merge requests on GitLab.

## Before you start

- **Bug or small fix** — open an issue using the [Default template](.gitlab/issue_templates/Default.md), then reference it from your branch and merge request.
- **Anything larger** — open the issue first and describe the change you have in mind. It is cheaper to agree on the approach before writing code than after.
- **Security problem** — do not open a public issue. Follow [SECURITY.md](SECURITY.md).
- **Translation** — see [Adding a language](docs/features/i18n/adding-a-language.md). Locale parity is checked at build time, so an incomplete translation fails the frontend build rather than shipping half-translated UI.

## Local development

Prerequisites are the .NET 10 SDK, Node.js 22+, pnpm, and Docker (for PostgreSQL and the integration tests).

```bash
# Backend — http://localhost:8080
cd sd-backend && dotnet run --project SplitDuo.Api

# Frontend — http://localhost:3000, proxies /api to :8080
cd sd-frontend && pnpm install && pnpm dev
```

The default development login is `admin@splitduo.local` / `changeme123`; it only exists in Development. Full setup, migrations, and dev services are in [Development](docs/readme/development.md).

## Tests

Run the checks that cover the area you touched:

```bash
cd sd-backend && dotnet test SplitDuo.Tests.Unit
cd sd-backend && dotnet test SplitDuo.Tests.Integration   # needs Docker
cd sd-frontend && pnpm lint && pnpm typecheck
cd sd-frontend && pnpm test
```

Integration tests use Testcontainers, so they need a running Docker daemon. What is and is not worth testing, and how coverage is aggregated, is in [Testing](docs/readme/testing.md).

## Issues

Use the template at [.gitlab/issue_templates/Default.md](.gitlab/issue_templates/Default.md). Keep it short:

- **Title:** `type: concise description`, using the same types as commits.
- **Sections:** What (1–3 sentences) → Why (one sentence, omit if obvious) → Scope (bullets) → Done when (checklist).

## Merge requests

Branch from `main` as `type/scope-#<issue>`, for example `fix/csv-import-#42`, and open the merge request against `main`. The [Default MR template](.gitlab/merge_request_templates/Default.md) is applied automatically.

- **Title:** `type(scope): message`, the same format as commits.
- **Description:** What (1–3 sentences) → `Closes #<iid>` → Done when (the checklist from the issue).
- Merge requests are squash-merged and the source branch is removed.
- Keep the change focused. Unrelated refactors belong in their own issue.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/), subject only — no body, no footer:

```
type(scope): message
```

Types: `feat`, `fix`, `refactor`, `docs`, `chore`, `style`, `perf`, `test`, `ci`, `build`. Keep the subject under 72 characters and leave off the trailing period. The changelog is generated from these messages, so the type matters.

## Repository layout

```
sd-backend/     .NET 10 solution — SplitDuo.Api (controllers, DTOs), SplitDuo.Core (domain, data, services)
sd-frontend/    Nuxt 4 SPA — Vue 3, Nuxt UI v4, TailwindCSS v4, TypeScript
docs/           self-hosting, architecture, feature and agent documentation
ci/             GitLab CI job definitions
scripts/        release tooling
Dockerfile      multi-stage build: frontend generate -> backend wwwroot -> runtime image
```

The marketing site lives in a separate repository and is not part of this build.

## Code style

- Write for the next reader: plain code that says what it means. Three obvious lines beat one dense line.
- Keep control flow flat and each function step legible on its own.
- Comment the **why** — the decision and its trade-off — not the mechanics the code already shows.
- Leave the surrounding code better than you found it, but keep drive-by rewrites out of a focused change.
- Backend conventions: [Backend guide](docs/agents/backend.md). Frontend conventions: [Frontend guide](docs/agents/frontend.md).

## Licensing

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
