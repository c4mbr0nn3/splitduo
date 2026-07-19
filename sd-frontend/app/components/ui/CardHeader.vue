<template>
  <div
    :class="[
      'flex items-baseline gap-3',
      variant === 'centered' ? 'justify-center' : 'flex-1',
    ]"
  >
    <UButton
      v-if="backTo"
      icon="i-lucide-arrow-left"
      variant="ghost"
      size="sm"
      square
      @click="handleBack"
    />
    <div :class="variant === 'centered' ? 'text-center w-full' : 'flex-1'">
      <h1 :class="getDefaultTitleClass">
        {{ title }}
      </h1>
      <p
        v-if="subtitle"
        class="text-sm text-muted mt-1"
      >
        {{ subtitle }}
      </p>
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  title: {
    type: String,
    required: true,
  },
  subtitle: {
    type: String,
    default: null,
  },
  backTo: {
    type: String,
    default: null,
  },
  variant: {
    type: String,
    default: 'default',
    validator: value => ['default', 'centered'].includes(value),
  },
  size: {
    type: String,
    default: 'md',
    validator: value => ['md', 'lg'].includes(value),
  },
})

const emit = defineEmits(['back'])

const getDefaultTitleClass = computed(() => {
  if (props.variant === 'centered') {
    if (props.size === 'lg') {
      return 'text-2xl font-bold text-center'
    }
    return 'text-xl font-bold text-center'
  }
  if (props.size === 'lg') {
    return 'text-2xl font-bold text-primary'
  }
  return 'text-xl font-semibold'
})

const handleBack = () => {
  if (props.backTo) {
    navigateTo(props.backTo)
    emit('back')
  }
}
</script>
