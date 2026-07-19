<template>
  <UCard class="hover:shadow-md transition-shadow">
    <!-- Row 1: Title, Description, Amount & Date -->
    <div class="mb-3">
      <div class="flex justify-between text-xs text-dimmed items-start gap-4 mb-3">
        <span>
          Paid by {{ expense.paidByUserId === currentUser?.id ? 'you' : `${expense.paidByUser.firstName} ${expense.paidByUser.lastName}` }}
        </span>
        <div>
          {{ formattedDate }}
        </div>
      </div>
      <div class="flex justify-between items-start gap-4 mb-1">
        <div class="flex min-w-0">
          <h4 class="font-medium truncate">
            {{ expense.title }}
          </h4>
        </div>
        <div class="flex flex-col items-end flex-shrink-0">
          <div
            class="font-bold whitespace-nowrap"
            :class="expense.paidByUserId === currentUser?.id ? 'text-success' : 'text-error'"
          >
            {{ formatCurrency(expense.amount) }}
          </div>
        </div>
      </div>
      <div
        v-if="expense.description"
        class="flex justify-between items-start gap-4"
      >
        <p class="text-xs text-muted truncate">
          {{ expense.description }}
        </p>
      </div>
    </div>

    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
      <div class="flex flex-wrap items-center gap-2">
        <UBadge
          variant="soft"
          :color="categoryColor"
          :label="categoryName"
          class="capitalize"
        >
          <template #leading>
            <UIcon
              :name="categoryIcon"
              mode="svg"
            />
          </template>
        </UBadge>
        <div class="flex items-center gap-1 text-xs text-dimmed">
          <UIcon
            name="i-lucide-credit-card"
            class="w-3 h-3"
          />
          <span class="capitalize">{{ paymentModeName }}</span>
        </div>
        <div class="flex items-center gap-1 text-xs text-dimmed">
          <UIcon
            name="i-lucide-users"
            class="w-3 h-3"
          />
          <span>{{ expense.splits.length }} people</span>
        </div>
        <UBadge
          v-if="userSplit"
          variant="soft"
          color="neutral"
          :label="`Your share: ${formatAmount(userSplit.splitAmount)}€`"
        />
      </div>
      <div class="flex items-center justify-end">
        <UiButtonDropdown
          icon-only
          dropdown-icon="i-lucide-ellipsis-vertical"
          size="sm"
          variant="ghost"
          color="neutral"
          :items="dropdownItems"
          :disabled="isDeletingExpense"
        />
      </div>
    </div>
  </UCard>
</template>

<script setup>
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

const categoryIcon = computed(() => {
  const icons = {
    groceries: 'i-lucide-shopping-cart',
    transportation: 'i-lucide-car',
    utilities: 'i-lucide-zap',
    entertainment: 'i-lucide-gamepad-2',
    health: 'i-lucide-heart-pulse',
    education: 'i-lucide-graduation-cap',
    travel: 'i-lucide-plane',
    shopping: 'i-lucide-shopping-bag',
    housing: 'i-lucide-home',
    dining: 'i-lucide-utensils',
    other: 'i-lucide-more-horizontal',
  }
  return icons[categoryName.value.toLowerCase()] || icons.other
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

const userSplit = computed(() => {
  if (!props.currentUser) return null
  return props.expense.splits.find(split => split.userId === props.currentUser.id)
})

const emit = defineEmits(['expense-deleted'])

const confirmDeleteExpense = async () => {
  const confirmed = await modal.error({
    title: 'Delete Expense',
    subtitle: 'This action cannot be undone.',
    content: 'The expense will be permanently deleted. Are you sure you want to delete this expense?',
    confirmText: 'Delete Expense',
    cancelText: 'Cancel',
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
    label: 'Edit',
    icon: 'i-lucide-edit-2',
    color: 'info',
    onSelect: navigateToEdit,
  },
  {
    type: 'separator',
  },
  {
    label: 'Delete',
    icon: 'i-lucide-trash-2',
    color: 'error',
    onSelect: confirmDeleteExpense,
  },
])
</script>
