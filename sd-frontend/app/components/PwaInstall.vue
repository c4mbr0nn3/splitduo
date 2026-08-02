<script setup lang="ts">
const deferredPrompt = ref<BeforeInstallPromptEvent | null>(null)
const isInstalled = ref(false)

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const isStandalone = (): boolean =>
  window.matchMedia('(display-mode: standalone)').matches
  || (window.navigator as Navigator & { standalone?: boolean }).standalone === true

const handleBeforeInstallPrompt = (event: Event) => {
  event.preventDefault()
  deferredPrompt.value = event as BeforeInstallPromptEvent
}

const handleAppInstalled = () => {
  deferredPrompt.value = null
  isInstalled.value = true
}

const installApp = async () => {
  if (!deferredPrompt.value) return

  deferredPrompt.value.prompt()
  const { outcome } = await deferredPrompt.value.userChoice

  if (outcome === 'accepted') {
    isInstalled.value = true
  }

  deferredPrompt.value = null
}

onMounted(() => {
  if (import.meta.client) {
    isInstalled.value = isStandalone()
    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
    window.addEventListener('appinstalled', handleAppInstalled)
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
  window.removeEventListener('appinstalled', handleAppInstalled)
})
</script>

<template>
  <ClientOnly>
    <div
      v-if="deferredPrompt && !isInstalled"
      class="fixed bottom-[calc(1.5rem+env(safe-area-inset-bottom))] left-[calc(1.5rem+env(safe-area-inset-left))] z-50"
    >
      <UButton
        icon="i-lucide-download"
        color="secondary"
        size="lg"
        class="rounded-full shadow-[var(--sd-card-shadow)] hover:shadow-[var(--sd-card-shadow-hover)] transition-shadow"
        :label="$t('pwa.installApp')"
        @click="installApp"
      />
    </div>
  </ClientOnly>
</template>
