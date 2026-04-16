<template>
  <input
    ref="fileInput"
    type="file"
    accept="image/*"
    capture="environment"
    class="hidden"
    @change="onFileSelected"
  >
  <UButton
    label="Scan Receipt"
    icon="i-lucide-scan"
    size="sm"
    variant="outline"
    :loading="isScanning"
    @click="fileInput.click()"
  />
</template>

<script setup>
const props = defineProps({
  groupId: {
    type: [String, Number],
    default: null,
  },
})

const { scanReceipt, isScanning } = useReceiptScan()
const fileInput = ref(null)

const onFileSelected = async (event) => {
  const file = event.target.files?.[0]
  if (!file) return
  await scanReceipt(file, props.groupId)
  event.target.value = ''
}
</script>
