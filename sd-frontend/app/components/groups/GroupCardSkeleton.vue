<template>
  <UCard variant="outline">
    <div class="space-y-4">
      <div class="flex items-start justify-between">
        <div class="flex items-center gap-3">
          <USkeleton class="w-12 h-12 rounded-full flex-shrink-0" />
          <div class="space-y-2">
            <USkeleton class="h-5 w-36" />
            <USkeleton class="h-3 w-20" />
          </div>
        </div>
        <div class="flex items-center gap-1">
          <USkeleton class="w-8 h-8 rounded" />
          <USkeleton class="w-8 h-8 rounded" />
        </div>
      </div>
      <USeparator />
      <div class="flex items-center justify-between">
        <USkeleton class="h-3 w-32" />
        <USkeleton class="h-3 w-32" />
      </div>
    </div>
  </UCard>
</template>
