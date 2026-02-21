// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: ['@nuxt/ui', '@nuxt/eslint'],

  ssr: false,

  devtools: { enabled: true },

  app: {
    head: {
      title: 'SplitDuo',
      titleTemplate: '%s | SplitDuo',
      link: [
        { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
        { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' },
        {
          rel: 'stylesheet',
          href: 'https://fonts.googleapis.com/css2?family=Geist:wght@300..700&display=swap',
        },
      ],
    },
  },

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    public: {
      apiBaseUrl: '/api/v1',
      appVersion: process.env.NUXT_PUBLIC_APP_VERSION || 'dev',
    },
  },

  compatibilityDate: '2025-07-16',

  hooks: {
    'prerender:routes'({ routes }) {
      routes.clear() // Do not generate any routes (except the defaults)
    },
  },

  eslint: {
    config: {
      stylistic: true,
    },
  },
})
