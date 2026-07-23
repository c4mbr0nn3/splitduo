<template>
  <UCard variant="outline">
    <div class="space-y-3 sm:space-y-4">
      <div class="flex items-start justify-between gap-2 sm:gap-3">
        <div class="min-w-0 space-y-2 flex-1">
          <USkeleton class="h-5 sm:h-6 w-3/4" />
          <USkeleton class="h-3 sm:h-4 w-1/2" />
        </div>
        <USkeleton class="w-8 h-8 rounded flex-shrink-0" />
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
