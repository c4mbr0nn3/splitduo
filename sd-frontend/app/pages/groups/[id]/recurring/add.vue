<template>
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
    :title="$t('recurring.addNew')"
    :submit-label="$t('recurring.createTemplate')"
    :group-id="groupId"
    :loading="isCreating"
    @submit="onSubmit"
    @cancel="goBack"
  />
</template>

<script setup lang="ts">
import type { RecurringFormModel, RecurringFormPayload } from '~/components/recurring/RecurringExpenseForm.vue'

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)

const pageLoading = ref(true)
const loadError = ref(false)
const isCreating = ref(false)

const { createTemplate } = useRecurringExpenses(groupId)
const { fetchGroup } = useGroups()

const retryLoad = async (): Promise<void> => {
  loadError.value = false
  pageLoading.value = true
  try {
    await fetchGroup(groupId)
  }
  catch {
    loadError.value = true
  }
  finally {
    pageLoading.value = false
  }
}

const getInitialFormData = (): RecurringFormModel => ({
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
    recurrenceMode: 1, // Weekly (preset default)
    weekdays: 1, // Monday
    dayOfMonth: null,
    interval: null,
    anchorDate: new Date().toISOString().split('T')[0]!,
    endDate: null,
  },
})

const formData = ref<RecurringFormModel>(getInitialFormData())

const onSubmit = async (payload: RecurringFormPayload): Promise<void> => {
  isCreating.value = true
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

    const created = await createTemplate(request)

    if (created) {
      await navigateTo(`/groups/${groupId}/recurring`)
    }
  }
  catch {
    // Error shown via toast
  }
  finally {
    isCreating.value = false
  }
}

const { goBack } = useSmartBack(`/groups/${groupId}/recurring`)

onMounted(async () => {
  await retryLoad()
})

useHead({
  title: computed(() => t('recurring.addNew')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
