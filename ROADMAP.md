# SplitDuo Roadmap

> **Disclaimer**: This roadmap was generated with the assistance of AI and represents a flexible plan for the project's development. It can be changed, revised, or adjusted at any time based on priorities, feedback, and project needs.

## Vision

SplitDuo is a lightweight, self-hosted expense splitting application designed for couples and small groups. This roadmap outlines the planned improvements and features to grow SplitDuo into a polished, feature-complete solution for managing shared expenses while maintaining its core principle of simplicity and self-hosted control.

## Current Status (v0.1.10)

The core application is fully functional and self-hostable.

### Auth & Security

- ✅ JWT authentication with refresh token rotation
- ✅ Two-factor authentication (TOTP, email codes, backup codes)
- ✅ Token-based invitation system for users and group members

### Expenses & Groups

- ✅ Expense tracking with automatic split calculations
- ✅ Balance calculation and settlement optimization
- ✅ Group management with role-based permissions
- ✅ "Add & Add More" flow for rapid expense entry

### Data

- ✅ CSV import (Cospend and SplitDuo formats)
- ✅ CSV export (SplitDuo proprietary format)
- ✅ Email notifications with outbox pattern

### Infrastructure

- ✅ Responsive mobile-first UI with dark mode
- ✅ Docker deployment with PostgreSQL (single container)
- ✅ App version displayed in dashboard

## Phase 1: UI/UX Consolidation & Polish

- **Timeline:** Months 1-3
- **Focus:** Align and improve the user interface across mobile and desktop platforms

### Goals

- [ ] **Desktop Layout Optimization**
  - Improve desktop-specific layouts for better use of horizontal space
  - Audit all pages for responsive breakpoint usage
  - Enhance grid layouts for larger screens

- [ ] **Component Standardization**
  - Standardize form components and validation patterns
  - Consolidate modal usage (consistent GenericModal implementation)
  - Standardize card header patterns across all cards
  - Create consistent error state components

- [ ] **Responsive Design Improvements**
  - Review and enhance all page layouts for tablet/desktop views
  - Ensure consistent spacing and typography across breakpoints
  - Optimize navigation patterns for different screen sizes

- [ ] **Accessibility Enhancements**
  - WCAG compliance audit
  - Keyboard navigation improvements
  - Screen reader optimization
  - Focus management in modals and forms

- [ ] **Loading States Enhancement**
  - Add skeleton loaders for better perceived performance
  - Standardize loading indicators across components

## Phase 2: Feature Enhancements

- **Timeline:** Months 4-7
- **Focus:** Extend core functionality and improve user experience

### Goals

- [ ] **Custom Categories**
  - Move from enum-based to CRUD operations
  - Allow users to create, edit, and delete custom categories
  - Category icons and color customization
  - Default categories for new groups

- [ ] **Custom Payment Modes**
  - Move from enum-based to CRUD operations
  - Allow users to create custom payment methods
  - Support for credit cards, digital wallets, etc.

- [ ] **Splitwise Import**
  - CSV parser for Splitwise export format
  - Data mapping and validation
  - Frontend import wizard UI
  - Migration guide documentation

- [ ] **Settlement Management Improvements**
  - Enhanced settlement suggestion UI
  - Settlement history visualization
  - Payment confirmation workflow
  - Settlement notifications

- [ ] **Recurring Expenses**
  - Schedule recurring expenses (daily, weekly, monthly)
  - Automatic expense creation with background jobs
  - Edit/pause/cancel recurring expense patterns
  - Preview of upcoming recurring expenses

- [ ] **Data Visualization & Reporting**
  - Expense trends over time (charts)
  - Category breakdown visualization
  - Monthly/yearly spending reports
  - Export reports as PDF

