<template>
  <UCard variant="outline">
    <div class="space-y-4">
      <div class="flex items-start justify-between">
        <div class="flex items-center gap-3">
          <USkeleton class="w-10 h-10 rounded-full flex-shrink-0" />
          <div class="space-y-2">
            <USkeleton class="h-5 w-32" />
            <USkeleton class="h-3 w-40" />
          </div>
        </div>
        <div class="flex items-center gap-1">
          <USkeleton class="w-8 h-8 rounded" />
          <USkeleton class="w-8 h-8 rounded" />
          <USkeleton class="w-8 h-8 rounded" />
        </div>
      </div>
      <USkeleton class="h-5 w-16 rounded-full" />
      <USeparator />
      <div class="flex items-center justify-between">
        <USkeleton class="h-3 w-28" />
        <USkeleton class="h-3 w-28" />
      </div>
    </div>
  </UCard>
</template>
