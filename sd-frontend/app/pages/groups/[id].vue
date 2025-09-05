<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard
      class="w-full max-w-2xl"
      variant="soft"
    >
      <template #header>
        <GroupsSectionHeader :group="group" />
      </template>
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
        <h3 class="text-lg font-semibold text-primary mb-2">
          Expenses
        </h3>
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
      <template #footer>
        <div
          v-if="expensePagination.totalPages > 1"
          class="flex justify-center"
        >
          <UPagination
            v-model:page="currentPage"
            :items-per-page="expensePagination.limit"
            :total="expensePagination.total"
            :sibling-count="1"
          />
        </div>
      </template>
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id
const { user } = useAuth()
const { currentGroup, fetchGroup } = useGroups()
const { expenses, fetchExpenses, pagination: expensePagination, isLoading: isLoadingExpenses } = useExpenses(groupId)
const { balanceSummary, fetchBalanceSummary } = useBalances(groupId)

const group = computed(() => currentGroup.value)
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

const currentPage = ref(1)

const getGroupTotal = () => {
  if (!summary.value || !summary.value.balances) return 0
  return summary.value.balances.reduce((total, balance) => total + balance.totalPaid, 0)
}

watch(currentPage, async (newPage) => {
  await fetchExpenses({ page: newPage })
}, { immediate: false })

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
    await fetchExpenses({ page: 1 })
    await fetchBalanceSummary()
  }
})

useHead({
  title: computed(() => group.value?.name || 'Group'),
})

definePageMeta({
  middleware: 'auth',
})
</script>
