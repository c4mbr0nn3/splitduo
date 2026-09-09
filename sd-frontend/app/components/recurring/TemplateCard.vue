<template>
  <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
    <div class="min-w-0">
      <div class="flex items-center gap-2 flex-wrap">
        <h3 class="text-base font-medium text-highlighted truncate max-w-[14rem] sm:max-w-none">
          {{ template.title }}
        </h3>
        <UBadge
          v-if="Number(template.pendingCount) > 0"
          color="warning"
          variant="subtle"
          size="sm"
          :label="$t('recurring.pendingBadge', { count: Number(template.pendingCount) }, Number(template.pendingCount))"
        />
        <UBadge
          v-if="Number(template.missedCount) > 0"
          color="warning"
          variant="subtle"
          size="sm"
          :label="$t('recurring.missedBadge', { count: Number(template.missedCount) }, Number(template.missedCount))"
        />
        <UBadge
          v-else-if="Number(template.skippedPeriodDates?.length) > 0"
          color="neutral"
          variant="subtle"
          size="sm"
          :label="$t('recurring.skippedBadge', { count: Number(template.skippedPeriodDates?.length) }, Number(template.skippedPeriodDates?.length))"
        />
      </div>
      <p class="text-xs text-dimmed mt-0.5 truncate">
        {{ summaryText }}
      </p>
    </div>
    <div class="flex items-center gap-3 shrink-0">
      <p class="text-xs text-muted">
        {{ $t('recurring.next') }} {{ nextOccurrenceText }}
      </p>
      <UTooltip
        v-if="!template.isActive && template.pausedReason"
        :text="template.pausedReason"
      >
        <UBadge
          color="warning"
          variant="subtle"
          :label="$t('recurring.pausedBadge')"
        />
      </UTooltip>
      <USwitch
        :model-value="template.isActive"
        :aria-label="$t('recurring.activeLabel')"
        @update:model-value="value => emit('toggle', value)"
      />
      <UiButtonDropdown
        icon-only
        dropdown-icon="i-lucide-ellipsis-vertical"
        size="md"
        square
        variant="ghost"
        color="neutral"
        :items="dropdownItems"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import type { DeepReadonly } from 'vue'
import type { RecurringExpenseTemplate } from '~/types/domain'

const props = defineProps<{
  template: DeepReadonly<RecurringExpenseTemplate>
}>()

const emit = defineEmits<{
  toggle: [isActive: boolean]
  edit: []
  delete: []
}>()

const { t } = useI18n()
const modal = useModal()

const summaryText = computed(() =>
  props.template.summary || t('recurring.noScheduleSummary'))

const nextOccurrenceText = computed(() =>
  props.template.nextOccurrence
    ? formatDateString(props.template.nextOccurrence)
    : t('recurring.noNextOccurrence'))

const dropdownItems = computed(() => [
  {
    label: t('expenses.edit'),
    icon: 'i-lucide-edit-2',
    color: 'info' as const,
    onSelect: () => emit('edit'),
  },
  { type: 'separator' },
  {
    label: t('expenses.delete'),
    icon: 'i-lucide-trash-2',
    color: 'error' as const,
    onSelect: confirmDelete,
  },
])

const confirmDelete = async (): Promise<void> => {
  const confirmed = await modal.error({
    title: t('recurring.deleteTitle'),
    subtitle: t('recurring.deleteConfirm'),
    content: t('recurring.deleteContent', { title: props.template.title }),
    confirmText: t('recurring.deleteButton'),
    cancelText: t('common.cancel'),
  })

  if (confirmed) {
    emit('delete')
  }
}
</script>
