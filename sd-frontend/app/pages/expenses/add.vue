<template>
  <ExpensesExpenseForm
    v-model="expenseFormData"
    :title="$t('expenses.addNew')"
    :submit-label="$t('expenses.addExpense')"
    :pre-selected-group-id="preSelectedGroupId"
    :loading="isCreating"
    show-add-more
    @submit="onSubmit"
    @add-more="onAddMore"
    @cancel="goBack"
    @add-attachment="onAddAttachment"
    @remove-attachment="onRemoveAttachment"
  />
</template>

<script setup lang="ts">
import type { CreateExpenseRequest } from '~/types/domain'

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
  splits?: { userId: string, included: boolean, splitAmount: number | null }[]
  aliasSplits?: { aliasId: string, included: boolean, splitAmount: number | null }[]
}

const { t } = useI18n()
const { showError } = useNotifications()
const route = useRoute()
const { receiptImageUrl, clearReceiptImage } = useReceiptScan()

onUnmounted(() => clearReceiptImage())

// Check if groupId is passed as query parameter (from group page) or route parameter
const preSelectedGroupId = computed<string | null>(() => {
  const id = route.query.groupId || route.params.groupId
  return id ? String(id) : null
})

const getInitialFormData = (): ExpenseFormModel => ({
  groupId: preSelectedGroupId.value ?? undefined,
  title: typeof route.query.title === 'string' ? route.query.title : '',
  description: typeof route.query.description === 'string' ? route.query.description : '',
  amount: route.query.amount ? String(route.query.amount) : undefined,
  paidByUserId: undefined,
  expenseDate: typeof route.query.expenseDate === 'string' ? route.query.expenseDate : new Date().toISOString().split('T')[0],
  categoryId: route.query.categoryId ? Number(route.query.categoryId) : undefined,
  paymentModeId: route.query.paymentModeId ? Number(route.query.paymentModeId) : undefined,
  splits: [],
})

// Form data state
const expenseFormData = ref(getInitialFormData())
const stashedFiles = ref<File[]>([])

// Loading states
const isCreating = ref(false)

// useExpenses must be called at setup scope (it uses useI18n/useNotifications).
// groupId isn't known until submit, so bridge via a reactive ref.
const activeGroupId = ref('')
const { createExpense } = useExpenses(activeGroupId)

// useExpenseAttachments must also be called at setup scope (it uses useApi/
// useI18n/useNotifications). groupId/expenseId aren't known until after
// createExpense resolves, so bridge via reactive refs — uploadAttachment
// reads them at call time, not construction time.
const activeExpenseId = ref('')
const attachmentsComposable = useExpenseAttachments(activeGroupId, activeExpenseId)

// Upload all attachments (scanned receipt + manually stashed files) in
// parallel. Uses Promise.allSettled so a single failure doesn't reject the
// whole batch — each file is independent and non-fatal (the expense is
// already created).
const uploadAllAttachments = async (): Promise<void> => {
  const files: File[] = []

  // Collect scanned receipt (if any) as a File
  if (receiptImageUrl.value) {
    try {
      const blob = await fetch(receiptImageUrl.value).then(r => r.blob())
      files.push(new File([blob], 'receipt.jpg', { type: blob.type }))
    }
    catch {
      showError(t('toasts.attachments.autoUploadFailed'))
    }
  }

  // Add manually stashed files
  files.push(...stashedFiles.value)

  if (files.length === 0) return

  const results = await Promise.allSettled(
    files.map(f => attachmentsComposable.uploadAttachment(f)),
  )

  const failures = results.filter(r => r.status === 'rejected')
  if (failures.length > 0) {
    showError(t('toasts.attachments.autoUploadFailed'))
  }

  stashedFiles.value = []
}

// Form submission
const onSubmit = async (payload: { groupId: string, expenseData: Record<string, unknown> }) => {
  const { groupId, expenseData } = payload
  activeGroupId.value = groupId
  isCreating.value = true
  try {
    const createdExpense = await createExpense(expenseData as CreateExpenseRequest)

    if (createdExpense) {
      // Auto-attach the scanned receipt and any manually selected files to the
      // newly created expense. Non-fatal: the expense is already created, so
      // a failure only shows a toast.
      activeExpenseId.value = createdExpense.id
      await uploadAllAttachments()
      clearReceiptImage()
      await navigateTo(`/groups/${groupId}`)
    }
  }
  catch (error: unknown) {
    console.error('Failed to create expense:', error)
  }
  finally {
    isCreating.value = false
  }
}

const onAddMore = async (payload: { groupId: string, expenseData: Record<string, unknown> }) => {
  const { groupId, expenseData } = payload
  activeGroupId.value = groupId
  isCreating.value = true
  try {
    const createdExpense = await createExpense(expenseData as CreateExpenseRequest)

    if (createdExpense) {
      activeExpenseId.value = createdExpense.id
      await uploadAllAttachments()
      clearReceiptImage()
      const resetData = getInitialFormData()
      resetData.groupId = groupId
      expenseFormData.value = resetData
    }
  }
  catch (error: unknown) {
    console.error('Failed to create expense:', error)
  }
  finally {
    isCreating.value = false
  }
}

const onAddAttachment = (file: File): void => {
  stashedFiles.value.push(file)
}

const onRemoveAttachment = (file: File): void => {
  const index = stashedFiles.value.findIndex(f => f.name === file.name && f.size === file.size)
  if (index >= 0) {
    stashedFiles.value.splice(index, 1)
  }
}

const { goBack } = useSmartBack(`/groups/${route.query.groupId}`)

useHead({
  title: computed(() => t('expenses.addNew')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
