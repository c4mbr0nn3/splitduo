<template>
  <div>
    <LayoutAppHeader />
    <UMain>
      <UContainer>
        <slot />
      </UContainer>
    </UMain>
  </div>
</template>

<script setup>
const { fetchAiStatus } = useAiStatus()
await fetchAiStatus()
</script>
