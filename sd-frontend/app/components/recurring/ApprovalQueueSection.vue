<template>
  <UCard
    variant="soft"
    color="warning"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <template #header>
      <div class="flex items-center gap-2">
        <UIcon
          name="i-lucide-inbox"
          class="size-5 text-warning shrink-0"
        />
        <h2 class="text-base font-semibold text-highlighted">
          {{ $t('recurring.approvalQueue') }}
        </h2>
        <UBadge
          color="warning"
          variant="solid"
          size="sm"
          :label="String(pendingInstances.length)"
        />
      </div>
    </template>

    <div class="space-y-2">
      <div
        v-for="instance in pendingInstances"
        :key="instance.id"
        class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between py-2 border-b border-[var(--sd-surface-border)] last:border-0"
      >
        <div class="min-w-0">
          <p class="text-sm font-medium text-highlighted truncate">
            {{ instance.templateTitle }}
          </p>
          <p class="text-xs text-dimmed mt-0.5">
            {{ formatDateString(instance.periodDate) }} · {{ formatCurrency(instance.amount) }}
          </p>
        </div>
        <div class="flex items-center gap-2 shrink-0">
          <UButton
            icon="i-lucide-check"
            size="sm"
            :label="$t('recurring.approve')"
            :loading="isApproving === instance.id"
            @click="emit('approve', instance.id)"
          />
          <UButton
            icon="i-lucide-x"
            size="sm"
            color="error"
            variant="outline"
            :label="$t('recurring.reject')"
            :loading="isRejecting === instance.id"
            @click="confirmReject(instance)"
          />
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { DeepReadonly } from 'vue'
import type { RecurringExpenseInstance } from '~/types/domain'

defineProps<{
  pendingInstances: DeepReadonly<RecurringExpenseInstance[]>
  isApproving?: string | null
  isRejecting?: string | null
}>()

const emit = defineEmits<{
  approve: [instanceId: string]
  reject: [instanceId: string]
}>()

const { t } = useI18n()
const modal = useModal()

const confirmReject = async (instance: DeepReadonly<RecurringExpenseInstance>): Promise<void> => {
  const confirmed = await modal.error({
    title: t('recurring.rejectTitle'),
    subtitle: t('recurring.rejectConfirm'),
    content: t('recurring.rejectContent', { title: instance.templateTitle }),
    confirmText: t('recurring.reject'),
    cancelText: t('common.cancel'),
  })

  if (confirmed) {
    emit('reject', instance.id)
  }
}
</script>
