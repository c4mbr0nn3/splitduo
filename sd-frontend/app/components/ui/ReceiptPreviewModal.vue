<template>
  <UModal
    v-model:open="isOpen"
    :title="$t('expenses.receiptPreview')"
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
          :label="zoomed ? $t('expenses.fit') : $t('expenses.zoom')"
          variant="outline"
          @click="zoomed = !zoomed"
        />
        <UButton
          :label="$t('expenses.close')"
          variant="outline"
          class="flex-1"
          @click="isOpen = false"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
interface Props {
  modelValue: boolean
  imageUrl?: string | null
}
const props = withDefaults(defineProps<Props>(), {
  imageUrl: null,
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

const zoomed = ref(false)

const isOpen = computed({
  get: () => props.modelValue,
  set: (val) => {
    if (!val) zoomed.value = false
    emit('update:modelValue', val)
  },
})
</script>
