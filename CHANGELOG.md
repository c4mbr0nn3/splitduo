# Changelog

All notable changes to SplitDuo will be documented in this file.

## [0.1.21] - 2026-02-24

### Bug Fixes

- add APP_VERSION build argument to Docker image build script

### Build System

- fix dockerfile by adding npmrc file copy

## [0.1.20] - 2026-02-24

### Features

- add autofocus to TOTP input and simplify auto-submit condition
- 2fa flow in frontend
- totp 2fa challenge and remove email 2fa
- db migrations

### Code Refactoring

- switch to otp.net library
- remove LastName from email notification templates

### Documentation

- update claude md with email template rules

### Build System

- switch to pnpm 10

## [0.1.19] - 2026-02-24

### Code Refactoring

- update email notification templates for improved clarity and tone

## [0.1.18] - 2026-02-24

### Features

- email template provider

### Documentation

- add screenshots to readme

## [0.1.17] - 2026-02-24

### Features

- implement net balance calculation for user groups and update UI accordingly
- add option to seed demo data and implement demo data seeding logic

### Documentation

- update api yaml
- update changelog and roadmap

## [0.1.16] - 2026-02-21

### Styling

- improve layout and structure of profile page for better UI consistency
- update button variants and layout for better UI consistency
- enhance

## [0.1.15] - 2026-02-21

### Code Refactoring

- consolidate style

## [0.1.14] - 2026-02-21

### Features

- implement chart theme composable and update chart components
- expenses stats charts

### Code Refactoring

- authentication setup and add JwtBearerOptions configuration using options pattern properly

### Documentation

- update roadmap and project spec for version 0.1.13
- changelog

### Styling

- format

## [0.1.13] - 2026-02-21

### Features

- persiste page and filters in group index view
- add text search functionality to expense filters
- replace UInput with UiDatePicker for date selection in ExpenseFilterCard
- add filters in group expense page

### Bug Fixes

- update filter title styling to use primary color

### Code Refactoring

- implement ExpenseFilterCard component

## [0.1.12] - 2026-02-21

### Features

- add skeleton components for user, group, and expense cards; implement loading state in dashboard and groups pages
- add Net Balance card to dashboard with dynamic color and icon

### Bug Fixes

- simplify API request handling by creating fetchOnce function and improving error handling

### Code Refactoring

- introduce ExpenseFilterOptions for improved expense retrieval filtering
- remove useless settlement entity
- standardize page width

## [0.1.11] - 2026-02-18

### Features

- group stats
- add notification system for expense and group deletions
- splitwise import

### Bug Fixes

- remove filtering of "Payment" category in Splitwise CSV parser

### Code Refactoring

- implement ExpensesTab and StatsTab components for group page
- TabsNav component and integrate stats and expenses tabs in group page

## [0.1.10] - 2026-02-08

### Features

- invitation system

## [0.1.9] - 2026-02-08

Internal dependency updates, no user-facing changes.

## [0.1.8] - 2026-02-07

### Features

- add "Add & Add More" button and functionality to ExpenseForm

## [0.1.7] - 2026-02-01

### Features

- pass application version as build argument and display in dashboard

## [0.1.6] - 2026-02-01

### Maintenance

- Dependency alignment and internal updates

## [0.1.5] - 2026-02-01

### Maintenance

- Package lock alignment and dependency updates

## [0.1.4] - 2026-02-01

### Build System

- go back to node 22

## [0.1.3] - 2026-02-01

### Bug Fixes

- update expense response was broken since i do not understand EF core tracking context

### Documentation

- update readme
- add base claude md file
- format roadmap
- roadmap

### Styling

- format

### Build System

- update Node.js and .NET versions in Dockerfile

## [0.1.2] - 2025-10-28

### Features

- forgot password
- implement password change functionality with validation and notification
- generic modal

### Bug Fixes

- replace UBadge with UButton for member count display

### Code Refactoring

- update revoke tokens functionality to include confirmation modal
- remove 'Settle Up' quick action from dashboard
- reorganize create group button and refresh button layout
- standardize card header

## [0.1.1] - 2025-10-28

### The Beginning

Well, here we are! SplitDuo v0.1.1—the first tagged release, and honestly, the first version that felt ready to actually deploy and use.

This whole thing started from a simple frustration: splitting expenses with your partner shouldn't require signing up for another SaaS product or installing an entire Nextcloud instance. So here's a self-hosted, open-source alternative that does one thing well: helps couples (or any two people, really) track shared expenses and figure out who owes what.

**What you get in this first release:**

**The Full Stack**

- .NET backend with PostgreSQL, following vertical slice architecture
- Vue.js/Nuxt frontend with Nuxt UI components, designed mobile-first
- Single Docker container deployment (backend serves the frontend)
- Docker Compose setup ready for your homelab
- GitLab CI/CD pipeline that actually works

**Core Features**

- User authentication with JWT + refresh tokens (proper security!)
- Group management so you can organize expenses by context
- Expense tracking with flexible split options
- Automatic balance calculations (no mental math required)
- Settlement tracking to record payments
- Categories and payment modes to keep things organized
- Import from Cospend backups (for the Nextcloud refugees)
- Export to SplitDuo format (your data, your rules)

**The Details That Matter**

- Background job processing for imports and emails
- Email notifications with retry logic (because SMTP is... fun)
- Real-time split calculation preview in the UI
- Mobile-responsive design (because you're adding expenses on-the-go)
- Loading states and empty states that don't feel janky
- Version bumping script for releases (meta!)

**What's Not Here (Yet)**

- Native mobile apps (PWA works fine for now)
- Advanced reporting or analytics
- Recurring expenses
- Multi-currency support
- A million other features that Splitwise has

This is intentionally minimal. It does what I need it to do, and hopefully what you need too. Everything else can come later if it makes sense.

The code isn't perfect, the UI could be prettier, and there are definitely bugs hiding in there. But it works, it's deployed, and it's handling real expenses. That's good enough for v0.1.
