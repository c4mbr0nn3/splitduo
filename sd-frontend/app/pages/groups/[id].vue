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
          <!-- Group Statistics -->
          <GroupsStatsCards
            :total-expenses="expensePagination.total"
            :group-total="getGroupTotal()"
          />
        </div>
        <UCard
          v-else
          variant="outline"
        >
          <div class="text-center py-8">
            <UIcon
              name="i-lucide-bar-chart-3"
              class="w-12 h-12 text-gray-300 mx-auto mb-4"
            />
            <p class="text-dimmed">
              No summary available
            </p>
            <p class="text-sm text-gray-400 mt-1">
              Add some expenses to see your balance
            </p>
          </div>
        </UCard>
      </div>
      <div>
        <h3 class="text-lg font-semibold text-primary mb-2">
          Expenses
        </h3>
        <div
          v-if="isLoadingExpenses"
          class="flex justify-center py-8"
        >
          <UIcon
            name="i-lucide-loader-2"
            class="w-6 h-6 animate-spin text-gray-400"
          />
        </div>
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
        <div v-else>
          No expenses found.
        </div>
      </div>
      <template #footer>
        <!-- Pagination Component -->
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
  // Sum all totalPaid amounts to get the group's total spending
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
</script>
