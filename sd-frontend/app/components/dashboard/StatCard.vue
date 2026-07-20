<template>
  <UCard
    class="sd-surface"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <div class="flex items-start gap-3">
      <div
        class="size-10 rounded-full flex items-center justify-center shrink-0 mt-0.5"
        :class="iconTint"
      >
        <UIcon
          :name="icon"
          class="size-5"
          :class="iconText"
        />
      </div>
      <div class="min-w-0 flex-1">
        <p class="text-sm font-medium text-muted truncate">
          {{ statsLabel }}
        </p>
        <div class="flex items-baseline justify-between gap-2">
          <p class="text-2xl font-semibold sd-tabular text-highlighted">
            {{ numericValue }}
          </p>
          <span
            v-if="props.type === 'currency'"
            class="text-base font-medium text-muted"
          >
            €
          </span>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup>
const props = defineProps({
  stats: {
    type: Object,
    required: true,
  },
  icon: {
    type: String,
    required: true,
  },
  color: {
    type: String,
    default: 'neutral',
  },
  type: {
    type: String,
    default: 'base',
  },
})

const statsLabel = computed(() => props.stats.label)

const numericValue = computed(() => {
  const v = props.stats.value || 0
  return props.type === 'currency' ? formatAmount(v) : v
})

const tone = computed(() => props.stats.color || props.color)
const toneMap = {
  neutral: { tint: 'bg-muted/10', text: 'text-muted' },
  teal: { tint: 'bg-primary/10', text: 'text-primary' },
  primary: { tint: 'bg-primary/10', text: 'text-primary' },
  green: { tint: 'bg-success/10', text: 'text-success' },
  success: { tint: 'bg-success/10', text: 'text-success' },
  red: { tint: 'bg-error/10', text: 'text-error' },
  error: { tint: 'bg-error/10', text: 'text-error' },
  yellow: { tint: 'bg-warning/10', text: 'text-warning' },
  warning: { tint: 'bg-warning/10', text: 'text-warning' },
}
const iconTint = computed(() => (toneMap[tone.value] || toneMap.neutral).tint)
const iconText = computed(() => (toneMap[tone.value] || toneMap.neutral).text)
</script>
