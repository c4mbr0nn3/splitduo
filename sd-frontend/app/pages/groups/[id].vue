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
          <div class="grid grid-cols-1 gap-4">
            <UCard variant="outline">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 bg-purple-100 rounded-full flex items-center justify-center">
                  <UIcon
                    name="i-lucide-clipboard-list"
                    class="w-5 h-5 text-purple-600"
                  />
                </div>
                <div>
                  <p class="text-sm text-muted">
                    Total Expenses
                  </p>
                  <p class="font-bold text-lg text-purple-600">
                    {{ expensePagination.total || 0 }}
                  </p>
                </div>
              </div>
            </UCard>

            <UCard variant="outline">
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 bg-green-100 rounded-full flex items-center justify-center">
                  <UIcon
                    name="i-lucide-calculator"
                    class="w-5 h-5 text-green-600"
                  />
                </div>
                <div>
                  <p class="text-sm text-muted">
                    Group Total
                  </p>
                  <p class="font-bold text-lg text-green-600">
                    {{ getGroupTotal().toFixed(2) }}€
                  </p>
                </div>
              </div>
            </UCard>
          </div>
        </div>

        <!-- Fallback when no summary -->
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
            <UCard
              v-for="expense in expenses"
              :key="expense.id"
              class="hover:shadow-md transition-shadow"
            >
              <!-- Row 1: Title, Description, Amount & Date -->
              <div class="flex justify-between text-xs text-dimmed items-start gap-4 mb-3">
                <span>
                  Paid by {{ expense.paidByUserId === user?.id ? 'you' : `${expense.paidByUser.firstName} ${expense.paidByUser.lastName}` }}
                </span>
                <div>
                  {{ formatDate(expense.expenseDate) }}
                </div>
              </div>
              <div class="flex justify-between items-start gap-4 mb-1">
                <div class="flex min-w-0">
                  <h4 class="font-medium truncate">
                    {{ expense.title }}
                  </h4>
                </div>
                <div class="flex items-end flex-shrink-0">
                  <div
                    class="font-bold whitespace-nowrap"
                    :class="expense.paidByUserId === user?.id ? 'text-green-600' : 'text-red-600'"
                  >
                    {{ expense.amount.toFixed(2) }}€
                  </div>
                </div>
              </div>
              <div class="flex justify-between items-start gap-4 mb-3">
                <p
                  v-if="expense.description"
                  class="text-xs text-gray-400 truncate"
                >
                  {{ expense.description }}
                </p>
              </div>

              <!-- Row 2: Who paid & Method -->
              <div class="flex flex-wrap items-center gap-4 text-xs text-dimmed mb-2">
                <div class="flex items-center gap-1">
                  <UIcon
                    name="i-lucide-credit-card"
                    class="w-3 h-3"
                  />
                  <span class="capitalize">{{ expense.paymentMode }}</span>
                </div>
              </div>

              <!-- Row 3: How many people -->
              <div class="flex items-center gap-1 text-xs text-dimmed mb-3">
                <UIcon
                  name="i-lucide-users"
                  class="w-3 h-3"
                />
                <span>{{ expense.splits.length }} people</span>
              </div>

              <!-- Row 4: Category & Split -->
              <div class="flex justify-between items-center">
                <UBadge
                  variant="soft"
                  :color="getCategoryColor(expense.category)"
                  :icon="getCategoryIcon(expense.category)"
                  class="capitalize"
                >
                  {{ expense.category }}
                </UBadge>
                <UBadge
                  v-if="getUserSplit(expense)"
                  variant="soft"
                  color="neutral"
                >
                  Your share: {{ getUserSplit(expense).splitAmount.toFixed(2) }}€
                </UBadge>
              </div>
            </UCard>
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

// Helper functions
const formatDate = (dateString) => {
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

const getCategoryIcon = (category) => {
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
  return icons[category.toLowerCase()] || icons.other
}

const getCategoryColor = (category) => {
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
  return colors[category.toLowerCase()] || colors.other
}

const getUserSplit = (expense) => {
  if (!user.value) return null
  return expense.splits.find(split => split.userId === user.value.id)
}

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
