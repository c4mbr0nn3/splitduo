<template>
  <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Stats Cards -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
      <DashboardStatCard
        :stats="{ label: 'Total Groups', value: userStats?.totalGroups }"
        icon="i-lucide-users"
        color="blue"
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
            />
          </div>
        </template>
        <div
          v-if="isLoadingGroups"
          class="flex justify-center py-8"
        >
          <UIcon
            name="i-lucide-loader-2"
            class="w-6 h-6 animate-spin text-gray-400"
          />
        </div>

        <div
          v-else-if="groups.length === 0"
          class="text-center py-8"
        >
          <UIcon
            name="i-lucide-users"
            class="mx-auto mb-4"
          />
          <p class=" mb-4">
            No groups yet
          </p>
          <UButton @click="createFirstGroup">
            Create Your First Group
          </UButton>
        </div>

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
            block
            size="lg"
            class="justify-start"
            @click="createNewGroup"
          >
            <UIcon
              name="i-lucide-plus"
              class="w-5 h-5 mr-2"
            />
            Create New Group
          </UButton>

          <UButton
            block
            size="lg"
            variant="outline"
            class="justify-start"
            @click="addExpense"
          >
            <UIcon
              name="i-lucide-receipt"
              class="w-5 h-5 mr-2"
            />
            Add Expense
          </UButton>

          <UButton
            block
            size="lg"
            variant="outline"
            class="justify-start"
            @click="settleUp"
          >
            <UIcon
              name="i-lucide-credit-card"
              class="w-5 h-5 mr-2"
            />
            Settle Up
          </UButton>

          <UButton
            block
            size="lg"
            variant="outline"
            class="justify-start"
            @click="viewProfile"
          >
            <UIcon
              name="i-lucide-user"
              class="w-5 h-5 mr-2"
            />
            View Profile
          </UButton>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup>
const { groups, fetchGroups, isLoading: isLoadingGroups } = useGroups()
const { userStats, fetchUserStats } = useUsers()
const { showInfo } = useNotifications()

// Fetch groups on component mount
onMounted(async () => {
  try {
    await fetchGroups()
    await fetchUserStats()
  }
  catch (error) {
    console.error('Failed to fetch groups:', error)
  }
})

// Navigation and action handlers
const navigateToGroup = (groupId) => {
  navigateTo(`/groups/${groupId}`)
}

const createNewGroup = () => {
  navigateTo('/groups/add')
}

const createFirstGroup = () => {
  navigateTo('/groups/add')
}

const addExpense = () => {
  showInfo('Add expense coming soon!')
}

const settleUp = () => {
  showInfo('Settle up coming soon!')
}

const viewProfile = () => {
  showInfo('Profile view coming soon!')
}

// Set page meta
// definePageMeta({
//  middleware: 'auth',
// })
</script>
