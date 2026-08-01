<template>
  <UCard
    class="sd-surface sd-surface-hover"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <NuxtLink
      :to="`/groups/${expense.groupId}/expenses/${expense.id}/edit`"
      class="block"
    >
      <!-- Row 1: date + payer · amount -->
      <div class="flex items-start justify-between gap-3">
        <div class="min-w-0">
          <p class="text-xs text-dimmed">{{ formattedDate }}</p>
          <p class="text-sm text-muted mt-0.5">{{ payerName }}</p>
        </div>
        <div class="flex items-center gap-1.5 shrink-0">
          <span
            class="size-2 rounded-full"
            :class="youOwe ? 'bg-error' : 'bg-success'"
            aria-hidden="true"
          />
          <span class="text-lg font-semibold sd-tabular text-highlighted">
            {{ formatCurrency(expense.amount) }}
          </span>
        </div>
      </div>

      <!-- Row 2: title -->
      <h3 class="text-base font-medium text-highlighted mt-2 truncate">
        {{ expense.title }}
      </h3>

      <!-- Row 3: metadata -->
      <div class="flex flex-wrap items-center gap-2 mt-3">
        <UBadge
          :color="categoryColor"
          variant="subtle"
          size="sm"
        >{{ categoryName }}</UBadge>
        <UBadge
          v-if="paymentModeName"
          color="neutral"
          variant="outline"
          size="sm"
        >{{ paymentModeName }}</UBadge>
        <span
          class="text-xs text-dimmed truncate max-w-[12rem] sm:max-w-[16rem]"
          :title="splitLabel"
        >
          {{ splitLabel }}
        </span>
      </div>
    </NuxtLink>
    <div class="flex items-center justify-end -mt-2">
      <span @click.stop>
        <UiButtonDropdown
          icon-only
          dropdown-icon="i-lucide-ellipsis-vertical"
          size="md"
          square
          variant="ghost"
          color="neutral"
          :items="dropdownItems"
          :disabled="isDeletingExpense"
        />
      </span>
    </div>
  </UCard>
</template>

<script setup>
const { t } = useI18n()

const props = defineProps({
  expense: {
    type: Object,
    required: true,
  },
  currentUser: {
    type: Object,
    default: null,
  },
})

const { getCategoryName } = useCategories()
const { getPaymentModeName } = usePaymentModes()
const modal = useModal()

const isDeletingExpense = ref(false)

// Computed properties for category and payment mode names
const categoryName = computed(() => {
  return getCategoryName(props.expense.categoryId)
})

const paymentModeName = computed(() => {
  return getPaymentModeName(props.expense.paymentModeId)
})

const formattedDate = computed(() => {
  return formatDateString(props.expense.expenseDate)
})

const categoryColor = computed(() => {
  const colors = {
    groceries: 'success',
    transportation: 'primary',
    utilities: 'warning',
    entertainment: 'secondary',
    health: 'error',
    education: 'info',
    travel: 'secondary',
    shopping: 'error',
    housing: 'warning',
    dining: 'warning',
    other: 'neutral',
  }
  return colors[categoryName.value.toLowerCase()] || colors.other
})

const youOwe = computed(() => {
  return props.currentUser && props.expense.paidByUserId !== props.currentUser.id
})

const payerName = computed(() => {
  if (props.expense.paidByUserId === props.currentUser?.id) return t('expenses.you')
  return `${props.expense.paidByUser.firstName} ${props.expense.paidByUser.lastName}`
})

const isAliasSplit = computed(() => {
  return Array.isArray(props.expense.aliasSplits) && props.expense.aliasSplits.length > 0
})

const splitCount = computed(() => {
  if (isAliasSplit.value) return props.expense.aliasSplits.length
  return props.expense.splits?.length || 0
})

const splitLabel = computed(() => {
  if (!isAliasSplit.value) return t('expenses.peopleCount', { count: splitCount.value })
  const names = props.expense.aliasSplits.map(s => s.aliasName).join(', ')
  return t('expenses.splitAmong', { names })
})

const emit = defineEmits(['expense-deleted'])

const confirmDeleteExpense = async () => {
  const confirmed = await modal.error({
    title: t('expenses.deleteTitle'),
    subtitle: t('expenses.deleteConfirm'),
    content: t('expenses.deleteContent'),
    confirmText: t('expenses.deleteButton'),
    cancelText: t('common.cancel'),
  })

  if (confirmed) {
    await deleteExpense()
  }
}

const deleteExpense = async () => {
  isDeletingExpense.value = true
  try {
    const { deleteExpense: deleteExpenseApi } = useExpenses(props.expense.groupId)
    await deleteExpenseApi(props.expense.id)
    emit('expense-deleted', props.expense.id)
  }
  catch (error) {
    console.error('Failed to delete expense:', error)
  }
  finally {
    isDeletingExpense.value = false
  }
}

const navigateToEdit = () => {
  if (!props.expense.id) return
  navigateTo(`/groups/${props.expense.groupId}/expenses/${props.expense.id}/edit`)
}

const dropdownItems = computed(() => [
  {
    label: t('expenses.edit'),
    icon: 'i-lucide-edit-2',
    color: 'info',
    onSelect: navigateToEdit,
  },
  {
    type: 'separator',
  },
  {
    label: t('expenses.delete'),
    icon: 'i-lucide-trash-2',
    color: 'error',
    onSelect: confirmDeleteExpense,
  },
])
</script>
