<template>
  <div class="flex gap-1 border-b border-default pb-3 mb-4">
    <UButton
      :to="`/groups/${groupId}`"
      label="Expenses"
      icon="i-lucide-receipt"
      size="sm"
      :variant="isStats ? 'ghost' : 'soft'"
    />
    <UButton
      :to="`/groups/${groupId}/stats`"
      label="Stats"
      icon="i-lucide-bar-chart-3"
      size="sm"
      :variant="isStats ? 'soft' : 'ghost'"
    />
  </div>
</template>

<script setup>
defineProps({
  groupId: {
    type: String,
    required: true,
  },
})

const route = useRoute()
const isStats = computed(() => route.path.endsWith('/stats'))
</script>
