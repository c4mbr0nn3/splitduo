// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: ['@vite-pwa/nuxt', '@nuxt/ui', '@nuxt/eslint'],

  ssr: false,

  devtools: { enabled: true },

  app: {
    spaLoadingTemplate: true,
    head: {
      title: 'SplitDuo',
      titleTemplate: '%s | SplitDuo',
      link: [
        { rel: 'manifest', href: '/manifest.webmanifest' },
        { rel: 'apple-touch-icon', href: '/apple-touch-icon.png' },
        { rel: 'preconnect', href: 'https://fonts.googleapis.com' },
        { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' },
        {
          rel: 'stylesheet',
          href: 'https://fonts.googleapis.com/css2?family=Geist:wght@300..700&display=swap',
        },
      ],
      meta: [
        { name: 'theme-color', content: '#14b8a6' },
        { name: 'apple-mobile-web-app-capable', content: 'yes' },
        { name: 'mobile-web-app-capable', content: 'yes' },
        { name: 'apple-mobile-web-app-status-bar-style', content: 'default' },
        { name: 'apple-mobile-web-app-title', content: 'SplitDuo' },
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
      id: '/',
      name: 'SplitDuo',
      short_name: 'SplitDuo',
      description: 'Expense splitting app for small groups — couples, housemates, travel companions, or anyone sharing costs.',
      lang: 'en',
      dir: 'ltr',
      categories: ['finance', 'productivity'],
      theme_color: '#14b8a6',
      background_color: '#ffffff',
      display: 'standalone',
      display_override: ['standalone', 'minimal-ui'],
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
