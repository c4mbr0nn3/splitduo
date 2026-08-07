import { vi, beforeEach } from 'vitest'
import { ref, type Ref } from 'vue'

// Nuxt runtime stubs for composable tests.
//
// `defineVitestConfig` (from @nuxt/test-utils) applies Nuxt's auto-import
// transform, which rewrites bare `useState`/`useCookie`/`useNuxtApp`/... calls
// in composables to real imports from `#app/composables/state`,
// `#app/composables/cookie`, `#app/nuxt`, etc. So `vi.stubGlobal` cannot
// intercept them — we must `vi.mock` the resolved module paths instead.
//
// These mocks are hoisted by Vitest and apply to all test files that
// transitively import the mocked modules.

// --- In-memory stores (reset between tests via beforeEach) ---
const stateStore = new Map<string, Ref<unknown>>()
const cookieStore = new Map<string, Ref<unknown>>()
const i18nLocale = ref('en')

// --- Mock #app/composables/state (useState, clearNuxtState) ---
vi.mock('#app/composables/state', () => ({
  useState: <T>(key: string, init: () => T): Ref<T> => {
    if (!stateStore.has(key)) {
      stateStore.set(key, ref(init()) as Ref<unknown>)
    }
    return stateStore.get(key) as Ref<T>
  },
  clearNuxtState: (key?: string) => {
    if (key) stateStore.delete(key)
    else stateStore.clear()
  },
}))

// --- Mock #app/composables/cookie (useCookie, refreshCookie) ---
vi.mock('#app/composables/cookie', () => ({
  useCookie: <T>(key: string, opts?: { default?: () => T }): Ref<T | null> => {
    if (!cookieStore.has(key)) {
      const initial = opts?.default ? opts.default() : null
      cookieStore.set(key, ref(initial) as Ref<unknown>)
    }
    return cookieStore.get(key) as Ref<T | null>
  },
  refreshCookie: () => {},
}))

// --- Mock #app/nuxt (useNuxtApp, useRuntimeConfig, defineNuxtPlugin, ...) ---
vi.mock('#app/nuxt', async (importOriginal) => {
  const actual = await importOriginal<typeof import('#app/nuxt')>()
  return {
    ...actual,
    useNuxtApp: () => ({
      $config: { public: { apiBaseUrl: '/api/v1', appVersion: 'test' } },
      $i18n: { locale: i18nLocale },
    }),
    useRuntimeConfig: () => ({
      public: { apiBaseUrl: '/api/v1', appVersion: 'test' },
    }),
  }
})

// --- Mock #app/composables/error (createError) ---
vi.mock('#app/composables/error', async (importOriginal) => {
  const actual = await importOriginal<typeof import('#app/composables/error')>()
  return {
    ...actual,
    createError: (opts: { statusCode?: number, statusMessage?: string, message?: string }): Error => {
      const err = new Error(opts.message ?? opts.statusMessage ?? 'Error')
      Object.assign(err, { statusCode: opts.statusCode, statusMessage: opts.statusMessage })
      return err
    },
  }
})

// --- Mock #app/composables/router (navigateTo, useRoute, useRouter) ---
const navigateToMock = vi.fn(async () => undefined)
vi.mock('#app/composables/router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('#app/composables/router')>()
  return {
    ...actual,
    navigateTo: navigateToMock,
    useRoute: () => ({ path: '/', query: {}, params: {}, meta: {} }),
    useRouter: () => ({
      back: vi.fn(),
      push: vi.fn(),
      replace: vi.fn(),
      afterEach: vi.fn(() => () => {}),
      beforeEach: vi.fn(() => () => {}),
      currentRoute: ref({ path: '/' }),
      options: { history: { state: { back: null, current: '/', forward: null, position: 0, replaced: false, scroll: null } } },
    }),
  }
})

// --- Mock #app/composables/ssr (useRequestHeaders, etc.) ---
vi.mock('#app/composables/ssr', () => ({
  useRequestHeader: () => undefined,
  useRequestHeaders: () => ({}),
  useResponseHeader: () => ref({}),
  useRequestEvent: () => undefined,
  useRequestFetch: () => vi.fn(),
  setResponseStatus: () => {},
  onPrehydrate: () => {},
  prerenderRoutes: () => {},
}))

// --- Export navigateToMock for tests that want to assert on it ---
export { navigateToMock }

// --- Reset state between tests ---
beforeEach(() => {
  stateStore.clear()
  cookieStore.clear()
  i18nLocale.value = 'en'
  navigateToMock.mockClear()
})
