<template>
  <UCard :ui="{ body: 'p-4 sm:p-5' }">
    <div class="flex items-center gap-3">
      <USkeleton class="w-10 h-10 rounded-full flex-shrink-0" />
      <div class="space-y-2 flex-1">
        <USkeleton class="h-4 w-32" />
        <USkeleton class="h-3 w-20" />
      </div>
    </div>
  </UCard>
</template>
