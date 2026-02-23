# SplitDuo - Expense Splitting App

## Project Overview

**SplitDuo** is a lightweight, open-source expense splitting application designed initially for couples and two-person households. It provides a slim alternative to existing solutions like Splitwise (proprietary) and Cospend (requires full Nextcloud installation).

## App Name & Domain

- **App Name**: SplitDuo
- **Available Domains**:
  - splitduo.io
  - splitduo.app _(recommended)_
  - splitduo.me
- **Recommended Choice**: splitduo.app (matches the app nature and has built-in HTTPS requirements)

## Technology Stack

### Backend

- **.NET** - Web API backend
- **PostgreSQL** - Database
- **Authentication System** - Required for public internet access

### Frontend

- **Vue.js** - JavaScript framework
- **Nuxt UI** - UI component library
- **Mobile-First Design** - Responsive design optimized for mobile usage

### Infrastructure

- **Single Docker Container** - Backend and frontend served from one container for simplicity
- **PostgreSQL Database** - Separate database container
- **Docker Compose** - For easy multi-container deployment and management
- **Homelab Hosting** - Self-hosted on personal infrastructure
- **Public Internet Facing** - Accessible from outside the local network

## Core Features

### Initial Scope (v1.0)

- **Two-User Focus**: Designed primarily for couples/partners
- **Mobile-First UI**: Optimized for on-the-go expense tracking via smartphones
- **Expense Tracking**: Add, edit, and delete shared expenses
- **Payment Mode Tracking**: Track payment methods (cash, card, transfer, etc.) for each expense
- **Split Calculations**: Automatic calculation of who owes what with balance optimization
- **Balance Management**: Real-time balance tracking across all expenses and settlements
- **Settlement Optimization**: Smart suggestions to minimize the number of transactions needed
- **Payment Tracking**: Record settlements between users with comprehensive history
- **User Authentication**: Secure login system for public access
- **Data Import**: Import existing data from Cospend backup files
- **Data Export**: Export/backup splitting data to CSV with Cospend backup file structure compatibility

### Future Expansion Possibilities

- **Multi-User Support**: Expand to support groups of more than 2 people
- **Advanced Features**: Categories, recurring expenses, payment tracking, etc.
- **Native Mobile App**: Dedicated iOS/Android applications

## Data Migration & Compatibility

### Import Functionality

- **Cospend Backup Import**: Users can migrate from Cospend by importing their backup files
- **Seamless Transition**: Preserve existing expense history and user data

### Export Functionality

- **CSV Export**: Export all expense and splitting data
- **Cospend Compatibility**: Export format matches Cospend backup file structure
- **Data Portability**: Users can migrate away from SplitDuo if needed

## Key Differentiators

1. **Lightweight**: No need for full Nextcloud installation like Cospend
2. **Open Source**: Unlike Splitwise, source code is available and modifiable
3. **Self-Hosted**: Complete control over data and privacy
4. **Couple-Focused**: Initially optimized for two-person use cases
5. **Migration Friendly**: Easy import from Cospend, easy export to other systems

## Roadmap & Milestones

### Phase 1: Core MVP

- [x] Set up .NET backend with PostgreSQL
- [x] Implement user authentication (JWT + refresh token rotation)
- [x] Configure initial user creation via AppOptions (no registration endpoint)
- [x] Implement email notification system (outbox pattern with background processing)
- [x] Implement user management service (CRUD operations)
- [x] Implement groups management service (create, manage members, authorization)
- [x] Implement core expense CRUD operations (with integrated split calculation)
- [x] Implement balance calculation service (automatic debt calculation)
- [x] Implement Categories as enum (no CRUD for now)
- [x] Implement Payment Modes as enum (no CRUD for now)
- [x] Implement Cospend backup file import (CSV parsing and data mapping)
- [x] Add data validation and error handling
- [x] Create mobile-first Vue.js frontend with Nuxt UI
- [x] Deploy with Docker Compose (single app container + PostgreSQL container)
- [x] GitLab CI/CD pipeline for automated builds and push to Docker registry

### Phase 2: Enhancements & Additional Imports/Exports

- [x] Implement invite system (email-based invitations)
- [x] Multi-user group support (beyond two users)
- [ ] Implement custom categories and payment modes (CRUD operations)
- [x] Implement Splitwise data import (CSV parsing and data mapping)
- [x] Implement CSV export with proprietary format
- [x] Implement CSV import with proprietary format
- [x] Implement 2FA (Two-Factor Authentication) on frontend (TOTP-only, v0.1.20)

### Phase 3: Polish

- [x] Implement settlements management (payment recording between users)
- [x] Implement settlement optimization (minimize transaction suggestions)
- [ ] Improve mobile UI/UX and responsive design
- [ ] Improve desktop UI/UX
- [ ] Implement unit and integration tests

### Phase 4: Future Expansion

- [ ] Advanced features (recurring expenses, payment tracking)
- [x] Implement data visualization (charts, reports) (v0.1.14)

## Technical Considerations

### Security

- Secure authentication for public internet access
- HTTPS enforcement (especially with .app domain)
- Input validation and SQL injection prevention
- Rate limiting and abuse prevention

### Performance

- Efficient database queries
- Optimized Docker images

### Backup & Recovery

- Database backup strategies
- Docker Compose deployment management
- Data recovery procedures
- Export functionality as backup mechanism

## Success Metrics

### Initial Goals

- Successfully replace existing Cospend usage
- Smooth migration of existing expense data
- Reliable two-person expense tracking
- Stable self-hosted deployment

### Growth Metrics

- User satisfaction with simplified setup vs. Cospend
- Data migration success rate
- System uptime and reliability
- Community adoption (if open-sourced)

## Notes

- The app should remain focused on simplicity and reliability over feature bloat
- Maintain compatibility with Cospend data format for easy migration paths
- Consider the needs of couples/partners as the primary use case
- Keep the architecture extensible for future multi-user expansion
