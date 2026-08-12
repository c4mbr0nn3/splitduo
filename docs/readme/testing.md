# Testing

## Integration tests

Integration tests live in `sd-backend/SplitDuo.Tests.Integration` and run the full API against a real PostgreSQL 17 container via [Testcontainers](https://dotnet.testcontainers.org/) + podman. No Docker daemon required — only podman.

**Prerequisites:**

- podman installed and the user socket running:
  ```bash
  systemctl --user start podman.socket   # one-time per session
  podman info --format '{{.Host.RemoteSocket.Path}}'  # verify: unix:///run/user/<uid>/podman/podman.sock
  ```
- .NET 10 SDK

**Run the tests:**

```bash
cd sd-backend
./run-integration-tests.sh
```

The script sets the podman socket env vars (`DOCKER_HOST`, `TESTCONTAINERS_RYUK_CONTAINER_PRIVILEGED`) and the test app config (`SD_JWT_SECRET_KEY`, `SD_JWT_ISSUER`, `SD_JWT_AUDIENCE`, `SD_SEED_DEMO_DATA=false`), then runs `dotnet test`. A fresh `postgres:17-alpine` container is spun up per test run and torn down after.

**What's covered:** Auth, Groups, Group Members, Expenses, Balances, Aliases, Invitations, Two-Factor, Imports, Categories & Payment Modes, User Settings, Users Profile, and i18n — all against the real API via `WebApplicationFactory` (`SplitDuoApiFactory.cs`) with [Respawn](https://github.com/jbogard/Respawn) for fast test isolation.

**Troubleshooting:**

- `Could not connect to Docker daemon` → the podman socket isn't running. Start it with `systemctl --user start podman.socket`.
- `Ryuk container failed to start` → rootless podman needs privileged Ryuk. The script sets `TESTCONTAINERS_RYUK_CONTAINER_PRIVILEGED=true`; if it still fails, add `export TESTCONTAINERS_RYUK_DISABLED=true` to skip the resource reaper.
- Tests hang on startup → first container pull takes ~30s; subsequent runs reuse the cached image.

## Unit tests

Backend unit tests live in `sd-backend/SplitDuo.Tests.Unit` and cover localization setup, supported-languages resolution, and email template rendering.

## Frontend tests

Frontend tests use [Vitest](https://vitest.dev/) and live alongside source files (`*.test.ts`). They cover composables (auth, 2FA, resource composables), utilities (currency, date, JWT, enum, user roles), and key components (expense form, date picker, import mapping, expense filters).

```bash
cd sd-frontend
pnpm test
```

## Coverage

Both backend test projects collect coverage via [coverlet](https://github.com/coverlet-coverage/coverlet). The `run-coverage.sh` wrapper runs unit + integration tests with coverage and aggregates the results into an HTML report.

```bash
# One-time: install the report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

cd sd-backend
./run-coverage.sh
xdg-open TestResults/coverage-report/index.html
```

Raw `coverage.cobertura.xml` files are written under `TestResults/` (gitignored). The script wipes `TestResults/` each run so reports never merge stale data.