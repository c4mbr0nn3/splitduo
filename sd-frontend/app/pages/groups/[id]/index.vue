<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard
      class="w-full max-w-2xl"
      variant="soft"
    >
      <template #header>
        <GroupsSectionHeader
          :group="group"
          :is-exporting="isExporting"
          @export="handleExport"
        />
      </template>

      <GroupsTabsNav :group-id="groupId" />

      <!-- Expenses tab -->
      <template v-if="activeTab === 'expenses'">
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
      </template>

      <!-- Stats tab -->
      <template v-else-if="activeTab === 'stats'">
        <UiLoadingSpinner
          v-if="isLoadingStats"
          text="Loading stats..."
        />
        <div
          v-else-if="groupStats"
          class="space-y-6"
        >
          <GroupsStatsCards
            :total-expenses="groupStats.totalExpenses"
            :group-total="groupStats.totalAmount"
          />
          <div>
            <h3 class="text-lg font-semibold text-primary mb-3">
              Member Balances
            </h3>
            <div class="space-y-3">
              <UCard
                v-for="balance in groupStats.balances"
                :key="balance.userId"
                variant="outline"
              >
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <div
                      class="w-10 h-10 rounded-full flex items-center justify-center text-sm font-bold"
                      :class="balance.balance >= 0 ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'"
                    >
                      {{ balance.user.firstName[0] }}{{ balance.user.lastName[0] }}
                    </div>
                    <span class="font-medium">{{ balance.user.firstName }} {{ balance.user.lastName }}</span>
                  </div>
                  <span
                    class="font-bold text-lg"
                    :class="balance.balance >= 0 ? 'text-green-600' : 'text-red-600'"
                  >
                    {{ balance.balance >= 0 ? '+' : '' }}{{ balance.balance.toFixed(2) }} €
                  </span>
                </div>
                <USeparator class="my-3" />
                <div class="grid grid-cols-2 gap-4 text-center">
                  <div>
                    <p class="text-xs text-dimmed mb-1">
                      Paid
                    </p>
                    <p class="font-semibold text-blue-600">
                      {{ balance.totalPaid.toFixed(2) }} €
                    </p>
                  </div>
                  <div>
                    <p class="text-xs text-dimmed mb-1">
                      Owes
                    </p>
                    <p class="font-semibold text-orange-600">
                      {{ balance.totalOwed.toFixed(2) }} €
                    </p>
                  </div>
                </div>
              </UCard>
            </div>
          </div>
        </div>
        <UiEmptyState
          v-else
          icon="i-lucide-bar-chart-3"
          title="No stats available"
          subtitle="Add some expenses to see group stats"
        />
      </template>

      <template #footer>
        <div
          v-if="activeTab === 'expenses' && expensePagination.totalPages > 1"
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
const { balanceSummary, fetchBalanceSummary, groupStats, fetchGroupStats, isLoading: isLoadingStats } = useBalances(groupId)
const { exportToCsv, isExporting } = useImportExport(groupId)

const activeTab = computed(() => route.query.tab === 'stats' ? 'stats' : 'expenses')

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

const addExpense = () => {
  navigateTo(`/expenses/add?groupId=${groupId}`)
}

const onExpenseDeleted = async () => {
  await Promise.all([
    fetchExpenses({ page: currentPage.value }),
    fetchBalanceSummary(),
  ])
}

const handleExport = async () => {
  try {
    await exportToCsv()
  }
  catch (error) {
    console.error('Failed to export group data:', error)
  }
}

watch(currentPage, async (newPage) => {
  await fetchExpenses({ page: newPage })
}, { immediate: false })

watch(activeTab, async (tab) => {
  if (tab === 'stats' && !groupStats.value) {
    await fetchGroupStats()
  }
})

onMounted(async () => {
  if (groupId) {
    const fetches = [fetchGroup(groupId), fetchExpenses({ page: 1 }), fetchBalanceSummary()]
    if (activeTab.value === 'stats') fetches.push(fetchGroupStats())
    await Promise.all(fetches)
  }
})

useHead({
  title: computed(() => group.value?.name || 'Group'),
})

definePageMeta({
  middleware: 'auth',
})
</script>
