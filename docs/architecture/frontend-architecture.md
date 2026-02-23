# SplitDuo Frontend Architecture

## Overview

This document outlines the technical decisions and architectural patterns used in the SplitDuo frontend solution built with Nuxt 4 and Nuxt UI.

## Project Structure

### Modern Nuxt 4 Architecture

The `sd-frontend` project follows Nuxt 4's modern app directory structure with Vue 3 Composition API:

```bash
sd-frontend/
├── app/
│   ├── app.config.ts              # Nuxt UI theme (primary: violet, neutral: zinc)
│   ├── app.vue                    # Root component (UApp + NuxtPage)
│   ├── assets/css/main.css        # Tailwind imports, custom font & color tokens
│   ├── components/
│   │   ├── admin/                 # UserCard, UserForm
│   │   ├── button/                # ColorMode toggle
│   │   ├── dashboard/             # GroupCard, StatCard + skeletons
│   │   ├── expenses/              # ExpenseForm
│   │   ├── groups/                # ExpenseCard, GroupForm, StatsCards, charts, members/
│   │   ├── layout/                # AppHeader, LogoutButton
│   │   └── ui/                    # EmptyState, GenericModal, CardHeader, ButtonDropdown, DatePicker
│   ├── composables/
│   │   ├── api/base.js            # useApi — $fetch wrapper with auth headers
│   │   ├── auth/                  # useAuth, useAuthToken, use2FA
│   │   ├── resources/             # useGroups, useExpenses, useBalances, useUsers,
│   │   │                          # useCategories, usePaymentModes, useImportExport, useInvitations
│   │   ├── ui/                    # useChartTheme, useModal
│   │   ├── utils/                 # useNotifications, useErrorHandling, usePagination, useDebounceSearch
│   │   └── index.js               # Barrel export
│   ├── layouts/
│   │   ├── auth.vue               # Minimal centered layout
│   │   └── default.vue            # AppHeader + content
│   ├── middleware/
│   │   ├── auth.js                # Redirect to / if unauthenticated
│   │   └── admin.js               # Redirect if not global admin
│   ├── pages/                     # File-based routing (see Routes below)
│   ├── plugins/
│   │   └── auth.client.js         # Restores auth state on page load
│   └── utils/
│       ├── date.js                # Date formatting helpers
│       ├── enumUtils.js           # Generic enum factory
│       └── userRoles.js           # User role enums
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

**Nuxt 4** (`^4.3.1`):

- **Vue 3**: Latest Vue version with Composition API
- **Auto-imports**: Components, composables, and utilities
- **Server-Side Rendering**: Full SSR/SPA/Static generation support
- **File-based Routing**: Zero-config routing system
- **TypeScript**: First-class TypeScript integration

### UI Framework

**Nuxt UI** (`^4.4.0`):

- **Design System**: Built on Tailwind CSS with consistent theming
- **Component Library**: Pre-built form components, buttons, cards, modals
- **Accessibility**: WCAG compliant components out of the box
- **Customization**: Easy theming and color palette customization
- **Performance**: Optimized bundle size with tree-shaking

### Icon System

**Iconify Integration**:

- **@iconify-json/lucide** (`^1.2.66`): Modern, clean line icons
- **@iconify-json/simple-icons** (`^1.2.50`): Brand and technology icons
- **Auto-loading**: Icons loaded on-demand to minimize bundle size

### Development Tools

**ESLint** (`^9.34.0`):

- **@nuxt/eslint** (`^1.9.0`): Nuxt-specific ESLint configuration
- **Stylistic Rules**: Code formatting and consistency
- **TypeScript Support**: Full TypeScript linting integration

## Configuration Management

### App Configuration (`app.config.ts`)

**Design System Configuration**:

```typescript
export default defineAppConfig({
  ui: {
    colors: {
      primary: "emerald", // Primary brand color
      neutral: "slate", // Neutral/secondary color
    },
    button: {
      defaultVariants: {
        // Customizable component defaults
      },
    },
  },
});
```

**Features:**

- **Runtime Configuration**: App-level settings available in components
- **Theme Customization**: Centralized color palette and component defaults
- **Type Safety**: Fully typed configuration with IntelliSense support

### Nuxt Configuration (`nuxt.config.ts`)

**Module Configuration**:

```typescript
export default defineNuxtConfig({
  modules: [
    "@nuxt/ui", // UI component framework
    "@nuxt/eslint", // Linting integration
  ],
  devtools: { enabled: true }, // Development tools
  css: ["~/assets/css/main.css"], // Global styles
  compatibilityDate: "2025-07-16", // Nuxt version compatibility
  eslint: {
    config: {
      stylistic: true, // Code formatting rules
    },
  },
});
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
  --font-sans: "Public Sans", sans-serif;

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
- **Component Integration**: Seamless Nuxt UI component styling

