<template>
  <div class="flex flex-col items-center justify-center py-6 sm:py-8">
    <UiLoadingSpinner
      v-if="pageLoading"
      :text="$t('expenses.loading')"
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-receipt"
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

    <div
      v-else
      class="w-full max-w-2xl space-y-4"
    >
      <ExpensesExpenseForm
        v-model="expenseFormData"
        :title="$t('expenses.editExpense')"
        :submit-label="$t('expenses.updateExpense')"
        :pre-selected-group-id="groupId"
        :expense-id="expenseId"
        :loading="isUpdating"
        @submit="onSubmit"
        @cancel="goBack"
        @add-attachment="onAddAttachment"
      />
      <UCard
        v-if="expenseId"
        variant="soft"
        :ui="{ body: 'p-4' }"
      >
        <template #header>
          <p class="text-sm font-semibold text-highlighted">
            {{ $t('expenses.attachments.title') }}
          </p>
        </template>
        <ExpensesExpenseAttachmentsList
          v-if="attachments"
          :group-id="groupId"
          :expense-id="expenseId"
          :instance="attachments"
        />
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Expense, SplitMode } from '~/types/domain'

interface ExpenseFormModel {
  expenseId?: string
  groupId?: string
  title?: string
  description?: string
  amount?: string
  paidByUserId?: string
  expenseDate?: string
  categoryId?: number
  paymentModeId?: number
  splits?: { userId: string, included: boolean, splitAmount: number | null, splitPercentage?: number | null }[]
  aliasSplits?: { aliasId: string, included: boolean, splitAmount: number | null, splitPercentage?: number | null }[]
  splitMode?: SplitMode
}

interface SplitItem {
  userId: string
  included: boolean
  splitAmount: number | null
  splitPercentage?: number | null
}

interface AliasSplitItem {
  aliasId: string
  included: boolean
  splitAmount: number | null
  splitPercentage?: number | null
}

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)
const expenseId = route.params.expenseId ? String(route.params.expenseId) : undefined

const { currentExpense, fetchExpense, updateExpense } = useExpenses(groupId)

// Create the attachments composable at setup scope so Nuxt context (useApi,
// useI18n, useNotifications) is available — calling it inside an async event
// handler loses the context and the upload fails silently. The single shared
// instance is passed down to ExpensesExpenseAttachmentsList so uploads and the
// displayed list share the same reactive source.
const attachments = expenseId ? useExpenseAttachments(groupId, expenseId) : null

const isUpdating = ref(false)

const onAddAttachment = async (file: File): Promise<void> => {
  if (!attachments) return
  try {
    await attachments.uploadAttachment(file)
  }
  catch {
    // Error shown via toast
  }
}

// Form data state
const expenseFormData = ref<ExpenseFormModel>({
  groupId: undefined,
  title: '',
  description: '',
  amount: undefined,
  paidByUserId: undefined,
  expenseDate: new Date().toISOString().split('T')[0],
  categoryId: undefined,
  paymentModeId: undefined,
  splits: [],
})

// Watch currentExpense and update form data
watch(currentExpense, (expense) => {
  if (expense) {
    const e = expense as unknown as Expense
    const isAliasMode = Array.isArray(e.aliasSplits) && e.aliasSplits.length > 0
    const mappedSplits = isAliasMode ? [] : (mapSplits(e.splits) || [])
    const mappedAliasSplits = isAliasMode ? (mapAliasSplits(e.aliasSplits) || []) : []

    expenseFormData.value = {
      expenseId: e.id,
      groupId: e.groupId,
      title: e.title,
      description: e.description ?? '',
      amount: String(e.amount ?? ''),
      paidByUserId: e.paidByUserId,
      expenseDate: e.expenseDate ? new Date(e.expenseDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0],
      categoryId: Number(e.categoryId) || undefined,
      paymentModeId: Number(e.paymentModeId) || undefined,
      splits: mappedSplits,
      aliasSplits: mappedAliasSplits,
      splitMode: 'amounts',
    }
  }
}, { immediate: true })

const mapSplits = (splits: { userId?: string, splitAmount?: number | string }[] | undefined): SplitItem[] => {
  if (!Array.isArray(splits)) return []
  return splits.map((s) => {
    return {
      userId: s.userId ?? '',
      included: true,
      splitAmount: s.splitAmount != null ? Number(s.splitAmount) : null,
      splitPercentage: null,
    }
  })
}

const mapAliasSplits = (aliasSplits: { aliasId?: string, splitAmount?: number | string }[] | null | undefined): AliasSplitItem[] => {
  if (!Array.isArray(aliasSplits)) return []
  return aliasSplits.map((s) => {
    return {
      aliasId: s.aliasId ?? '',
      included: true,
      splitAmount: s.splitAmount != null ? Number(s.splitAmount) : null,
      splitPercentage: null,
    }
  })
}

const onSubmit = async (payload: { groupId: string, expenseData: Record<string, unknown> }) => {
  const { expenseData } = payload
  isUpdating.value = true
  try {
    const updatedExpense = await updateExpense(expenseId!, expenseData)
    if (updatedExpense) {
      await navigateTo(`/groups/${groupId}`)
    }
  }
  catch (error: unknown) {
    console.error('Failed to update expense:', error)
  }
  finally {
    isUpdating.value = false
  }
}

const { goBack } = useSmartBack(`/groups/${groupId}`)

// Page-level gate: wait for the expense fetch before rendering the form
const pageLoading = ref(true)
const loadError = ref(false)

const loadExpense = async () => {
  pageLoading.value = true
  loadError.value = false
  try {
    if (groupId && expenseId) {
      await fetchExpense(expenseId)
    }
  }
  catch {
    loadError.value = true
  }
  finally {
    pageLoading.value = false
  }
}

const retryLoad = async () => {
  await loadExpense()
}

onMounted(async () => {
  await loadExpense()
})

useHead({
  title: computed(() => `${t('expenses.editExpense')} - ${currentExpense.value?.title || ''}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
