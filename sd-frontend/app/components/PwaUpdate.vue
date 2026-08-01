<script setup>
const { $pwa } = useNuxtApp()

const updateApp = async () => {
  await $pwa?.updateServiceWorker(true)
}
</script>

<template>
  <ClientOnly>
    <div
      v-if="$pwa?.needRefresh"
      class="fixed bottom-[calc(1.5rem+env(safe-area-inset-bottom))] right-[calc(1.5rem+env(safe-area-inset-right))] z-50"
    >
      <UButton
        icon="i-lucide-refresh-cw"
        color="primary"
        size="lg"
        class="rounded-full shadow-[var(--sd-card-shadow)] hover:shadow-[var(--sd-card-shadow-hover)] transition-shadow"
        :label="$t('pwa.updateAvailable')"
        @click="updateApp"
      />
    </div>
  </ClientOnly>
</template>
