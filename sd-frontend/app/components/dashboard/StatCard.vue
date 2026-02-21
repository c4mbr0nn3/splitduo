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
    return `${value.toFixed(2)} €`
  }
  return value
})

const statsColor = computed(() => {
  switch (props.stats.color) {
    case 'teal':
      return 'text-teal-600'
    case 'green':
      return 'text-green-600'
    case 'red':
      return 'text-red-600'
    case 'yellow':
      return 'text-yellow-600'
    case 'purple':
      return 'text-purple-600'
    case 'rose':
      return 'text-rose-600'
    case 'pink':
      return 'text-pink-600'
    case 'amber':
      return 'text-amber-600'
    default:
      return 'text-white'
  }
})

const cardBorder = computed(() => {
  const borders = {
    teal: 'border-l-4 border-l-teal-400 dark:border-l-teal-500',
    green: 'border-l-4 border-l-green-400 dark:border-l-green-500',
    red: 'border-l-4 border-l-red-400 dark:border-l-red-500',
    yellow: 'border-l-4 border-l-yellow-400 dark:border-l-yellow-500',
    purple: 'border-l-4 border-l-purple-400 dark:border-l-purple-500',
    rose: 'border-l-4 border-l-rose-400 dark:border-l-rose-500',
    pink: 'border-l-4 border-l-pink-400 dark:border-l-pink-500',
    amber: 'border-l-4 border-l-amber-400 dark:border-l-amber-500',
  }
  return borders[props.color] || ''
})

const iconColor = computed(() => {
  switch (props.color) {
    case 'teal':
      return { bg: 'bg-teal-100', text: 'text-teal-600', border: 'border-teal-600' }
    case 'green':
      return { bg: 'bg-green-100', text: 'text-green-600', border: 'border-green-600' }
    case 'red':
      return { bg: 'bg-red-100', text: 'text-red-600', border: 'border-red-600' }
    case 'yellow':
      return { bg: 'bg-yellow-100', text: 'text-yellow-600', border: 'border-yellow-600' }
    case 'purple':
      return { bg: 'bg-purple-100', text: 'text-purple-600', border: 'border-purple-600' }
    case 'rose':
      return { bg: 'bg-rose-100', text: 'text-rose-600', border: 'border-rose-600' }
    case 'pink':
      return { bg: 'bg-pink-100', text: 'text-pink-600', border: 'border-pink-600' }
    case 'amber':
      return { bg: 'bg-amber-100', text: 'text-amber-600', border: 'border-amber-600' }
    default:
      return { bg: 'bg-gray-100', text: 'text-toned', border: 'border-gray-600' }
  }
})
</script>
