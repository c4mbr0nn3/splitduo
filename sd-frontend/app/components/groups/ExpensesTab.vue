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
        <div class="flex items-center gap-2">
          <UButton
            class="md:hidden"
            icon="i-lucide-filter"
            :label="activeFilterCount ? `Filters (${activeFilterCount})` : 'Filters'"
            size="sm"
            variant="outline"
            @click="mobileFiltersOpen = !mobileFiltersOpen"
          />
          <UButton
            label="Add Expense"
            icon="i-lucide-plus"
            size="sm"
            @click="addExpense"
          />
        </div>
      </div>
      <div class="md:grid md:grid-cols-3 md:gap-4">
        <div :class="['md:block', 'md:col-span-1', mobileFiltersOpen ? 'block mb-4' : 'hidden']">
          <GroupsExpenseFilterCard
            v-model:filters="pendingFilters"
            :category-options="categoryOptions"
            :member-options="memberOptions"
            :active-filter-count="activeFilterCount"
            @apply="applyFilters"
            @clear="clearFilters"
          />
        </div>
        <div class="md:col-span-2">
          <div
            v-if="showSkeleton"
            class="space-y-3"
          >
            <GroupsExpenseCardSkeleton
              v-for="i in 4"
              :key="i"
            />
          </div>
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
      </div>
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
const { expenses, fetchExpenses, pagination: expensePagination } = useExpenses(props.groupId)
const { balanceSummary, fetchBalanceSummary } = useBalances(props.groupId)
const { categories } = useCategories()

const currentPage = ref(1)
const showSkeleton = ref(true)
const mobileFiltersOpen = ref(false)
const pendingFilters = ref({ startDate: null, endDate: null, category: null, userId: null })
const activeFilters = ref({ startDate: null, endDate: null, category: null, userId: null })
const activeFilterCount = computed(() => Object.values(activeFilters.value).filter(Boolean).length)

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

const categoryOptions = computed(() => [
  { value: null, label: 'All categories' },
  ...categories.value.map(c => ({ value: c.name, label: c.name })),
])

const memberOptions = computed(() => {
  if (!summary.value?.balances) return [{ value: null, label: 'All members' }]
  return [
    { value: null, label: 'All members' },
    ...summary.value.balances.map(b => ({
      value: b.userId,
      label: `${b.user.firstName} ${b.user.lastName}`,
    })),
  ]
})

const getGroupTotal = () => {
  if (!summary.value?.balances) return 0
  return summary.value.balances.reduce((total, balance) => total + balance.totalPaid, 0)
}

const addExpense = () => {
  navigateTo(`/expenses/add?groupId=${props.groupId}`)
}

const applyFilters = async () => {
  activeFilters.value = { ...pendingFilters.value }
  currentPage.value = 1
  await fetchExpenses({ ...activeFilters.value, page: 1 })
  mobileFiltersOpen.value = false
}

const clearFilters = async () => {
  pendingFilters.value = { startDate: null, endDate: null, category: null, userId: null }
  activeFilters.value = { startDate: null, endDate: null, category: null, userId: null }
  currentPage.value = 1
  await fetchExpenses({ page: 1 })
  mobileFiltersOpen.value = false
}

const onExpenseDeleted = async () => {
  await Promise.all([
    fetchExpenses({ ...activeFilters.value, page: currentPage.value }),
    fetchBalanceSummary(),
  ])
}

watch(currentPage, async (newPage) => {
  await fetchExpenses({ ...activeFilters.value, page: newPage })
}, { immediate: false })

onMounted(async () => {
  try {
    await withMinDuration(async () => {
      await Promise.all([fetchExpenses({ page: 1 }), fetchBalanceSummary()])
    })
  }
  finally {
    showSkeleton.value = false
  }
})
</script>
