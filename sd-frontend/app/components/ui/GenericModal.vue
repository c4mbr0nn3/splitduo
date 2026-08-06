<script setup lang="ts">
import type { ButtonColor } from '~/composables/ui/useModal'

interface Props {
  title: string
  subtitle?: string
  content?: string
  color?: ButtonColor
  icon?: string
  iconColor?: string
  confirmText?: string
  cancelText?: string
  confirmColor?: ButtonColor | ''
  cancelColor?: ButtonColor
  loading?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  subtitle: '',
  content: '',
  color: 'primary',
  icon: '',
  iconColor: '',
  confirmText: 'Confirm',
  cancelText: 'Cancel',
  confirmColor: '',
  cancelColor: 'neutral',
  loading: false,
})

const emit = defineEmits<{
  close: [value: boolean]
}>()

const isOpen = defineModel<boolean>('open', { default: true })

const isProcessing = ref(false)

// Compute actual colors with fallbacks
const actualConfirmColor = computed(() => props.confirmColor || props.color)
const actualIconColor = computed(() => props.iconColor || props.color)

const handleCancel = () => {
  if (isProcessing.value) return
  emit('close', false)
  isOpen.value = false
}

const handleConfirm = async () => {
  if (isProcessing.value) return
  isProcessing.value = true
  emit('close', true)
  isOpen.value = false
  isProcessing.value = false
}

// Handle escape key and overlay click
const handleClose = () => {
  if (!isProcessing.value) {
    handleCancel()
  }
}
</script>

<template>
  <UModal
    v-model:open="isOpen"
    :dismissible="!isProcessing && !loading"
    @update:open="(value) => { if (!value) handleClose() }"
  >
    <template #header>
      <div class="flex items-start gap-3">
        <div
          v-if="icon"
          class="shrink-0"
        >
          <UIcon
            :name="icon"
            :class="`text-${actualIconColor}`"
            class="size-6"
          />
        </div>
        <div class="flex-1 min-w-0">
          <h3 class="text-lg font-semibold">
            {{ title }}
          </h3>
          <p
            v-if="subtitle"
            class="text-sm text-muted"
          >
            {{ subtitle }}
          </p>
        </div>
      </div>
    </template>

    <template #body>
      <div
        v-if="content"
        v-html="content"
      />
    </template>

    <template #footer>
      <div class="flex gap-2 w-full">
        <UButton
          :color="cancelColor"
          variant="outline"
          :disabled="isProcessing || loading"
          class="ml-auto"
          @click="handleCancel"
        >
          {{ cancelText }}
        </UButton>
        <UButton
          :color="actualConfirmColor"
          :loading="isProcessing || loading"
          :disabled="isProcessing || loading"
          @click="handleConfirm"
        >
          {{ confirmText }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
