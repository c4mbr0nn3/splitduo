<template>
  <UModal>
    <slot name="button" />
    <template #title>
      <div class="flex items-center gap-2">
        <UIcon
          :name="icon"
          :class="iconColorClass"
        />
        <h3 class="text-lg font-semibold">
          {{ title }}
        </h3>
      </div>
    </template>
    <template #body>
      <div class="space-y-3">
        <p class="text-muted">
          {{ message }}
        </p>
        <p
          v-if="subtitle"
          class="text-sm text-muted"
        >
          {{ subtitle }}
        </p>
      </div>
    </template>
    <template #footer>
      <div class="flex items-center gap-3 justify-end ml-auto">
        <UButton
          variant="ghost"
          color="neutral"
          :loading="isProcessing"
          @click="handleCancel"
        >
          {{ cancelText }}
        </UButton>
        <UButton
          variant="outline"
          :color="confirmColor"
          :loading="isProcessing"
          @click="handleConfirm"
        >
          {{ confirmText }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup>
defineProps({
  title: {
    type: String,
    default: 'Confirm Action',
  },
  message: {
    type: String,
    required: true,
  },
  subtitle: {
    type: String,
    default: null,
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
    default: 'red',
  },
  icon: {
    type: String,
    default: 'i-lucide-alert-triangle',
  },
  iconColorClass: {
    type: String,
    default: 'text-red-500',
  },
  isProcessing: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['confirm', 'cancel'])

const handleConfirm = () => {
  emit('confirm')
}

const handleCancel = () => {
  emit('cancel')
  isOpen.value = false
}
</script>
