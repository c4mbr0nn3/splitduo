# .NET / NuGet upgrade notes

Consult this when bumping backend packages.

## Where things live

- One solution: `sd-backend/sd-backend.sln`, holding four projects — `SplitDuo.Core`, `SplitDuo.Api`, `SplitDuo.Tests.Unit`, `SplitDuo.Tests.Integration`. Run `dotnet` commands from `sd-backend/`.
- Packages are declared per-project in the `.csproj` files; there is no `Directory.Packages.props` central management and no `packages.lock.json`, so a bump is an edit to `.csproj` followed by `dotnet restore`.
- Migrations: `dotnet ef migrations add <Name> --project SplitDuo.Core --startup-project SplitDuo.Api`, run from the `SplitDuo.Api` project.

## Gates

| Step | Command | Notes |
|---|---|---|
| Build | `dotnet build` | Fast, catches compile breakage — the floor before every commit (per `docs/agents/backend.md`). |
| Unit tests | `dotnet test SplitDuo.Tests.Unit/SplitDuo.Tests.Unit.csproj` | No container needed. The project has no `InternalsVisibleTo` — keep test seams public. |
| Integration tests | `./run-integration-tests.sh` | Testcontainers PostgreSQL (`postgres:17-alpine`); needs Docker or rootless Podman. The script sets `DOCKER_HOST` and the Ryuk privileged flag, then runs `dotnet test SplitDuo.Tests.Integration/...`. |

Unit tests are not listed in `docs/agents/backend.md`; the invocation above is the one CI uses (`ci/verify.yml`). Prefer the explicit project path over a solution-wide `dotnet test`, which drags in the containerized integration project.

## Risk flags

- **`PrivateAssets` / `IncludeAssets`** — `Microsoft.EntityFrameworkCore.Design` and similar are design-time only. Keep `PrivateAssets="all"` intact when bumping, or the package leaks into the app's runtime graph.
- **EF Core + Npgsql majors** — a bump can change query translation or migration behavior without failing the build. Treat majors here as breaking and run the integration suite.
- **OpenAPI cascade** — the backend's OpenAPI output feeds the frontend types. After a bump that changes the spec, regenerate: run the backend in dev, export from `http://localhost:8080/scalar/v1`, copy into `docs/api/splitduoapi-v1.yaml`, then `pnpm gen:api` in `sd-frontend/`. Never hand-edit the spec or `app/types/api.d.ts`.
