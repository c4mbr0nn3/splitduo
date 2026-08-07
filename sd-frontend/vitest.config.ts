import { defineVitestConfig } from '@nuxt/test-utils/config'

// Unit test runner config (see tasks/spec.md).
//
// `defineVitestConfig` loads the Nuxt config with `test: true` and merges the
// Nuxt Vite config (aliases `~`/`@` → `app/`, unimport auto-import transform,
// vue plugin) into Vitest. This is what makes `mockNuxtImport` available in
// test files and lets composables use auto-imported APIs (ref, useState, ...)
// without explicit imports.
//
// The default project runs in `happy-dom` (no browser binary — works on
// Fedora). `defineVitestConfig` also registers a `nuxt` project for
// `*.nuxt.test.ts` files, which this repo does not use; it stays inert.
export default defineVitestConfig({
  test: {
    environment: 'happy-dom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    include: ['app/**/*.test.ts'],
    coverage: {
      provider: 'v8',
      include: ['app/**'],
      reporter: ['text'],
    },
  },
})
