<template>
  <UCard :ui="{ body: 'p-4 sm:p-5' }">
    <div class="flex items-center gap-3">
      <USkeleton class="w-10 h-10 rounded-full flex-shrink-0" />
      <div class="space-y-2 min-w-0">
        <USkeleton class="h-3 w-24" />
        <USkeleton class="h-7 w-16" />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
</script>
