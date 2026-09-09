<template>
  <UCard
    class="sd-surface h-fit"
    :ui="{ body: 'p-4 sm:p-5 space-y-3' }"
  >
    <p class="text-xs font-medium text-muted uppercase tracking-wide">
      {{ $t('dashboard.quickActions') }}
    </p>
    <UButton
      v-for="(action, index) in actions"
      :key="action.id"
      :to="action.to"
      :icon="action.icon"
      :label="action.label"
      :variant="index === 0 ? 'solid' : 'outline'"
      :color="index === 0 ? 'primary' : 'neutral'"
      size="lg"
      block
      class="justify-start"
      @click="action.onClick?.()"
    />
  </UCard>
</template>

<script setup lang="ts">
export interface QuickAction {
  id: string
  label: string
  icon: string
  to?: string
  onClick?: () => void
}

defineProps<{
  actions: QuickAction[]
}>()
</script>
