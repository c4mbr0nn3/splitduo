<template>
  <div>
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
      <GroupsStatsCategoryChart
        v-if="groupStats.categoryBreakdown?.length"
        :category-breakdown="groupStats.categoryBreakdown"
      />
      <GroupsStatsMonthlyChart
        v-if="groupStats.monthlyBreakdown?.length"
        :monthly-breakdown="groupStats.monthlyBreakdown"
      />
      <GroupsStatsMemberPaidChart
        v-if="groupStats.balances?.length"
        :balances="groupStats.balances"
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
  </div>
</template>

<script setup>
const props = defineProps({
  groupId: { type: String, required: true },
})

const { groupStats, fetchGroupStats, isLoading: isLoadingStats } = useBalances(props.groupId)

onMounted(async () => {
  await fetchGroupStats()
})
</script>
