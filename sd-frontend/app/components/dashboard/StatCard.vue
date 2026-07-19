<template>
  <UCard :class="cardBorder">
    <div class="flex items-center gap-4">
      <div class="flex-shrink-0">
        <div
          class="w-10 h-10 rounded-full flex items-center justify-center border"
          :class="[iconColor.bg, iconColor.border]"
        >
          <UIcon
            :name="icon"
            class="size-6"
            :class="iconColor.text"
          />
        </div>
      </div>
      <div>
        <p class="text-sm font-medium text-dimmed">
          {{ statsLabel }}
        </p>
        <p
          class="text-2xl font-bold"
          :class="statsColor"
        >
          {{ statsValue }}
        </p>
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
    default: 'blue',
  },
  type: {
    type: String,
    default: 'base',
  },
})

const statsLabel = computed(() => {
  return props.stats.label
})

const statsType = computed(() => {
  return props.type
})

const statsValue = computed(() => {
  const value = props.stats.value || 0
  if (statsType.value === 'currency') {
    return `${formatAmount(value)} €`
  }
  return value
})

const statsColor = computed(() => {
  switch (props.stats.color) {
    case 'teal':
      return 'text-primary'
    case 'green':
      return 'text-success'
    case 'red':
      return 'text-error'
    case 'yellow':
      return 'text-warning'
    case 'purple':
      return 'text-secondary'
    case 'rose':
      return 'text-secondary'
    case 'pink':
      return 'text-primary'
    case 'amber':
      return 'text-warning'
    default:
      return 'text-primary'
  }
})

const cardBorder = computed(() => {
  const borders = {
    teal: 'border-l-4 border-l-primary',
    green: 'border-l-4 border-l-success',
    red: 'border-l-4 border-l-error',
    yellow: 'border-l-4 border-l-warning',
    purple: 'border-l-4 border-l-secondary',
    rose: 'border-l-4 border-l-secondary',
    pink: 'border-l-4 border-l-primary',
    amber: 'border-l-4 border-l-warning',
  }
  return borders[props.color] || ''
})

const iconColor = computed(() => {
  switch (props.color) {
    case 'teal':
      return { bg: 'bg-primary/10', text: 'text-primary', border: 'border-primary' }
    case 'green':
      return { bg: 'bg-success/10', text: 'text-success', border: 'border-success' }
    case 'red':
      return { bg: 'bg-error/10', text: 'text-error', border: 'border-error' }
    case 'yellow':
      return { bg: 'bg-warning/10', text: 'text-warning', border: 'border-warning' }
    case 'purple':
      return { bg: 'bg-secondary/10', text: 'text-secondary', border: 'border-secondary' }
    case 'rose':
      return { bg: 'bg-secondary/10', text: 'text-secondary', border: 'border-secondary' }
    case 'pink':
      return { bg: 'bg-primary/10', text: 'text-primary', border: 'border-primary' }
    case 'amber':
      return { bg: 'bg-warning/10', text: 'text-warning', border: 'border-warning' }
    default:
      return { bg: 'bg-muted/10', text: 'text-muted', border: 'border-muted' }
  }
})
</script>
