<template>
  <div>
    <div class="mb-6">
      <div
        v-if="summary && mySummary"
        class="space-y-4"
      >
        <GroupsUserBalanceCard :balance="mySummary" />
        <GroupsStatsCards
          :total-expenses="expensePagination.total"
          :group-total="getGroupTotal()"
        />
      </div>
      <UCard
        v-else
        variant="outline"
      >
        <UiEmptyState
          icon="i-lucide-bar-chart-3"
          title="No summary available"
          subtitle="Add some expenses to see your balance"
        />
      </UCard>
    </div>
    <div>
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-lg font-semibold text-primary">
          Expenses
        </h3>
        <UButton
          label="Add Expense"
          icon="i-lucide-plus"
          size="sm"
          @click="addExpense"
        />
      </div>
      <UiLoadingSpinner
        v-if="isLoadingExpenses"
        text="Loading expenses..."
      />
      <div v-else-if="expenses.length">
        <div class="space-y-3">
          <GroupsExpenseCard
            v-for="expense in expenses"
            :key="expense.id"
            :expense="expense"
            :current-user="user"
            @expense-deleted="onExpenseDeleted"
          />
        </div>
      </div>
      <UiEmptyState
        v-else
        icon="i-lucide-receipt"
        title="No expenses found"
        subtitle="Start adding expenses to track your group spending"
      />
    </div>
    <template v-if="expensePagination.totalPages > 1">
      <USeparator class="mt-4" />
      <div class="flex justify-center mt-4">
        <UPagination
          v-model:page="currentPage"
          :items-per-page="expensePagination.limit"
          :total="expensePagination.total"
          :sibling-count="1"
        />
      </div>
    </template>
  </div>
</template>

<script setup>
const props = defineProps({
  groupId: { type: String, required: true },
})

const { user } = useAuth()
const { expenses, fetchExpenses, pagination: expensePagination, isLoading: isLoadingExpenses } = useExpenses(props.groupId)
const { balanceSummary, fetchBalanceSummary } = useBalances(props.groupId)

const currentPage = ref(1)

const summary = computed(() => balanceSummary.value)
const mySummary = computed(() => {
  if (!summary.value) return null
  const my = summary.value.balances.find(el => el.userId === user.value?.id)
  return my
    ? {
        balance: my.balance || 0,
        totalPaid: my.totalPaid || 0,
        totalOwed: my.totalOwed || 0,
      }
    : null
})

const getGroupTotal = () => {
  if (!summary.value?.balances) return 0
  return summary.value.balances.reduce((total, balance) => total + balance.totalPaid, 0)
}

const addExpense = () => {
  navigateTo(`/expenses/add?groupId=${props.groupId}`)
}

const onExpenseDeleted = async () => {
  await Promise.all([
    fetchExpenses({ page: currentPage.value }),
    fetchBalanceSummary(),
  ])
}

watch(currentPage, async (newPage) => {
  await fetchExpenses({ page: newPage })
}, { immediate: false })

onMounted(async () => {
  await Promise.all([fetchExpenses({ page: 1 }), fetchBalanceSummary()])
})
</script>
