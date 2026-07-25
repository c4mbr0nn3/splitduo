# Changelog

All notable changes to SplitDuo will be documented in this file.

Generated with [git-cliff](https://git-cliff.org) from Conventional Commits.

## [1.7.0] - 2026-07-25

### Features

- User settings
## [1.6.4] - 2026-07-24

### Bug Fixes

- *(expenses)* Treat comma as decimal separator in amount inputs
## [1.6.3] - 2026-07-24

### Features

- *(frontend)* Carousel for dashboard recap stats on mobile
## [1.6.2] - 2026-07-24

### Bug Fixes

- *(frontend)* Add dark-mode body background for Android nav bar sampling
## [1.6.1] - 2026-07-24

### Bug Fixes

- *(frontend)* Round currency display to 2 decimals and fix stat card overflow
- *(frontend)* Set color-scheme to follow app dark mode for Android nav bar
## [1.6.0] - 2026-07-23

### Features

- Group alias
## [1.5.0] - 2026-07-20

### Features

- *(frontend)* Proactive token refresh before expiry
## [1.4.0] - 2026-07-20

### Bug Fixes

- *(frontend)* Sync auth token state to prevent stale retry after refresh

### Features

- *(frontend)* Add loading indicator and SPA loading template
## [1.3.1] - 2026-07-20

### Bug Fixes

- *(frontend)* Preserve selected participants when editing expense
## [1.3.0] - 2026-07-20

### Features

- *(frontend)* Rework group stats and recap UI with hierarchy
## [1.2.0] - 2026-07-19

### Bug Fixes

- *(release)* Bump package.json alongside VERSION in cat-v config

### Features

- *(frontend)* Add PWA install prompt, offline banner, and iOS meta tags
## [1.1.0] - 2026-07-19

### Build System

- *(release)* Use commit-and-tag-version for version bumps

### Features

- *(frontend)* Unify back navigation with useSmartBack composable
## [1.0.0] - 2026-07-19

### Bug Fixes

- *(scripts)* Add selinux z mount label for container scans
- *(backend)* Resolve all nullable warnings
- *(auth)* Use UTC DateTime for JWT expiry
- *(scripts)* Tag bump commit after changelog amend

### Code Refactoring

- *(ui)* Gate destructive actions behind overflow menu and confirmation

### Features

- *(scripts)* Add trivy and trufflehog security scan scripts

### Styling

- *(frontend)* Apply UI consistency fixes across all pages
- *(frontend)* Polish desktop and tablet UI layout
## [0.1.38] - 2026-07-12

### Bug Fixes

- *(auth)* Use role claim name to match MapInboundClaims=false
## [0.1.37] - 2026-07-12

### Bug Fixes

- *(docker)* Restore Api project only to skip missing test csprojs
## [0.1.36] - 2026-07-12

### Code Refactoring

- *(backend)* Inject TimeProvider for testable time-dependent code

### Features

- *(auth)* Rework auth with lockout, multi-device refresh, security stamp
## [0.1.35] - 2026-07-11

### Bug Fixes

- *(docker)* Copy pnpm-workspace.yaml for build script approval
## [0.1.33] - 2026-04-21

### Bug Fixes

- Replace UiDatePicker with UiInputDate for date filters
## [0.1.32] - 2026-04-21

### Bug Fixes

- Update app description to reflect broader user base
- Update SameSite attribute for cookies to 'lax' for better compatibility
- Add middleware to redirect authenticated users to dashboard
## [0.1.31] - 2026-04-17

### Bug Fixes

- Update button labels to use span for better responsiveness
- Implement 3-decimal precision for expense splits and update currency formatting
## [0.1.30] - 2026-04-17

### Features

- Add rate limiting for receipt scanning and validate receipt amount
## [0.1.29] - 2026-04-16

### Bug Fixes

- Ensure color is set for notification messages
## [0.1.28] - 2026-04-16

### Features

- Implement AI receipt scanning feature
## [0.1.27] - 2026-04-15

### Features

- *(expense)* Replace UiDatePicker with UiInputDate component
## [0.1.26] - 2026-04-15

### Features

- *(health)* Add health checks for PostgreSQL database
## [0.1.25] - 2026-04-15

### Bug Fixes

- *(auth)* Enhance token handling and refresh logic
## [0.1.24] - 2026-04-11

### Bug Fixes

- Update short name in manifest to 'SplitDuo'
- Replace IConfiguration with IOptions for database connection in LogCleanupJob
## [0.1.23] - 2026-04-11

### Features

- Add pwa feature
- Replace UInput with UiDatePicker for expense date selection. Closes #4
## [0.1.22] - 2026-03-23

### Bug Fixes

- Change currency format from USD to EUR in formatCurrency function. Closes #2

### Features

- Add dev.sh script for managing development services
- Add ToDisplayName extension method for PaymentMode enum
- Add TicketRestaurant option to PaymentMode enum and update extension method. Closes #3
## [0.1.21] - 2026-02-23

### Bug Fixes

- Add APP_VERSION build argument to Docker image build script

### Build System

- Fix dockerfile by adding npmrc file copy
## [0.1.20] - 2026-02-23

### Build System

- Switch to pnpm 10

### Code Refactoring

- Remove LastName from email notification templates
- Switch to otp.net library

### Features

- Db migrations
- Totp 2fa challenge and remove email 2fa
- 2fa flow in frontend
- Add autofocus to TOTP input and simplify auto-submit condition
## [0.1.19] - 2026-02-22

### Code Refactoring

- Update email notification templates for improved clarity and tone
## [0.1.18] - 2026-02-22

### Features

- Email template provider
## [0.1.17] - 2026-02-21

### Features

- Add option to seed demo data and implement demo data seeding logic
- Implement net balance calculation for user groups and update UI accordingly
## [0.1.16] - 2026-02-21

### Styling

- Enhance
- Update button variants and layout for better UI consistency
- Improve layout and structure of profile page for better UI consistency
## [0.1.15] - 2026-02-21

### Code Refactoring

- Consolidate style
## [0.1.14] - 2026-02-21

### Code Refactoring

- Authentication setup and add JwtBearerOptions configuration using options pattern properly

### Features

- Expenses stats charts
- Implement chart theme composable and update chart components

### Styling

- Format
## [0.1.13] - 2026-02-21

### Bug Fixes

- Update filter title styling to use primary color

### Code Refactoring

- Implement ExpenseFilterCard component

### Features

- Add filters fin group expense page
- Replace UInput with UiDatePicker for date selection in ExpenseFilterCard
- Add text search functionality to expense filters
- Persiste page and filters in group index view
## [0.1.12] - 2026-02-21

### Bug Fixes

- Simplify API request handling by creating fetchOnce function and improving error handling

### Code Refactoring

- Standardize page width
- Remove useless settlement entity
- Introduce ExpenseFilterOptions for improved expense retrieval filtering

### Features

- Add Net Balance card to dashboard with dynamic color and icon
- Add skeleton components for user, group, and expense cards; implement loading state in dashboard and groups pages
## [0.1.11] - 2026-02-18

### Bug Fixes

- Remove filtering of "Payment" category in Splitwise CSV parser

### Code Refactoring

- TabsNav component and integrate stats and expenses tabs in group page
- Implement ExpensesTab and StatsTab components for group page

### Features

- Splitwise import
- Add notification system for expense and group deletions
- Group stats
## [0.1.10] - 2026-02-08

### Features

- Invitation system
## [0.1.8] - 2026-02-07

### Features

- Add "Add & Add More" button and functionality to ExpenseForm
## [0.1.7] - 2026-02-01

### Features

- Pass application version as build argument and display in dashboard
## [0.1.4] - 2026-02-01

### Build System

- Go back to node 22
## [0.1.3] - 2026-02-01

### Bug Fixes

- Update expense response was broken since i do not understand EF core tracking context

### Build System

- Update Node.js and .NET versions in Dockerfile

### Styling

- Format
## [0.1.2] - 2025-10-28

### Bug Fixes

- Replace UBadge with UButton for member count display

### Code Refactoring

- Standardize card header
- Reorganize create group button and refresh button layout
- Remove 'Settle Up' quick action from dashboard
- Update revoke tokens functionality to include confirmation modal

### Features

- Generic modal
- Implement password change functionality with validation and notification
- Forgot password