### Color System

**Primary Colors**:

- **Emerald**: Primary brand color for buttons, links, highlights
- **Slate**: Neutral colors for text, borders, backgrounds

**Custom Green Palette**: Extended green color range (50-950) for nuanced brand expression

## Component Architecture

### Page Components

**File-based Routing**:

- `pages/index.vue`: Main dashboard/home page
- `pages/login.vue`: Authentication page

**Login Component Features** (`pages/login.vue`):

```vue
<template>
  <div class="flex justify-center items-center h-screen p-4">
    <UCard class="w-full">
      <template #header>
        <div class="text-2xl">Welcome Back</div>
      </template>
      <UForm :state="form" @submit="onSubmit">
        <!-- Form implementation -->
      </UForm>
    </UCard>
  </div>
</template>

<script setup>
// Composition API implementation
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

**Current State**: Placeholder login form with simulated authentication

**Integration Requirements for Backend Connection**:

1. **HTTP Client Setup**: Nuxt 3's `$fetch` or composable-based API client
2. **Authentication State**: Pinia store or composables for user state management
3. **JWT Token Handling**: Automatic token attachment and refresh logic
4. **API Type Definitions**: TypeScript interfaces for backend DTOs
5. **Error Handling**: Global error handling and user feedback patterns

### Mobile-First Design

**Responsive Architecture**:

- **Tailwind Responsive Classes**: Mobile-first breakpoint system
- **Nuxt UI Components**: Responsive by default
- **Touch-Friendly Interface**: Optimized for mobile interaction
- **Performance**: SSR and hydration for fast mobile loading

## Potential Future Enhancements

- **PWA**: Service worker caching for offline use
- **Advanced Reporting**: PDF export, multi-period comparison
- **Recurring Expenses**: Frontend scheduling UI

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
10. **Performance Focus** - SSR, auto-optimization, and bundle efficiency

## Current Implementation Status

### Completed Features (v0.1.21)

- ✅ **Authentication**: Login, logout, JWT/refresh token management, forgot/reset password
- ✅ **Two-Factor Authentication**: TOTP setup, QR code, backup codes, login challenge
- ✅ **Dashboard**: Stat cards (total expenses, net balance, group count), group list
- ✅ **Group Management**: Create, edit, delete groups; invite users; manage members
- ✅ **Expense Management**: Full CRUD, split calculations, category and payment mode selection
- ✅ **Expense Filters**: Text search, date range, category, payment mode; persistent pagination
- ✅ **Balance & Settlements**: Per-group balances, settlement suggestions, settlement recording
- ✅ **Group Statistics**: Category donut chart, monthly bar chart, member paid bar chart
- ✅ **Data Import**: Two-phase CSV import (Cospend, Splitwise, SplitDuo formats)
- ✅ **Data Export**: CSV export in SplitDuo format
- ✅ **Admin Panel**: User management (create, edit, delete users)
- ✅ **Invitations**: Token-based invitation flow with accept page
- ✅ **Dark Mode**: Toggle via ColorMode button
- ✅ **Skeleton Loaders**: Loading states for cards and expense lists
- ✅ **Responsive Design**: Mobile-first layout with AppHeader navigation

## Dependencies Analysis

### Production Dependencies

- **@nuxt/ui** (`^4.4.0`): Core UI component framework (TailwindCSS v4 built-in)
- **nuxt** (`^4.3.1`): Main framework and build system
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
