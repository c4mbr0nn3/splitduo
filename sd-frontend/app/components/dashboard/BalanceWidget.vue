<template>
  <UCard
    class="sd-surface"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <!-- Header row: section label + groups badge -->
    <div class="flex items-center justify-between gap-2 mb-4">
      <h2 class="text-lg font-semibold text-highlighted">
        {{ label }}
      </h2>
      <UBadge
        color="neutral"
        variant="soft"
        size="sm"
        icon="i-lucide-users"
        :label="groupsLabel"
      />
    </div>

    <!-- Hero: net balance -->
    <div class="flex items-center gap-3">
      <div
        class="size-12 rounded-full flex items-center justify-center shrink-0"
        :class="netTint"
      >
        <UIcon
          :name="netIcon"
          class="size-6"
          :class="netText"
        />
      </div>
      <p
        class="text-3xl font-bold sd-tabular truncate min-w-0"
        :class="netText"
      >
        {{ netPrefix }}{{ formatCurrency(net) }}
      </p>
    </div>

    <USeparator class="my-4" />

    <!-- Sub stats -->
    <div class="grid grid-cols-2 gap-4">
      <div>
        <p class="text-xs text-dimmed mb-1">
          {{ $t('dashboard.youOwe') }}
        </p>
        <p class="font-semibold sd-tabular text-error truncate">
          {{ formatCurrency(youOwe) }}
        </p>
      </div>
      <div class="text-right">
        <p class="text-xs text-dimmed mb-1">
          {{ $t('dashboard.youreOwed') }}
        </p>
        <p class="font-semibold sd-tabular text-success truncate">
          {{ formatCurrency(youreOwed) }}
        </p>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { formatCurrency } from '~/utils/currency'

const { t } = useI18n()

interface Props {
  label: string
  groups: number
  youOwe: number
  youreOwed: number
}

const props = defineProps<Props>()

const net = computed(() => Number(props.youreOwed) - Number(props.youOwe))
const netPrefix = computed(() => net.value > 0 ? '+' : '')

const groupsLabel = computed(() => t('dashboard.groupsCount', { count: props.groups }, props.groups))

// Tone: positive=success, negative=error, zero=primary (teal)
const netTint = computed(() => {
  if (net.value > 0) return 'bg-success/10'
  if (net.value < 0) return 'bg-error/10'
  return 'bg-primary/10'
})
const netText = computed(() => {
  if (net.value > 0) return 'text-success'
  if (net.value < 0) return 'text-error'
  return 'text-primary'
})
const netIcon = computed(() => {
  if (net.value > 0) return 'i-lucide-trending-up'
  if (net.value < 0) return 'i-lucide-trending-down'
  return 'i-lucide-scale'
})
</script>
