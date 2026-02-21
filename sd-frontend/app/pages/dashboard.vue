<template>
  <div class="py-8">
    <!-- Stats Cards -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
      <template v-if="showSkeleton">
        <DashboardStatCardSkeleton
          v-for="i in 4"
          :key="i"
        />
      </template>
      <template v-else>
        <DashboardStatCard
          :stats="{ label: 'Total Groups', value: userStats?.totalGroups }"
          icon="i-lucide-users"
          color="teal"
        />
        <DashboardStatCard
          :stats="{ label: 'You Owe', value: userStats?.youOwe, color: 'red' }"
          type="currency"
          icon="i-lucide-frown"
          color="red"
        />
        <DashboardStatCard
          :stats="{ label: `You're Owed`, value: userStats?.youreOwed, color: 'green' }"
          type="currency"
          icon="i-lucide-smile"
          color="green"
        />
        <DashboardStatCard
          :stats="{ label: 'Net Balance', value: netBalance, color: netBalanceColor }"
          type="currency"
          :icon="netBalance >= 0 ? 'i-lucide-trending-up' : 'i-lucide-trending-down'"
          :color="netBalanceColor"
        />
      </template>
    </div>
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <h2 class="text-lg font-semibold">
              Recent Groups
            </h2>
            <UButton
              size="sm"
              variant="subtle"
              label="View All"
              @click="viewAllGroups"
            />
          </div>
        </template>

        <div
          v-if="showSkeleton"
          class="space-y-4"
        >
          <DashboardGroupCardSkeleton
            v-for="i in 3"
            :key="i"
          />
        </div>

        <UiEmptyState
          v-else-if="groups.length === 0"
          icon="i-lucide-users"
          title="No groups yet"
          subtitle="Get started by creating your first group"
        >
          <template #action>
            <UButton
              label="Create Your First Group"
              @click="createFirstGroup"
            />
          </template>
        </UiEmptyState>

        <div
          v-else
          class="space-y-4"
        >
          <template
            v-for="group in groups.slice(0, 3)"
            :key="group.id"
          >
            <DashboardGroupCard
              :group="group"
              @click="navigateToGroup(group.id)"
            />
          </template>
        </div>
      </UCard>
      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">
            Quick Actions
          </h2>
        </template>

        <div class="space-y-4">
          <UButton
            v-for="action in quickActions"
            :key="action.id"
            block
            size="lg"
            :variant="action.variant"
            class="justify-start"
            :label="action.label"
            :icon="action.icon"
            @click="action.handler"
          />
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup>
const { groups, fetchGroups } = useGroups()
const { userStats, fetchUserStats } = useUsers()

const showSkeleton = ref(true)

const netBalance = computed(() => (userStats.value?.youreOwed || 0) - (userStats.value?.youOwe || 0))
const netBalanceColor = computed(() => netBalance.value > 0 ? 'green' : netBalance.value < 0 ? 'red' : 'teal')

onMounted(async () => {
  try {
    await withMinDuration(async () => {
      await Promise.all([fetchGroups(), fetchUserStats()])
    })
  }
  catch (error) {
    console.error('Failed to fetch dashboard data:', error)
  }
  finally {
    showSkeleton.value = false
  }
})

const navigateToGroup = (groupId) => {
  navigateTo(`/groups/${groupId}`)
}

const createFirstGroup = () => {
  navigateTo('/groups/add')
}

const viewAllGroups = () => {
  navigateTo('/groups')
}

const quickActions = [
  {
    id: 'create-group',
    label: 'Create New Group',
    icon: 'i-lucide-plus',
    variant: 'solid',
    handler: () => navigateTo('/groups/add'),
  },
  {
    id: 'add-expense',
    label: 'Add Expense',
    icon: 'i-lucide-receipt',
    variant: 'outline',
    handler: () => navigateTo('/expenses/add'),
  },
]

useHead({
  title: 'Dashboard',
})

definePageMeta({
  middleware: 'auth',
})
</script>
