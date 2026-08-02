<template>
  <UCard :ui="{ body: 'p-3 sm:p-4' }">
    <div class="space-y-3 sm:space-y-4">
      <div class="flex items-start justify-between gap-2 sm:gap-3">
        <div class="min-w-0 flex-1 space-y-2">
          <USkeleton class="h-5 sm:h-6 w-3/4" />
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <USkeleton class="h-5 w-24 rounded-full" />
        <USkeleton class="h-5 w-28 rounded-full" />
      </div>

      <div class="flex items-end justify-between gap-2">
        <USkeleton class="h-3 w-24" />
        <USkeleton class="h-5 w-28 rounded-full" />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
</script>
