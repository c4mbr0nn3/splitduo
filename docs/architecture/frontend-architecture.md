# SplitDuo Frontend Architecture

## Overview

This document outlines the technical decisions and architectural patterns used in the SplitDuo frontend. The frontend uses Nuxt 4 and Nuxt UI.

## Project Structure

### Modern Nuxt 4 Architecture

The `sd-frontend` project follows the Nuxt 4 app directory structure with the Vue 3 Composition API:

```bash
sd-frontend/
├── app/
│   ├── app.config.ts              # Nuxt UI theme (primary: teal, secondary: rose, neutral: zinc)
│   ├── app.vue                    # Root component (UApp + NuxtPage)
│   ├── assets/css/main.css        # Tailwind imports, custom font & color tokens
│   ├── components/
│   │   ├── admin/                 # UserCard, UserForm, PendingUserCard, ChangeEmailModal
│   │   ├── dashboard/             # StatCard, GroupCard, BalanceWidget, QuickActionsCard + skeletons
│   │   ├── expenses/              # ExpenseForm, ExpenseAttachmentsList
│   │   ├── groups/                # ExpenseCard, GroupForm, StatsCards, charts, members/
│   │   ├── layout/                # AppHeader, LogoutButton
│   │   ├── push/                  # PushOptInModal
│   │   ├── recurring/             # RecurringExpenseForm, RecurrenceBuilder, TemplateCard
│   │   ├── settings/              # ThemeSwitcher, LanguageSwitcher, NotificationSettings
│   │   ├── system/                # UpdateBanner
│   │   └── ui/                    # EmptyState, GenericModal, CardHeader, ButtonDropdown, DatePicker
│   ├── composables/
│   │   ├── api/base.ts            # useApi — $fetch wrapper with auth headers
│   │   ├── auth/                  # useAuth, useAuthToken, use2FA
│   │   ├── push/                  # usePush, usePushSession
│   │   ├── resources/             # useGroups, useExpenses, useBalances, useUsers,
│   │   │                          # useCategories, usePaymentModes, useImportExport, useInvitations
│   │   ├── ui/                    # useChartTheme, useModal
│   │   ├── utils/                 # useNotifications, useErrorHandling, usePagination, useDebounceSearch
│   │   └── index.ts               # Barrel export
│   ├── layouts/
│   │   ├── auth.vue               # Minimal centered layout
│   │   └── default.vue            # AppHeader + content
│   ├── middleware/
│   │   ├── auth.ts                # Redirect to / if unauthenticated
│   │   └── admin.ts               # Redirect if not global admin
│   ├── pages/                     # File-based routing (see Page Components below)
│   ├── plugins/
│   │   ├── apexcharts.client.ts   # Registers tree-shaken ApexCharts
│   │   ├── auth.client.ts         # Restores auth state on page load
│   │   └── auth-refresh.client.ts # Proactive token refresh before expiry
│   ├── service-worker/
│   │   └── sw.ts                  # PWA service worker (injectManifest)
│   ├── types/                     # api.d.ts (generated), domain.ts
│   └── utils/
│       ├── currency.ts            # Integer-millicent currency math helpers
│       ├── date.ts                # Date formatting helpers
│       ├── enumUtils.ts           # Generic enum factory
│       ├── html.ts                # HTML escaping helpers
│       ├── imageCompression.ts    # Client-side image compression
│       ├── jwt.ts                 # JWT exp-claim decoding
│       ├── pushOptInPending.ts    # Defers the push opt-in prompt
│       ├── settle.ts              # Settle-confirm URL helpers
│       ├── userRoles.ts           # User role enums
│       └── withMinDuration.ts     # Minimum-duration async wrapper
├── public/
├── nuxt.config.ts
├── package.json
└── eslint.config.mjs
```

**Benefits:**

- **Modern Structure**: Nuxt 4's streamlined app directory approach
- **File-based Routing**: Automatic route generation from pages directory
- **TypeScript First**: Full TypeScript support with generated type definitions
- **Component Auto-imports**: Automatic component and composable imports
- **Developer Experience**: Hot module replacement and enhanced DevTools

## Technology Stack

### Core Framework

**Nuxt 4**:

- **Vue 3**: Latest Vue version with Composition API
- **Auto-imports**: Components, composables, and utilities
- **SPA Mode**: Client-side rendering with `ssr: false`
- **File-based Routing**: Zero-config routing system
- **TypeScript**: First-class TypeScript integration
- **@vite-pwa/nuxt**: PWA support with a service worker and a web app manifest
- **@nuxtjs/i18n**: Locale support for English and Italian

### UI Framework

**Nuxt UI**:

- **Design System**: Built on Tailwind CSS with consistent theming
- **Component Library**: Pre-built form components, buttons, cards, modals
- **Accessibility**: WCAG compliant components out of the box
- **Customization**: Easy theming and color palette customization
- **Performance**: Optimized bundle size with tree-shaking

