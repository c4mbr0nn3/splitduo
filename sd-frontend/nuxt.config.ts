// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: [
    '@nuxt/ui',
    '@nuxt/eslint',
  ],

  ssr: false,

  devtools: { enabled: true },

  app: {
    head: {
      title: 'SplitDuo',
    },
  },

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    public: {
      apiBaseUrl: import.meta.dev ? 'http://localhost:8080/api/v1' : '/api/v1',
    },
  },

  compatibilityDate: '2025-07-16',

  eslint: {
    config: {
      stylistic: true,
    },
  },
})
