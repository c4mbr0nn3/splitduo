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
      class="fixed bottom-6 right-6 z-50"
    >
      <UButton
        icon="i-lucide-refresh-cw"
        color="primary"
        size="lg"
        class="rounded-full shadow-lg"
        label="Update Available"
        @click="updateApp"
      />
    </div>
  </ClientOnly>
</template>
