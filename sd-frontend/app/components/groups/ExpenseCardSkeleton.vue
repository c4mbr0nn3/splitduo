<template>
  <UCard>
    <div class="mb-3">
      <div class="flex justify-between mb-3">
        <USkeleton class="h-3 w-24" />
        <USkeleton class="h-3 w-20" />
      </div>
      <div class="flex justify-between items-start mb-1">
        <USkeleton class="h-5 w-40" />
        <USkeleton class="h-5 w-16" />
      </div>
    </div>
    <div class="flex flex-col gap-2">
      <div class="flex items-center gap-2">
        <USkeleton class="h-3 w-20" />
        <USkeleton class="h-3 w-16" />
      </div>
      <div class="flex justify-between items-center">
        <USkeleton class="h-5 w-24 rounded-full" />
        <div class="flex items-center gap-1">
          <USkeleton class="w-7 h-7 rounded" />
          <USkeleton class="w-7 h-7 rounded" />
        </div>
      </div>
    </div>
  </UCard>
</template>
