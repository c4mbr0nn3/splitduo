<template>
  <UButton
    icon="i-lucide-scan-line"
    size="sm"
    color="primary"
    variant="soft"
    :aria-label="$t('expenses.scanReceipt')"
    :title="$t('expenses.scanReceipt')"
    :loading="isScanning"
    @click="fileInput?.click()"
  >
    <span class="hidden sm:inline">{{ $t('expenses.scanReceipt') }}</span>
  </UButton>
  <input
    ref="fileInput"
    type="file"
    accept="image/*"
    capture="environment"
    class="hidden"
    @change="onFileSelected"
  >
</template>

<script setup lang="ts">
interface Props {
  groupId?: string | null
}
const props = withDefaults(defineProps<Props>(), {
  groupId: null,
})

const { scanReceipt, isScanning } = useReceiptScan()
const fileInput = ref<HTMLInputElement | null>(null)

const onFileSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return
  await scanReceipt(file, props.groupId)
  target.value = ''
}
</script>
