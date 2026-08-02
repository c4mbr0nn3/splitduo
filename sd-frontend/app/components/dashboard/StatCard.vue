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
          <p class="min-w-0 text-2xl font-semibold sd-tabular text-highlighted truncate">
            {{ numericValue }}
          </p>
          <span
            v-if="props.type === 'currency'"
            class="shrink-0 text-base font-medium text-muted"
          >
            €
          </span>
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
interface StatData {
  label: string
  value: number | string
  color?: string
}

interface Props {
  stats: StatData
  icon: string
  color?: string
  type?: string
}
const props = withDefaults(defineProps<Props>(), {
  color: 'neutral',
  type: 'base',
})

const statsLabel = computed(() => props.stats.label)

const numericValue = computed(() => {
  const v = props.stats.value || 0
  return props.type === 'currency' ? formatAmount(v) : v
})

const tone = computed(() => props.stats.color || props.color)
const toneMap: Record<string, { tint: string, text: string }> = {
  neutral: { tint: 'bg-muted/10', text: 'text-muted' },
  teal: { tint: 'bg-primary/10', text: 'text-primary' },
  primary: { tint: 'bg-primary/10', text: 'text-primary' },
  green: { tint: 'bg-success/10', text: 'text-success' },
  success: { tint: 'bg-success/10', text: 'text-success' },
  red: { tint: 'bg-error/10', text: 'text-error' },
  error: { tint: 'bg-error/10', text: 'text-error' },
  yellow: { tint: 'bg-warning/10', text: 'text-warning' },
  warning: { tint: 'bg-warning/10', text: 'text-warning' },
  rose: { tint: 'bg-secondary/10', text: 'text-secondary' },
  secondary: { tint: 'bg-secondary/10', text: 'text-secondary' },
  info: { tint: 'bg-info/10', text: 'text-info' },
}
function getTone(key: string) {
  return toneMap[key] ?? toneMap.neutral!
}
const iconTint = computed(() => getTone(tone.value).tint)
const iconText = computed(() => getTone(tone.value).text)
</script>