- [ ] **AI Integration (Experimental)**

  > **Note**: Optional feature enabled via Ollama URL in docker-compose environment variables. Great learning opportunity for AI integration in full-stack applications.
  - **Infrastructure Setup**
    - Optional Ollama integration via environment variables
    - Backend service for LLM interaction
    - Graceful fallback when Ollama is not configured
    - Model selection and configuration UI

  - **Expenditure Analysis & Insights**
    - Natural language spending summaries (monthly/weekly/custom periods)
    - Spending pattern detection ("You tend to spend more on weekends")
    - Anomaly detection for unusual expenses
    - Comparative analysis between time periods
    - Category-based insights and recommendations
    - Trend identification and forecasting

  - **Natural Language Expense Entry**
    - Parse natural language input (e.g., "Paid $45 for groceries at Whole Foods yesterday")
    - Extract structured data: amount, category, date, description, payer
    - Handle split instructions ("split with Sarah", "I owe 60%")
    - Support for conversational expense creation
    - Smart field suggestions based on context

## Phase 3: Security & Testing

- **Timeline:** Months 8-10
- **Focus:** Enhance security features and establish comprehensive test coverage

### Goals

- [ ] **Frontend 2FA Integration**
  - Complete 2FA setup UI (backend already implemented)
  - TOTP QR code generation and display
  - Backup codes display and regeneration
  - 2FA enforcement options for admins

- [ ] **Backend Testing**
  - Unit tests for critical services (auth, expenses, settlements)
  - Integration tests for API endpoints
  - Test coverage reporting
  - Automated test execution in CI/CD

- [ ] **Frontend Testing**
  - Component unit tests (Vitest)
  - Integration tests for critical user flows
  - E2E testing for authentication and expense workflows

- [ ] **Security Hardening**
  - Security audit of authentication flows
  - Input validation review
  - SQL injection prevention verification
  - XSS protection verification

## Phase 4: Documentation & Long-term Polish

- **Timeline:** Months 11-12
- **Focus:** Polish the product and provide comprehensive documentation

### Goals

- [ ] **User Documentation**
  - Getting started guide
  - Feature documentation with screenshots
  - FAQ section
  - Troubleshooting guide

- [ ] **Admin Documentation**
  - Self-hosting guide (Docker, environment variables)
  - Configuration reference
  - Backup and restore procedures
  - Upgrade procedures

- [ ] **API Documentation**
  - Swagger/OpenAPI enhancements
  - API usage examples
  - Authentication flow documentation
  - Webhook documentation (if implemented)

- [ ] **Developer Documentation**
  - Architecture overview
  - Contribution guidelines
  - Development environment setup
  - Code style guide

- [ ] **Performance Optimization**
  - Database query optimization
  - API response time improvements
  - Frontend bundle size optimization
  - Caching strategy implementation

- [ ] **Budget Tracking**
  - Set category-based budgets
  - Budget progress tracking
  - Budget alerts and notifications
  - Monthly budget reports

## Future Considerations

Ideas for beyond the 12-month roadmap:

- **Mobile Applications**: Native iOS and Android apps
- **Multi-currency Support**: Handle expenses in different currencies
- **Advanced Splitting**: Percentage-based, custom ratio splits
- **Expense Templates**: Quick-add frequently used expenses
- **Group Statistics**: Advanced analytics and insights
- **Collaborative Features**: Comments on expenses, @mentions
- **Receipt Attachments**: Upload and store receipt images
- **Webhooks & Integrations**: Connect with other services
- **Advanced Settlements**: Payment platform integration (PayPal, Venmo)
- **Audit Trail**: Complete history of changes to expenses and groups

## Notes

This roadmap is designed for a side project development pace (after work hours and weekends). Timelines are estimates and may be adjusted based on:

- Available development time
- Feature complexity
- User feedback and priorities
- Technical challenges encountered

Phases are sequential but may overlap. Features within a phase can be implemented in any order based on priority and dependencies.

---

- **Last Updated:** 2026-02-18
- **Current Version:** v0.1.10