### Icon System

**Iconify Integration**:

- **@iconify-json/lucide**: Modern, clean line icons
- **@iconify-json/simple-icons**: Brand and technology icons
- **Auto-loading**: Icons loaded on-demand to minimize bundle size

### Development Tools

**ESLint**:

- **@nuxt/eslint**: Nuxt-specific ESLint configuration
- **Stylistic Rules**: Code formatting and consistency
- **TypeScript Support**: Full TypeScript linting integration

## Configuration Management

### App Configuration (`app.config.ts`)

**Design System Configuration**:

```typescript
export default defineAppConfig({
  // https://ui.nuxt.com/getting-started/theme#design-system
  ui: {
    colors: {
      primary: 'teal',
      secondary: 'rose',
      neutral: 'zinc',
    },
    card: {
      defaultVariants: {
        variant: 'outline',
      },
    },
    button: {
      defaultVariants: {},
    },
  },
})
```

**Features:**

- **Runtime Configuration**: App-level settings available in components
- **Theme Customization**: Centralized color palette and component defaults
- **Type Safety**: Fully typed configuration with IntelliSense support

### Nuxt Configuration (`nuxt.config.ts`)

**Module Configuration**:

```typescript
export default defineNuxtConfig({
  modules: ['@vite-pwa/nuxt', '@nuxt/ui', '@nuxt/eslint', '@nuxtjs/i18n'],

  ssr: false,

  devtools: { enabled: true }, // Development tools
  css: ['~/assets/css/main.css'], // Global styles
  compatibilityDate: '2025-07-16', // Nuxt version compatibility

  typescript: {
    typeCheck: false,
  },

  eslint: {
    config: {
      stylistic: true, // Code formatting rules
    },
  },

  i18n: {
    strategy: 'no_prefix',
    defaultLocale: 'en',
    locales: [
      { code: 'en', file: 'en.json', name: 'English' },
      { code: 'it', file: 'it.json', name: 'Italiano' },
    ],
  },
})
```

**Key Features:**

- **Module System**: Automatic setup and configuration
- **Development Tools**: Enhanced debugging and inspection
- **Global Styles**: Centralized CSS imports
- **ESLint Integration**: Automated code quality checks

## Styling Architecture

### Design System

**Tailwind CSS Foundation**:

```css
@import "tailwindcss";
@import "@nuxt/ui";

@theme static {
  --font-sans: 'Geist', sans-serif;

  /* Custom Color Palette */
  --color-green-50: #effdf5;
  --color-green-100: #d9fbe8;
  /* ... full green palette for brand consistency */
  --color-green-950: #052e16;
}
```

**Features:**

- **Utility-First**: Tailwind CSS for rapid UI development
- **Custom Theme**: Brand-specific color palette and typography
- **CSS Variables**: Dynamic theming support
- **Component Integration**: The custom theme styles Nuxt UI components directly

### Color System

**Primary Colors**:

- **Teal**: Primary brand color for buttons, links, highlights
- **Rose**: Secondary color for accents
- **Zinc**: Neutral colors for text, borders, backgrounds

**Custom Green Palette**: Extended green color range (50-950) for nuanced brand expression

## Component Architecture

### Page Components

**File-based Routing**:

- `pages/index.vue`: Authentication page
- `pages/dashboard.vue`: Main dashboard page

**Authentication Page Features** (`pages/index.vue`):

```vue
<template>
  <UAuthForm
    :title="$t('auth.welcomeBack')"
    :fields="fields"
    :submit="{ label: $t('auth.login'), loading: isLoading }"
    @submit="onSubmit"
  />
</template>

<script setup lang="ts">
definePageMeta({
  layout: 'auth',
  middleware: defineNuxtRouteMiddleware(() => {
    const { user } = useAuth()
    if (user.value) {
      return navigateTo('/dashboard')
    }
  }),
})
</script>
```

**Component Pattern**:

- **Composition API**: Vue 3's reactive composition functions
- **Template Slots**: Flexible component layouts with named slots
- **Reactive State**: `ref()` and `reactive()` for state management
- **Form Handling**: Nuxt UI form components with validation

### Root Application (`app.vue`)

**Minimal Application Shell**:

```vue
<template>
  <UApp>
    <NuxtPage />
  </UApp>
</template>
```

**Features:**

- **UApp Wrapper**: Nuxt UI application container
- **NuxtPage Router**: Automatic page component rendering
- **Layout System**: Ready for layout integration

## TypeScript Integration

### Configuration Strategy

**Nuxt-Generated Types**:

```json
{
  "references": [
    { "path": "./.nuxt/tsconfig.app.json" },
    { "path": "./.nuxt/tsconfig.server.json" },
    { "path": "./.nuxt/tsconfig.shared.json" },
    { "path": "./.nuxt/tsconfig.node.json" }
  ]
}
```

