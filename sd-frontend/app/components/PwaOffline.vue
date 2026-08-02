<script setup lang="ts">
const { t } = useI18n()
const isOffline = ref(false)
let onlineTimeout: ReturnType<typeof setTimeout> | null = null

const showBackOnlineToast = () => {
  const { showSuccess } = useNotifications()
  onlineTimeout = setTimeout(() => {
    showSuccess(t('pwa.backOnline'))
  }, 300)
}

const handleOffline = () => {
  isOffline.value = true
}

const handleOnline = () => {
  if (!isOffline.value) return
  isOffline.value = false
  showBackOnlineToast()
}

onMounted(() => {
  if (import.meta.client) {
    isOffline.value = !navigator.onLine
    window.addEventListener('offline', handleOffline)
    window.addEventListener('online', handleOnline)
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('offline', handleOffline)
  window.removeEventListener('online', handleOnline)
  if (onlineTimeout) {
    clearTimeout(onlineTimeout)
  }
})
</script>

<template>
  <ClientOnly>
    <div
      v-if="isOffline"
      class="fixed top-0 left-0 right-0 z-50 bg-warning pt-[env(safe-area-inset-top)]"
    >
      <div class="flex items-center justify-center gap-2 px-4 py-2 text-warning-inverted text-sm font-medium">
        <span
          class="i-lucide-wifi-off size-4"
          aria-hidden="true"
        />
        <span>{{ $t('pwa.offline') }}</span>
      </div>
    </div>
  </ClientOnly>
</template>
