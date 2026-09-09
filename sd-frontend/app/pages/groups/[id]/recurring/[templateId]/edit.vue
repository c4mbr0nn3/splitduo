<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8">
    <UiLoadingSpinner
      v-if="pageLoading"
      :text="$t('recurring.loading')"
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-repeat"
      :title="$t('groups.unableToLoad')"
    >
      <template #action>
        <UButton
          color="primary"
          variant="outline"
          size="sm"
          @click="retryLoad"
        >
          {{ $t('groups.retry') }}
        </UButton>
      </template>
    </UiEmptyState>

    <RecurringExpenseForm
      v-else
      v-model="formData"
      :title="$t('recurring.editTemplate')"
      :submit-label="$t('recurring.updateTemplate')"
      :group-id="groupId"
      :loading="isUpdating"
      :orphaned-splits="orphanedSplits"
      @submit="onSubmit"
      @cancel="goBack"
    />
  </div>
</template>

<script setup lang="ts">
import type { RecurringExpenseTemplate, GroupMember } from '~/types/domain'
import type { RecurringFormModel, RecurringFormPayload, RecurringFormSplit } from '~/components/recurring/RecurringExpenseForm.vue'

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)
const templateId = String(route.params.templateId)

const { fetchTemplate, currentTemplate, updateTemplate } = useRecurringExpenses(groupId)
const { fetchGroupMembers } = useGroups()

const pageLoading = ref(true)
const loadError = ref(false)
const isUpdating = ref(false)

const retryLoad = async (): Promise<void> => {
  loadError.value = false
  await loadTemplate()
}

const loadTemplate = async (): Promise<void> => {
  pageLoading.value = true
  loadError.value = false
  try {
    const [members] = await Promise.all([
      fetchGroupMembers(groupId),
      fetchTemplate(templateId),
    ])
    groupMembers.value = members ?? []
  }
  catch {
    loadError.value = true
  }
  finally {
    pageLoading.value = false
  }
}

// Map the fetched template onto the form model
const formData = ref<RecurringFormModel>({
  templateId,
  title: '',
  description: '',
  amount: '',
  paidByUserId: '',
  categoryId: undefined,
  paymentModeId: undefined,
  requiresApproval: false,
  splits: [],
  aliasSplits: [],
  recurrence: {
    recurrenceMode: 1,
    weekdays: 0,
    dayOfMonth: null,
    interval: null,
    anchorDate: null,
    endDate: null,
  },
})

watch(currentTemplate, (template) => {
  if (!template) return
  const tpl = template as RecurringExpenseTemplate
  formData.value = {
    templateId,
    title: tpl.title ?? '',
    description: tpl.description ?? '',
    amount: String(tpl.amount ?? ''),
    // Payer is always a user guid (backend resolves the alias server-side in
    // alias-mode groups) — never map paidByAliasId here.
    paidByUserId: tpl.paidByUserId ?? '',
    categoryId: Number(tpl.categoryId) || undefined,
    paymentModeId: Number(tpl.paymentModeId) || undefined,
    requiresApproval: !!tpl.requiresApproval,
    splits: (tpl.splits ?? []).map(s => ({
      userId: s.userId ?? '',
      included: true,
      splitAmount: Number(s.splitAmount) || 0,
    })),
    aliasSplits: (tpl.aliasSplits ?? []).map(s => ({
      aliasId: s.aliasId ?? '',
      included: true,
      splitAmount: Number(s.splitAmount) || 0,
    })),
    recurrence: {
      recurrenceMode: Number(tpl.recurrenceMode) || 1,
      weekdays: Number(tpl.weekdays) || 0,
      dayOfMonth: tpl.dayOfMonth != null ? Number(tpl.dayOfMonth) : null,
      interval: tpl.interval != null ? Number(tpl.interval) : null,
      anchorDate: tpl.anchorDate ?? null,
      endDate: tpl.endDate ?? null,
    },
  }
}, { immediate: true })

// Splits saved for members who have since left the group. Stays [] until both
// the template and the member list are loaded (avoids flagging every split as
// orphaned during the fetch race).
const groupMembers = ref<GroupMember[]>([])
const orphanedSplits = computed<RecurringFormSplit[]>(() => {
  const tpl = currentTemplate.value as RecurringExpenseTemplate | null
  if (!tpl || groupMembers.value.length === 0) return []
  return (tpl.splits ?? [])
    .filter(s => s.userId && !groupMembers.value.some(m => m.userId === s.userId))
    .map(s => ({ userId: s.userId ?? '', included: true, splitAmount: Number(s.splitAmount) || 0 }))
})

const onSubmit = async (payload: RecurringFormPayload): Promise<void> => {
  isUpdating.value = true
  try {
    const request = {
      title: payload.expense.title,
      description: payload.expense.description,
      amount: payload.expense.amount,
      categoryId: payload.expense.categoryId,
      paymentModeId: payload.expense.paymentModeId,
      paidByUserId: payload.expense.paidByUserId,
      recurrenceMode: payload.recurrence.recurrenceMode,
      weekdays: payload.recurrence.weekdays,
      dayOfMonth: payload.recurrence.dayOfMonth,
      interval: payload.recurrence.interval,
      anchorDate: payload.recurrence.anchorDate ?? '',
      endDate: payload.recurrence.endDate,
      requiresApproval: payload.expense.requiresApproval,
      splits: payload.splits,
      aliasSplits: payload.aliasSplits,
    }

    const updated = await updateTemplate(templateId, request)

    if (updated) {
      await navigateTo(`/groups/${groupId}/recurring`)
    }
  }
  catch {
    // Error shown via toast
  }
  finally {
    isUpdating.value = false
  }
}

const { goBack } = useSmartBack(`/groups/${groupId}/recurring`)

onMounted(async () => {
  await loadTemplate()
})

useHead({
  title: computed(() => `${t('recurring.editTemplate')} - ${currentTemplate.value?.title || ''}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
