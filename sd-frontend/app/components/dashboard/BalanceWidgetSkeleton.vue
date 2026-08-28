<template>
  <UCard
    class="sd-surface"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <div class="flex items-center justify-between gap-2 mb-4">
      <USkeleton class="h-5 w-32" />
      <USkeleton class="h-5 w-20 rounded-full" />
    </div>
    <div class="flex items-center gap-3">
      <USkeleton class="size-12 rounded-full" />
      <USkeleton class="h-8 w-36" />
    </div>
    <USeparator class="my-4" />
    <div class="grid grid-cols-2 gap-4">
      <div>
        <USkeleton class="h-3 w-16 mb-1" />
        <USkeleton class="h-5 w-24" />
      </div>
      <div class="text-right">
        <USkeleton class="h-3 w-16 mb-1 ml-auto" />
        <USkeleton class="h-5 w-24 ml-auto" />
      </div>
    </div>
  </UCard>
</template>
