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
    <slot name="actions" />
  </div>
</template>

<script setup lang="ts">
interface Props {
  title: string
  subtitle?: string | null
  backTo?: string | null
  variant?: 'default' | 'centered'
  size?: 'md' | 'lg'
}
const props = withDefaults(defineProps<Props>(), {
  subtitle: null,
  backTo: null,
  variant: 'default',
  size: 'md',
})

const emit = defineEmits<{
  back: []
}>()

const getDefaultTitleClass = computed(() => {
  if (props.variant === 'centered') {
    if (props.size === 'lg') {
      return 'text-2xl font-bold text-center'
    }
    return 'text-xl font-bold text-center'
  }
  if (props.size === 'lg') {
    return 'text-2xl font-semibold text-highlighted tracking-tight'
  }
  return 'text-lg font-semibold text-highlighted'
})

const { goBack } = useSmartBack(props.backTo ?? '')

const handleBack = () => {
  if (props.backTo) {
    emit('back')
    goBack()
  }
}
</script>
