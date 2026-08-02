<template>
  <div>
    <LayoutAppHeader />
    <UMain class="pb-[env(safe-area-inset-bottom)]">
      <UContainer>
        <slot />
      </UContainer>
    </UMain>
  </div>
</template>

<script setup lang="ts">
const { fetchAiStatus } = useAiStatus()
await fetchAiStatus()
</script>
