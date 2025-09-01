<template>
  <div class="min-h-screen">
    <!-- Header -->
    <div>
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="flex justify-between items-center h-16">
          <div class="flex items-center">
            <h1 class="text-2xl font-bold text-gray-100">
              SplitDuo
            </h1>
          </div>
          <div class="flex items-center gap-4">
            <span class="text-sm text-gray-600">
              Welcome, {{ user?.firstName || 'User' }}
            </span>
            <UButton
              variant="outline"
              :loading="isLoggingOut"
              @click="handleLogout"
            >
              Logout
            </UButton>
          </div>
        </div>
      </div>
    </div>

    <!-- Main Content -->
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <!-- Stats Cards -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        <UCard>
          <div class="flex items-center">
            <div class="flex-shrink-0">
              <div class="w-8 h-8 bg-blue-100 rounded-lg flex items-center justify-center">
                <UIcon
                  name="i-lucide-users"
                  class="w-5 h-5 text-blue-600"
                />
              </div>
            </div>
            <div class="ml-4">
              <p class="text-sm font-medium text-gray-600">
                Total Groups
              </p>
              <p class="text-2xl font-bold text-gray-900">
                {{ groups.length }}
              </p>
            </div>
          </div>
        </UCard>

        <UCard>
          <div class="flex items-center">
            <div class="flex-shrink-0">
              <div class="w-8 h-8 bg-green-100 rounded-lg flex items-center justify-center">
                <UIcon
                  name="i-lucide-receipt"
                  class="w-5 h-5 text-green-600"
                />
              </div>
            </div>
            <div class="ml-4">
              <p class="text-sm font-medium text-gray-600">
                Total Expenses
              </p>
              <p class="text-2xl font-bold text-gray-900">
                $0.00
              </p>
            </div>
          </div>
        </UCard>

        <UCard>
          <div class="flex items-center">
            <div class="flex-shrink-0">
              <div class="w-8 h-8 bg-yellow-100 rounded-lg flex items-center justify-center">
                <UIcon
                  name="i-lucide-dollar-sign"
                  class="w-5 h-5 text-yellow-600"
                />
              </div>
            </div>
            <div class="ml-4">
              <p class="text-sm font-medium text-gray-600">
                You Owe
              </p>
              <p class="text-2xl font-bold text-red-600">
                $0.00
              </p>
            </div>
          </div>
        </UCard>

        <UCard>
          <div class="flex items-center">
            <div class="flex-shrink-0">
              <div class="w-8 h-8 bg-purple-100 rounded-lg flex items-center justify-center">
                <UIcon
                  name="i-lucide-trending-up"
                  class="w-5 h-5 text-purple-600"
                />
              </div>
            </div>
            <div class="ml-4">
              <p class="text-sm font-medium text-gray-600">
                You're Owed
              </p>
              <p class="text-2xl font-bold text-green-600">
                $0.00
              </p>
            </div>
          </div>
        </UCard>
      </div>

      <!-- Recent Groups & Quick Actions -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Recent Groups -->
        <UCard>
          <template #header>
            <div class="flex items-center justify-between">
              <h2 class="text-lg font-semibold text-gray-900">
                Recent Groups
              </h2>
              <UButton
                size="sm"
                variant="outline"
              >
                View All
              </UButton>
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
              class="w-12 h-12 text-gray-300 mx-auto mb-4"
            />
            <p class="text-gray-500 mb-4">
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
            <div
              v-for="group in groups.slice(0, 3)"
              :key="group.id"
              class="flex items-center justify-between p-4 border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors cursor-pointer"
              @click="navigateToGroup(group.id)"
            >
              <div class="flex items-center">
                <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center mr-3">
                  <UIcon
                    name="i-lucide-users"
                    class="w-5 h-5 text-blue-600"
                  />
                </div>
                <div>
                  <h3 class="font-medium text-gray-900">
                    {{ group.name }}
                  </h3>
                  <p class="text-sm text-gray-500">
                    {{ group.members?.length || 0 }} members
                  </p>
                </div>
              </div>
              <UIcon
                name="i-lucide-chevron-right"
                class="w-5 h-5 text-gray-400"
              />
            </div>
          </div>
        </UCard>

        <!-- Quick Actions -->
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold text-gray-900">
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
  </div>
</template>

<script setup>
const { user, logout } = useAuth()
const { groups, fetchGroups, createGroup, isLoading: isLoadingGroups } = useGroups()
const { showSuccess, showInfo } = useNotifications()

const isLoggingOut = ref(false)

// Fetch groups on component mount
onMounted(async () => {
  try {
    await fetchGroups()
  }
  catch (error) {
    console.error('Failed to fetch groups:', error)
  }
})

// Logout handler
const handleLogout = async () => {
  isLoggingOut.value = true
  try {
    await logout()
    showSuccess('Logged out successfully')
  }
  catch (error) {
    console.error('Logout failed:', error)
  }
  finally {
    isLoggingOut.value = false
  }
}

// Navigation and action handlers
const navigateToGroup = (groupId) => {
  navigateTo(`/groups/${groupId}`)
}

const createNewGroup = () => {
  showInfo('Group creation coming soon!')
}

const createFirstGroup = () => {
  showInfo('Group creation coming soon!')
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
definePageMeta({
  middleware: 'auth',
})
</script>
