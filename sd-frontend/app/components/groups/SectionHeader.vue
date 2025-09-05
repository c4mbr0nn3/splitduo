<template>
  <div class="grid grid-cols-1 items-start">
    <div class="flex justify-between">
      <div class="flex min-w-0">
        <h1 class="text-xl font-bold text-primary truncate">
          {{ group?.name || 'Group Details' }}
        </h1>
      </div>
      <UBadge
        v-if="group?.memberCount"
        variant="soft"
        icon="i-lucide-users"
        class="ml-auto"
        :label="`${group.memberCount}`"
      />
    </div>
    <p
      v-if="group?.description"
      class="text-sm text-muted mt-1"
    >
      {{ group.description }}
    </p>
  </div>
</template>

<script setup>
defineProps({
  group: {
    type: Object,
    default: null,
  },
})
</script>
