<p align="center">
  <img src="docs/logo.png" alt="SplitDuo logo" width="120">
</p>

# SplitDuo

[![pipeline status](https://gitlab.com/j1mm0/splitduo/badges/main/pipeline.svg)](https://gitlab.com/j1mm0/splitduo/-/pipelines)
[![coverage report](https://gitlab.com/j1mm0/splitduo/badges/main/coverage.svg)](https://gitlab.com/j1mm0/splitduo/-/graphs/main/charts)
[![latest release](https://img.shields.io/gitlab/v/release/j1mm0/splitduo?label=latest%20release)](https://gitlab.com/j1mm0/splitduo/-/releases)
[![docker pulls](https://img.shields.io/docker/pulls/j1mm0/splitduo)](https://hub.docker.com/r/j1mm0/splitduo)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Self-hosted expense splitting for couples, housemates, and travel companions. It runs as one Docker container against your PostgreSQL, with no subscription and no vendor account.

**Canonical repository:** [gitlab.com/j1mm0/splitduo](https://gitlab.com/j1mm0/splitduo), where development, issues, and releases happen. [github.com/c4mbr0nn3/splitduo](https://github.com/c4mbr0nn3/splitduo) is a read-only mirror; please file issues and merge requests on GitLab.

The [website](https://splitduoapp.netlify.app) has a [getting-started guide](https://splitduoapp.netlify.app/getting-started), a [feature comparison](https://splitduoapp.netlify.app/#compare), and an [FAQ](https://splitduoapp.netlify.app/#faq).

<p align="center">
  <img src="docs/screenshots/screenshot-hero.png" alt="SplitDuo group view for a shared trip: net balance, who owes whom, group total, and the expense list" width="820">
</p>

## Contents

- [Why SplitDuo](#why-splitduo)
- [Features](#features)
- [How it compares](#how-it-compares)
- [Screenshots](#screenshots)
- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Running in production](#running-in-production)
- [Project status](#project-status)
- [Tech stack](#tech-stack)
- [Documentation](#documentation)
- [Release candidates](#release-candidates)
- [Contributing](#contributing)
- [Support](#support)
- [Security](#security)
- [License](#license)

## Why SplitDuo

SplitDuo started as a private tool for two people. I was splitting shared costs with my girlfriend in Splitwise and kept hitting its limits: I enter a weekend's expenses in one batch, which the free tier handles badly. I tried Cospend next, then realised I was running a whole Nextcloud instance for one app and using almost nothing else in it.

So I built the splitter we wanted: a single container next to a PostgreSQL database, with no cap on how many expenses we could enter. It has since grown past that original use, with recurring expenses, alias mode for couples sharing one slot, receipt scanning, and group statistics.

## Features

- **Splits.** Equal, exact amounts, proportional shares, or a custom breakdown, with live balances for the group.
- **Settling up.** Debts are collapsed into the fewest transfers needed to settle the group.
- **Recurring expenses.** Templates run weekly, monthly, or on a custom schedule. Pause one and choose whether missed periods are backfilled or skipped when it resumes, and require approval before generated expenses count toward balances.
- **Alias mode.** Group members into named aliases (a couple sharing one slot, a household treated as one) and split by subgroup. Balances settle at alias level.
- **Statistics.** Category, monthly, and per-member charts for each group.
- **Receipt scanning.** A photo prefills the amount, date, and category, and is attached to the expense. Works with any OpenAI-compatible endpoint. Needs AI configuration.
- **Attachments.** Receipts, invoices, or any other file, stored with the expense and downloadable later.
- **Import and export.** Cospend, Splitwise, and SplitDuo CSV, including the three-section format used by alias-mode groups. Export whenever you like.
- **Email.** Invitation links, expense and group notifications, 2FA codes, and password resets, in English and Italian. Needs SMTP; invitations also use `SD_BASE_URL`.
- **Two-factor authentication.** TOTP with backup codes, enabled per user.
- **Installable PWA.** The interface is built for a phone first and can be added to a home screen.
- **Two languages.** English and Italian, per user, with locale parity enforced at build time.
- **Admin panel.** User and invitation management with role-based access.

The optional features are configured through environment variables; see [Configuration](docs/readme/configuration.md).

## How it compares

| Capability | SplitDuo | Splitwise | Cospend |
| --- | --- | --- | --- |
| Open source | Yes | No | Yes |
| Self-hosted | Yes | No | Needs Nextcloud |
| Free | Yes | Freemium | Yes |
| No vendor account required | Yes | No | Yes |
| AI receipt scanning | Yes | Pro only | No |
| Mobile app | PWA | Native | No |
| CSV import and export | Yes | Export only | Yes |
| Two-factor authentication | Yes | Fingerprint only | Via Nextcloud |
| Multi-language | Yes | Yes | Yes |

The [full comparison](https://splitduoapp.netlify.app/#compare) is on the website. SplitDuo is not affiliated with Splitwise or Cospend.

## Screenshots

<p align="center">
  <img src="docs/screenshots/screenshot-dashboard-light.png" alt="SplitDuo group view on desktop in the light theme: balance cards, group total, settle-up prompt, and expense list" width="560">
  <img src="docs/screenshots/screenshot-mobile-dark.png" alt="The same SplitDuo group view on a phone in the dark theme, with stacked balance cards and the expense list" width="200">
</p>

<p align="center"><sub>The same group on desktop (light) and mobile (dark).</sub></p>

## Requirements

- **Docker with Compose v2.** The commands below use `docker compose`. The bundled file relies on `depends_on: condition: service_healthy`, which legacy `docker-compose` v1 does not handle the same way.
- **`linux/amd64` or `linux/arm64`.** Every image tag is published as a multi-architecture manifest, so the same tag runs on x86 servers, ARM NAS boxes, and Raspberry Pi boards with a 64-bit OS.
- **Roughly 1 GB RAM and 10 GB disk** for a household. Disk use grows with attachments and the database.
- **A reverse proxy for TLS** whenever the instance is reachable from anywhere but localhost. The app serves plain HTTP and does not terminate TLS itself.
- **PostgreSQL** comes with the compose stack. Pointing at an external database is optional through the `SD_DB_*` variables.

## Quick start

The commands assume a POSIX shell (macOS, Linux, or WSL).

```bash
git clone https://gitlab.com/j1mm0/splitduo.git
cd splitduo
printf 'SD_INITIAL_USER_PASSWORD=%s\nSD_JWT_SECRET_KEY=%s\n' \
  "$(openssl rand -base64 24)" "$(openssl rand -base64 48)" > .env
docker compose up -d
```

Open `http://localhost:3000` and log in as `admin@splitduo.local` with the password in `.env` (`grep SD_INITIAL_USER_PASSWORD .env`).

> **Set both secrets before the first start.** Compose reads them from `.env`. `SD_INITIAL_USER_PASSWORD` only seeds the admin user on first boot; later changes belong in the admin panel. `SD_JWT_SECRET_KEY` also derives the key that encrypts TOTP secrets, so changing it after someone has enrolled in two-factor auth invalidates their authenticator until they use a backup code or enrol again. Generate it once and keep it.

`:latest` tracks the newest stable release. Pin a version on a long-lived server: replace the `image:` line with a tag from [Releases](https://gitlab.com/j1mm0/splitduo/-/releases), such as `j1mm0/splitduo:1.16.0`.

## Running in production

- **Terminate TLS in front of the app.** SplitDuo serves plain HTTP. Put it behind a reverse proxy that handles HTTPS and security headers, and do not expose the container directly to the internet; see [Running on a server](docs/self-hosting.md#running-on-a-server-not-just-localhost). Camera-based receipt scanning also needs a secure origin, and browsers make an exception only for localhost.
- **Set `SD_BASE_URL`** to your public URL, so invitation, password-reset, and notification links point at your domain instead of localhost.
- **Back up the volumes.** Both `postgres_data` and `app_logs` hold state. The dump and restore commands are in [Where your data lives](docs/self-hosting.md#where-your-data-lives).
- **Upgrade deliberately.** Take a backup, then `docker compose pull` and `docker compose up -d`.
- **Behind a proxy, set `SD_KNOWN_PROXIES`.** Without it, per-IP rate limiting sees only the proxy's address and collapses into one shared bucket. See [Per-IP rate limiting behind a proxy](docs/self-hosting.md#per-ip-rate-limiting-behind-a-proxy).
- **Monitor `/health`** for readiness, which includes the database, and `/health/live` for liveness, which does not. A system admin token unlocks `/health/details` for the full per-check report.

The [self-hosting guide](docs/self-hosting.md) covers secrets, proxy options, health endpoints, optional AI and SMTP setup, and troubleshooting.

## Project status

SplitDuo is maintained by one person. There is no company or hosted service behind it, and no SLA. Releases follow semantic versioning, and 1.x is stable for household and small-group use; read the release notes before upgrading, since minor versions can change behaviour. Security fixes ship on the latest stable release only.

Development is AI-assisted. Coding agents write much of the implementation; the maintainer reviews it, sets the architecture and product direction, and cuts every release.

## Tech stack

| Layer | Technology |
| --- | --- |
| Backend | .NET 10, Entity Framework Core 10 |
| Frontend | Vue 3, Nuxt 4, Nuxt UI v4, TailwindCSS v4, TypeScript |
| Database | PostgreSQL 17 |
| Deployment | Single Docker container, multi-stage build, `linux/amd64` + `linux/arm64` |

The frontend is compiled to static files and served by the .NET backend, so the app and API share one origin and one port. TLS is still terminated in front of it.

## Documentation

Rendered from this repository: [Getting started](https://splitduoapp.netlify.app/getting-started) and [Changelog](https://splitduoapp.netlify.app/changelog).

### Setup and operations

- [Self-hosting with Docker](docs/self-hosting.md) for deployment in five minutes
- [Configuration](docs/readme/configuration.md) for the environment variable reference
- [Development](docs/readme/development.md) for local setup, migrations, and dev services
- [Testing](docs/readme/testing.md) for unit, integration, and frontend tests, and coverage
- [Releasing](docs/readme/releasing.md) for version bumps and changelog generation

### Architecture and design

- [Project specification](docs/project-spec.md)
- [System architecture](docs/architecture/system-architecture.md)
- [Backend architecture](docs/architecture/backend-architecture.md)
- [Frontend architecture](docs/architecture/frontend-architecture.md)
- [CI/CD pipeline](docs/ci-cd-pipeline.md)

### Feature documentation

- [2FA implementation](docs/features/2fa-implementation.md)
- [CSV import](docs/features/csv-import.md)
- [Invitation system](docs/features/invitation-system.md)
- [Receipt scanning](docs/features/receipt-scan/receipt-scan.md)
- [PWA](docs/features/pwa.md)
- [User settings](docs/features/user-settings.md)
- [Platform admin role management](docs/features/platform-admin-role-management.md)
- [Member role management](docs/features/member-role-management.md)
- [Adding a language (i18n)](docs/features/i18n/adding-a-language.md)

### API

- [OpenAPI specification](docs/api/splitduoapi-v1.yaml), with a base URL of `http://localhost:3000/api/v1`
- The interactive Scalar reference is at `http://localhost:8080/scalar/v1` when the backend runs in Development

## Release candidates

Release candidates are pre-release builds for testing upcoming changes. They are not stable, and they are not recommended for production or for data you care about.

Point the `image:` line at the `rc` tag, which always names the newest candidate, or at an immutable tag such as `j1mm0/splitduo:1.17.0-rc.1`. Then run `docker compose up -d`. `latest` always tracks the newest stable release, and candidates are never published as releases.

## Contributing

Bug reports, translations, documentation fixes, and code changes are welcome. Development happens on GitLab; the GitHub repository is a read-only mirror and does not accept pull requests.

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening an issue or merge request.
- Local setup is in [Development](docs/readme/development.md), and test commands are in [Testing](docs/readme/testing.md).
- Issues and merge requests use the templates in [.gitlab/](.gitlab/).
- To add a UI language, see [Adding a language](docs/features/i18n/adding-a-language.md).

## Support

- **Documentation first.** The [self-hosting guide](docs/self-hosting.md) covers deployment questions, and the [website FAQ](https://splitduoapp.netlify.app/#faq) covers the product ones.
- **Bugs and feature requests.** Open an issue on [GitLab](https://gitlab.com/j1mm0/splitduo/-/issues) with the issue template.
- **Security problems.** Report them privately and never in a public issue; see [SECURITY.md](SECURITY.md).
- **No chat channel and no SLA.** This is a side project, answered when there is time.

## Security

Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md). Before an instance is reachable from the internet, set both secrets, terminate TLS at a reverse proxy, and configure backups.

## License

[MIT](LICENSE) © 2025 Francesco Zorzi. SplitDuo is not affiliated with Splitwise or Cospend.
