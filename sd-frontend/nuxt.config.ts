// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({

  modules: [
    '@nuxt/ui',
    '@nuxt/eslint',
  ],
  imports: {
    dirs: [
      '~/composables/**',
    ],
  },
  devtools: { enabled: true },

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NODE_ENV === 'production' ? '/api/v1' : 'http://localhost:8080/api/v1',
    },
  },

  compatibilityDate: '2025-07-16',

  eslint: {
    config: {
      stylistic: true,
    },
  },
})
