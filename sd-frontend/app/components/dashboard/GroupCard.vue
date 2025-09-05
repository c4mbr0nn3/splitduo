<template>
  <UCard
    class="cursor-pointer"
    variant="subtle"
  >
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <div class="border border-primary text-primary rounded-full flex items-center justify-center w-10 h-10">
          <UIcon
            name="i-lucide-users"
            class="size-6"
          />
        </div>
        <div>
          <h3 class="font-medium text-primary">
            {{ group.name }}
          </h3>
          <p class="text-xs text-muted">
            {{ group.memberCount || 0 }} member(s)
          </p>
        </div>
      </div>
      <UIcon
        name="i-lucide-chevron-right"
        class="size-5 text-primary"
      />
    </div>
  </UCard>
</template>

<script setup>
defineProps({
  group: {
    type: Object,
    required: true,
  },
})
</script>
