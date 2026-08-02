<template>
  <UCard :ui="{ body: 'p-4 sm:p-5' }">
    <!-- Row 1: date + amount -->
    <div class="flex items-start justify-between gap-3">
      <div class="space-y-2">
        <USkeleton class="h-3 w-24" />
        <USkeleton class="h-3 w-16" />
      </div>
      <div class="flex items-center gap-2">
        <USkeleton class="size-2 rounded-full" />
        <USkeleton class="h-5 w-16" />
      </div>
    </div>
    <!-- Row 2: title -->
    <USkeleton class="h-4 w-48 mt-3" />
    <!-- Row 3: metadata -->
    <div class="flex items-center gap-2 mt-3">
      <USkeleton class="h-5 w-20 rounded-full" />
      <USkeleton class="h-5 w-16 rounded-full" />
      <USkeleton class="h-3 w-14" />
    </div>
  </UCard>
</template>

<script setup lang="ts">
</script>
