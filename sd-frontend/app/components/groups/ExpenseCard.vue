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
            :class="expense.paidByUserId === currentUser?.id ? 'text-green-600' : 'text-red-600'"
          >
            {{ expense.amount.toFixed(2) }}€
          </div>
        </div>
      </div>
      <div
        v-if="expense.description"
        class="flex justify-between items-start gap-4"
      >
        <p class="text-xs text-gray-400 truncate">
          {{ expense.description }}
        </p>
      </div>
    </div>

    <div class="flex flex-col gap-1">
      <div class="flex items-center gap-2">
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
      </div>
      <div>
        <UBadge
          v-if="userSplit"
          variant="soft"
          color="neutral"
          :label="`Your share: ${userSplit.splitAmount.toFixed(2)}€`"
        />
      </div>
      <div class="flex justify-between items-center">
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
        <div class="flex items-center gap-1">
          <UiConfirmDialog
            title="Delete Expense"
            message="Are you sure you want to delete this expense?"
            subtitle="This action cannot be undone and will remove all associated data."
            confirm-text="Delete Expense"
            confirm-color="error"
            icon="i-lucide-trash-2"
            icon-color-class="text-error-500"
            :is-processing="isDeletingExpense"
            @confirm="deleteExpense"
          >
            <template #button>
              <UButton
                variant="ghost"
                color="error"
                size="sm"
                icon="i-lucide-trash-2"
                @click.stop
              />
            </template>
          </UiConfirmDialog>
          <UButton
            variant="ghost"
            color="neutral"
            size="sm"
            icon="i-lucide-edit-2"
            @click="navigateToEdit"
          />
        </div>
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

const isDeletingExpense = ref(false)

// Computed properties for category and payment mode names
const categoryName = computed(() => {
  return getCategoryName(props.expense.categoryId)
})

const paymentModeName = computed(() => {
  return getPaymentModeName(props.expense.paymentModeId)
})

const formattedDate = computed(() => {
  return new Date(props.expense.expenseDate).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
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
</script>
