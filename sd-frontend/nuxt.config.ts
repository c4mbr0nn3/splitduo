// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({

  modules: [
    '@nuxt/ui',
    '@nuxt/eslint',
  ],
  devtools: { enabled: true },

  css: ['~/assets/css/main.css'],

  compatibilityDate: '2025-07-16',

  eslint: {
    config: {
      stylistic: true,
    },
  },
})
