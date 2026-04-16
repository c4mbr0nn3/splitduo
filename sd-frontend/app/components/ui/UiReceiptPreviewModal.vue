<template>
  <UModal
    v-model:open="isOpen"
    title="Receipt Preview"
  >
    <template #body>
      <div
        class="max-h-[70vh]"
        :class="zoomed ? 'overflow-auto' : 'overflow-y-auto'"
      >
        <img
          :src="imageUrl"
          :class="zoomed ? 'max-w-none h-auto rounded cursor-zoom-out' : 'w-full h-auto rounded cursor-zoom-in'"
          alt="Receipt"
          @click="zoomed = !zoomed"
        >
      </div>
    </template>
    <template #footer>
      <div class="flex gap-2 w-full">
        <UButton
          :icon="zoomed ? 'i-lucide-zoom-out' : 'i-lucide-zoom-in'"
          :label="zoomed ? 'Fit' : 'Zoom'"
          variant="outline"
          @click="zoomed = !zoomed"
        />
        <UButton
          label="Close"
          variant="outline"
          class="flex-1"
          @click="isOpen = false"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup>
const props = defineProps({
  modelValue: {
    type: Boolean,
    required: true,
  },
  imageUrl: {
    type: String,
    default: null,
  },
})

const emit = defineEmits(['update:modelValue'])

const zoomed = ref(false)

const isOpen = computed({
  get: () => props.modelValue,
  set: (val) => {
    if (!val) zoomed.value = false
    emit('update:modelValue', val)
  },
})
</script>
