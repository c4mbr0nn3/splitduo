<template>
  <UCard variant="subtle">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <USkeleton class="w-10 h-10 rounded-full flex-shrink-0" />
        <div class="space-y-2">
          <USkeleton class="h-4 w-32" />
          <USkeleton class="h-3 w-20" />
        </div>
      </div>
      <USkeleton class="w-5 h-5 rounded" />
    </div>
  </UCard>
</template>
