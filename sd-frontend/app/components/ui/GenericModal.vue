<script setup>
const props = defineProps({
  title: {
    type: String,
    required: true,
  },
  subtitle: {
    type: String,
    default: '',
  },
  content: {
    type: String,
    default: '',
  },
  color: {
    type: String,
    default: 'primary',
  },
  icon: {
    type: String,
    default: '',
  },
  iconColor: {
    type: String,
    default: '',
  },
  confirmText: {
    type: String,
    default: 'Confirm',
  },
  cancelText: {
    type: String,
    default: 'Cancel',
  },
  confirmColor: {
    type: String,
    default: '',
  },
  cancelColor: {
    type: String,
    default: 'neutral',
  },
  loading: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['close'])

const isOpen = defineModel('open', { type: Boolean, default: true })

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
