<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard class="w-full max-w-2xl">
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
              Balance: {{ mySummary.balance }}
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
        <div v-if="expenses.length">
          <ul class="divide-y divide-gray-200">
            <li
              v-for="expense in expenses"
              :key="expense.id"
              class="py-3 flex justify-between items-center"
            >
              <span>{{ expense.title }}</span>
              <span class="font-bold text-red-600">-{{ expense.amount }}€</span>
            </li>
          </ul>
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
const { expenses, fetchExpenses } = useExpenses(groupId)
const { balanceSummary, fetchBalanceSummary } = useBalances(groupId)
const group = computed(() => currentGroup.value)
const summary = computed(() => balanceSummary.value)
const mySummary = computed(() => {
  if (!summary.value) return null
  const my = summary.value.balances.find(el => el.userId === user.value.id)
  return {
    balance: my?.balance || 0 }
})

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
    await fetchExpenses()
    await fetchBalanceSummary()
  }
})
</script>
