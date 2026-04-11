// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: ['@vite-pwa/nuxt', '@nuxt/ui', '@nuxt/eslint'],

  ssr: false,

  devtools: { enabled: true },

  app: {
    head: {
      title: 'SplitDuo',
      titleTemplate: '%s | SplitDuo',
      link: [
        { rel: 'manifest', href: '/manifest.webmanifest' },
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

  pwa: {
    registerType: 'prompt',
    devOptions: {
      enabled: true,
      suppressWarnings: true,
    },
    manifest: {
      name: 'SplitDuo',
      short_name: 'Split',
      description: 'Expense splitting app for couples',
      theme_color: '#14b8a6',
      background_color: '#ffffff',
      display: 'standalone',
      icons: [
        {
          src: 'android-chrome-192x192.png',
          sizes: '192x192',
          type: 'image/png',
        },
        {
          src: 'android-chrome-512x512.png',
          sizes: '512x512',
          type: 'image/png',
        },
        {
          src: 'maskable-icon.png',
          sizes: '512x512',
          type: 'image/png',
          purpose: 'maskable',
        },
      ],
    },
    workbox: {
      navigateFallback: '/index.html',
      navigateFallbackDenylist: [/^\/api\//],
      globPatterns: ['**/*.{js,css,html,png,svg,ico}'],
      runtimeCaching: [
        {
          urlPattern: /^\/api\//,
          handler: 'NetworkOnly',
        },
      ],
    },
  },
})