**Benefits:**

- **Auto-generated Types**: Nuxt generates types for routes, components, composables
- **Runtime Safety**: Full type checking for API calls and data structures
- **Developer Experience**: IntelliSense for all framework features
- **Build-time Validation**: Catch errors during development

## Development Workflow

### Scripts and Commands

**Development Workflow**:

```json
{
  "scripts": {
    "dev": "nuxt dev", // Development server
    "build": "nuxt build", // Production build
    "generate": "nuxt generate", // Static site generation
    "preview": "nuxt preview", // Preview production build
    "lint": "eslint .", // Code linting
    "lint:fix": "eslint --fix ." // Auto-fix linting issues
  }
}
```

### Code Quality

**ESLint Configuration**:

- **Stylistic Rules**: Consistent code formatting
- **Nuxt-specific Rules**: Framework best practices
- **TypeScript Integration**: Type-aware linting
- **Auto-fixing**: Automated code correction

## Integration Readiness

### API Integration Preparation

**Current State**: The frontend is integrated with the backend API

**Integration Facts**:

1. **HTTP Client**: A `$fetch`-based client in `app/composables/api/base.ts`
2. **Authentication State**: Vue composables with `useState` and refs
3. **JWT Token Handling**: Automatic token attachment and proactive refresh
4. **API Type Definitions**: Types generated from `docs/api/splitduoapi-v1.yaml`
5. **Error Handling**: Centralized error handling with user feedback

### Mobile-First Design

**Responsive Architecture**:

- **Tailwind Responsive Classes**: Mobile-first breakpoint system
- **Nuxt UI Components**: Responsive by default
- **Touch-Friendly Interface**: Optimized for mobile interaction
- **Performance**: SPA client-side rendering for fast mobile loading

## Potential Future Enhancements

- **Advanced Reporting**: PDF export, multi-period comparison

## Technical Decisions Summary

1. **Nuxt 4 Adoption** - Latest framework version for modern features and performance
2. **Nuxt UI Integration** - Comprehensive UI component library for rapid development
3. **TypeScript First** - Full type safety from development to production
4. **Mobile-First Design** - Tailwind CSS responsive design approach
5. **File-based Routing** - Zero-configuration routing system
6. **Composition API** - Modern Vue 3 reactive programming
7. **Design System** - Consistent theming and color palette
8. **Development Tooling** - ESLint, auto-imports, and DevTools integration
9. **Modular Architecture** - Extensible structure for future feature expansion
10. **Performance Focus** - SPA rendering, auto-optimization, and bundle efficiency

## Current Implementation Status

### Completed Features

- ✅ **Authentication**: Login, logout, JWT/refresh token management, forgot/reset password
- ✅ **Two-Factor Authentication**: TOTP setup, QR code, backup codes, login challenge
- ✅ **Dashboard**: Stat cards (total expenses, net balance, group count), group list
- ✅ **Group Management**: Create, edit, and delete groups, invite users, and manage members
- ✅ **Expense Management**: Full CRUD, split calculations, category and payment mode selection
- ✅ **Expense Filters**: Text search, date range, category, payment mode, and persistent pagination
- ✅ **Balance & Settlements**: Per-group balances, settlement suggestions, settlement recording
- ✅ **Group Statistics**: Category donut chart, monthly bar chart, member paid bar chart
- ✅ **Data Import**: Two-phase CSV import (Cospend, Splitwise, SplitDuo formats)
- ✅ **Data Export**: CSV export in SplitDuo format
- ✅ **Admin Panel**: User management (create, edit, delete users)
- ✅ **Invitations**: Token-based invitation flow with accept page
- ✅ **Dark Mode**: Theme switcher on the profile page
- ✅ **Skeleton Loaders**: Loading states for cards and expense lists
- ✅ **Responsive Design**: Mobile-first layout with AppHeader navigation
- ✅ **PWA**: Installable app with service worker, manifest, and offline support
- ✅ **Recurring Expenses**: Template management with generated instances and approval flow
- ✅ **Push Notifications**: Web push subscriptions with opt-in prompt and group activity notifications

## Dependencies Analysis

### Production Dependencies

- **@nuxt/ui**: Core UI component framework (TailwindCSS v4 built-in)
- **nuxt**: Main framework and build system
- **@iconify-json/lucide**: Primary icon set (Lucide)
- **@iconify-json/simple-icons**: Brand icons

### Development Dependencies

- **@nuxt/eslint**: Code quality with stylistic rules
- **eslint**: JavaScript linting

**Dependency Strategy:**

- **Minimal Dependencies**: Keep bundle size optimal
- **Framework Integration**: Use Nuxt ecosystem modules
- **Regular Updates**: Stay current with security and features
- **Performance Focus**: All dependencies contribute to core functionality
