<template>
  <div class="space-y-3">
    <div
      v-if="attachments.length === 0 && !isLoading"
      class="text-sm text-neutral-500"
    >
      {{ $t('expenses.attachments.empty') }}
    </div>
    <div
      v-else
      class="flex flex-wrap gap-3"
    >
      <div
        v-for="attachment in attachments"
        :key="attachment.id"
        class="group relative"
      >
        <button
          type="button"
          class="block rounded-lg focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          :title="attachment.filenameOriginal"
          @click="openAttachment(attachment)"
        >
          <img
            v-if="isImage(attachment)"
            :src="thumbnailUrl(attachment)"
            :alt="attachment.filenameOriginal"
            class="size-16 rounded-lg object-cover"
          >
          <div
            v-else
            class="size-16 rounded-lg bg-neutral-100 flex items-center justify-center"
          >
            <UIcon
              name="i-lucide-file-text"
              class="size-8 text-neutral-400"
            />
          </div>
        </button>
        <UButton
          icon="i-lucide-x"
          size="xs"
          color="error"
          variant="solid"
          square
          class="absolute -top-1 -right-1 opacity-0 group-hover:opacity-100 transition-opacity"
          :aria-label="$t('expenses.attachments.deleteButton')"
          @click="confirmDelete(attachment)"
        />
      </div>
    </div>
    <UiLoadingSpinner
      v-if="isLoading"
      container-class="flex justify-center py-2"
      spinner-class="w-5 h-5 animate-spin text-muted"
    />
    <UiReceiptPreviewModal
      v-model="isPreviewOpen"
      :image-url="previewUrl"
    />
  </div>
</template>

<script setup lang="ts">
import type { ExpenseAttachment } from '~/types/domain'

interface Props {
  groupId: string
  expenseId: string
  /**
   * Optional shared useExpenseAttachments instance. When provided, the
   * component uses its reactive state and methods so uploads performed by the
   * parent (e.g. the edit page's "Add receipt") show up in this list. When
   * omitted, the component creates its own instance (standalone usage).
   */
  instance?: ReturnType<typeof useExpenseAttachments>
}
const props = defineProps<Props>()

const { t } = useI18n()
const modal = useModal()
const { attachments, isLoading, fetchAttachments, downloadAttachment, getAttachmentUrl, removeAttachment } = props.instance ?? useExpenseAttachments(props.groupId, props.expenseId)

const isPreviewOpen = ref(false)
const previewUrl = ref<string | null>(null)

const isImage = (attachment: ExpenseAttachment): boolean => attachment.mimeType.startsWith('image/')

// Thumbnails are fetched once per attachment and cached in a reactive record so
// re-renders (e.g. after a delete) don't re-download every image.
const thumbnailUrls = ref<Record<string, string>>({})

const thumbnailUrl = (attachment: ExpenseAttachment): string | null => {
  const cached = thumbnailUrls.value[attachment.id]
  if (cached) return cached
  // Kick off the fetch; the URL is stored when it resolves.
  getAttachmentUrl(attachment).then((url) => {
    if (url) thumbnailUrls.value[attachment.id] = url
  })
  return null
}

const openAttachment = async (attachment: ExpenseAttachment): Promise<void> => {
  if (isImage(attachment)) {
    const url = await getAttachmentUrl(attachment)
    if (!url) return
    previewUrl.value = url
    isPreviewOpen.value = true
    return
  }
  await downloadAttachment(attachment)
}

const confirmDelete = async (attachment: ExpenseAttachment): Promise<void> => {
  const confirmed = await modal.error({
    title: t('expenses.attachments.deleteTitle'),
    subtitle: t('expenses.attachments.deleteConfirm'),
    content: t('expenses.attachments.deleteContent'),
    confirmText: t('expenses.attachments.deleteButton'),
    cancelText: t('common.cancel'),
  })
  if (!confirmed) return

  try {
    await removeAttachment(attachment.id)
  }
  catch {
    // Error shown via toast
  }
}

// Revoke the preview object URL when the modal closes.
watch(isPreviewOpen, (open) => {
  if (!open && previewUrl.value) {
    URL.revokeObjectURL(previewUrl.value)
    previewUrl.value = null
  }
})

onMounted(() => {
  fetchAttachments().catch(() => {
    // Error shown via toast
  })
})
</script>
