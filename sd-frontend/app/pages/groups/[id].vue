<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard
      class="w-full max-w-2xl"
      variant="soft"
    >
      <template #header>
        <div class="text-2xl font-bold text-primary text-center mb-2">
          {{ group?.name || 'Group' }}
        </div>
        <div
          v-if="group?.description"
          class="text-sm text-gray-500 text-center mb-4"
        >
          {{ group.description }}
        </div>
      </template>
      <div class="mb-6">
        <h3 class="text-lg font-semibold text-primary mb-2">
          Summary
        </h3>
        <div v-if="summary">
          <div class="flex flex-wrap gap-4 justify-center">
            <div class="text-blue-600">
              Members: {{ group.memberCount }}
            </div>
            <div class="text-yellow-600">
              Balance: {{ mySummary.balance }}€
            </div>
          </div>
        </div>
        <div
          v-else
          class="text-gray-400"
        >
          No summary available.
        </div>
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
          <div class="space-y-3 mb-4">
            <UCard
              v-for="expense in expenses"
              :key="expense.id"

              class="hover:shadow-md transition-shadow"
            >
              <!-- Row 1: Title, Description, Amount & Date -->
              <div class="flex justify-between text-xs text-gray-500 items-start gap-4 mb-3">
                <span>
                  Paid by {{ expense.paidByUserId === user?.id ? 'you' : `${expense.paidByUser.firstName} ${expense.paidByUser.lastName}` }}
                </span>
                <div>
                  {{ formatDate(expense.expenseDate) }}
                </div>
              </div>
              <div class="flex justify-between items-start gap-4 mb-1">
                <div class="flex flex-col flex-1 min-w-0">
                  <h4 class="font-medium truncate">
                    {{ expense.title }}
                  </h4>
                </div>
                <div class="flex flex-col items-end flex-shrink-0">
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
              <div class="flex flex-wrap items-center gap-4 text-xs text-gray-500 mb-2">
                <div class="flex items-center gap-1">
                  <UIcon
                    name="i-lucide-credit-card"
                    class="w-3 h-3"
                  />
                  <span class="capitalize">{{ expense.paymentMode }}</span>
                </div>
              </div>

              <!-- Row 3: How many people -->
              <div class="flex items-center gap-1 text-xs text-gray-500 mb-3">
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
                >
                  Your share: {{ getUserSplit(expense).splitAmount.toFixed(2) }}€
                </UBadge>
              </div>
            </UCard>
          </div>

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
        </div>
        <div v-else>
          No expenses found.
        </div>
      </div>
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
  const my = summary.value.balances.find(el => el.userId === user.value.id)
  return {
    balance: my?.balance || 0 }
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
