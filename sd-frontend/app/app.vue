<script setup lang="ts">
import { en, it } from '@nuxt/ui/locale'

const { locale } = useI18n()
const uiLocales: Record<string, typeof en> = { en, it }
const uiLocale = computed(() => uiLocales[locale.value] ?? en)

// Web Push: bootstrap once per authenticated period — app mount covers a
// restored session and the auth watcher (inside usePushSession) covers a
// login completed without a reload; logout resets the gate. Skipped for
// anonymous visitors — the push endpoints require auth and would 401 on
// every public pageview.
const push = usePush()
const { isAuthenticated } = useAuth()
const pushSession = usePushSession(push, isAuthenticated)

onMounted(() => {
  pushSession.ensure()

  // Guarded: browsers and webviews without service-worker support (or a non-secure
  // origin) leave `navigator.serviceWorker` undefined, and an unguarded access would
  // throw out of this hook on every page load.
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.addEventListener('message', (event: MessageEvent) => {
      const data = event.data as { type?: string, url?: string } | undefined
      if (data?.type === 'NAVIGATE' && typeof data.url === 'string') {
        void navigateTo(data.url)
      }
    })
  }
})
</script>

<template>
  <UApp :locale="uiLocale">
    <NuxtLoadingIndicator
      color="repeating-linear-gradient(to right, #14b8a6 0%, #5eead4 50%, #0d9488 100%)"
      :height="3"
      :throttle="100"
      :duration="1500"
      error-color="repeating-linear-gradient(to right, #f43f5e 0%, #fb7185 50%, #e11d48 100%)"
    />
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
    <PwaUpdate />
    <PwaInstall />
    <PwaOffline />
  </UApp>
</template>
