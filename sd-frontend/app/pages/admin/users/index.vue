<template>
  <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header with Create User Button -->
    <UButton
      label="Create User"
      icon="i-lucide-user-plus"
      block
      class="mb-6"
      @click="navigateToAdd"
    />
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-primary">
        Users
      </h1>
      <p class="text-sm text-muted mt-1">
        Manage platform users and their permissions
      </p>
    </div>

    <!-- User Stats Cards -->
    <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
      <DashboardStatCard
        :stats="totalUsersStats"
        icon="i-lucide-users"
        color="blue"
      />

      <DashboardStatCard
        :stats="adminUsersStats"
        icon="i-lucide-crown"
        color="purple"
      />

      <DashboardStatCard
        :stats="regularUsersStats"
        icon="i-lucide-user"
        color="green"
      />
    </div>

    <!-- Users Table -->
    <UCard>
      <template #header>
        <div class="flex justify-between items-center">
          <h2 class="text-xl font-semibold text-primary">
            Users
          </h2>
          <div class="flex items-center space-x-4">
            <UInput
              v-model="searchQuery"
              icon="i-lucide-search"
              placeholder="Search users..."
              class="w-64"
            />
            <UButton
              icon="i-lucide-refresh-cw"
              variant="outline"
              :loading="isLoading"
              @click="refreshUsers"
            />
          </div>
        </div>
      </template>

      <div
        v-if="isLoading && !users.length"
        class="flex justify-center items-center py-12"
      >
        <UiLoadingSpinner />
      </div>

      <div
        v-else-if="filteredUsers.length === 0"
        class="py-12"
      >
        <UiEmptyState
          title="No users found"
          description="No users match your search criteria"
          icon="i-lucide-users"
        />
      </div>

      <div
        v-else
        class="overflow-x-auto"
      >
        <table class="min-w-full divide-y divide-border">
          <thead class="bg-secondary">
            <tr>
              <th class="px-6 py-3 text-left text-xs font-medium text-dimmed uppercase tracking-wider">
                User
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-dimmed uppercase tracking-wider">
                Email
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-dimmed uppercase tracking-wider">
                Role
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-dimmed uppercase tracking-wider">
                Created
              </th>
              <th class="px-6 py-3 text-left text-xs font-medium text-dimmed uppercase tracking-wider">
                Actions
              </th>
            </tr>
          </thead>
          <tbody class="bg-default divide-y divide-border">
            <tr
              v-for="user in filteredUsers"
              :key="user.id"
              class="hover:bg-secondary/50"
            >
              <td class="px-6 py-4 whitespace-nowrap">
                <div class="flex items-center">
                  <UAvatar
                    :alt="user.fullName || `${user.firstName} ${user.lastName}`"
                    icon="i-lucide-user"
                    class="mr-3"
                  />
                  <div>
                    <div class="text-sm font-medium text-primary">
                      {{ user.fullName || `${user.firstName} ${user.lastName || ''}`.trim() }}
                    </div>
                    <div class="text-sm text-dimmed">
                      ID: {{ user.id }}
                    </div>
                  </div>
                </div>
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <div class="text-sm text-primary">
                  {{ user.email }}
                </div>
              </td>
              <td class="px-6 py-4 whitespace-nowrap">
                <UBadge
                  :color="user.globalRoleId == 2 ? 'purple' : 'blue'"
                  variant="subtle"
                >
                  {{ user.globalRoleId == 2 ? 'Admin' : 'User' }}
                </UBadge>
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm text-dimmed">
                {{ formatDate(user.createdAt) }}
              </td>
              <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                <div class="flex items-center space-x-2">
                  <UButton
                    icon="i-lucide-eye"
                    variant="ghost"
                    size="sm"
                    color="neutral"
                    @click="viewUser(user)"
                  />
                  <UButton
                    icon="i-lucide-edit"
                    variant="ghost"
                    size="sm"
                    color="info"
                    @click="navigateToEdit(user.id)"
                  />
                  <UButton
                    icon="i-lucide-key"
                    variant="ghost"
                    size="sm"
                    color="warning"
                    @click="revokeTokens(user)"
                  />
                  <UButton
                    icon="i-lucide-trash-2"
                    variant="ghost"
                    size="sm"
                    color="error"
                    @click="confirmDeleteUser(user)"
                  />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <!-- Delete Confirmation Dialog -->
    <UiConfirmDialog
      v-model:open="showDeleteDialog"
      title="Delete User"
      :message="`Are you sure you want to delete ${userToDelete?.fullName || `${userToDelete?.firstName} ${userToDelete?.lastName || ''}`.trim()}? This action cannot be undone.`"
      confirm-text="Delete"
      confirm-color="red"
      :loading="isDeleting"
      @confirm="handleDeleteUser"
    />
  </div>
</template>

<script setup>
definePageMeta({
  middleware: ['auth', 'admin'],
  layout: 'default',
})

const {
  users,
  isLoading,
  fetchUsers,
  deleteUser,
  revokeUserTokens,
} = useUsers()

const { showSuccess } = useNotifications()

// State
const searchQuery = ref('')
const showDeleteDialog = ref(false)
const userToDelete = ref(null)
const isDeleting = ref(false)

// Computed
const filteredUsers = computed(() => {
  if (!searchQuery.value) return users.value

  const query = searchQuery.value.toLowerCase()
  return users.value.filter((user) => {
    return (
      user.firstName.toLowerCase().includes(query)
      || (user.lastName && user.lastName.toLowerCase().includes(query))
      || user.email.toLowerCase().includes(query)
    )
  })
})

const adminUsers = computed(() => {
  return users.value.filter(user => user.globalRoleId == 2)
})

const regularUsers = computed(() => {
  return users.value.filter(user => user.globalRoleId != 2)
})

// Stats objects for StatCard component
const totalUsersStats = computed(() => ({
  label: 'Total Users',
  value: users.value.length,
  color: 'blue',
}))

const adminUsersStats = computed(() => ({
  label: 'Admin Users',
  value: adminUsers.value.length,
  color: 'purple',
}))

const regularUsersStats = computed(() => ({
  label: 'Regular Users',
  value: regularUsers.value.length,
  color: 'green',
}))

// Methods
const refreshUsers = async () => {
  try {
    await fetchUsers()
  }
  catch (error) {
    console.error('Failed to refresh users:', error)
  }
}

const viewUser = (user) => {
  // TODO: Implement user detail view
  showSuccess(`Viewing ${user.fullName || `${user.firstName} ${user.lastName || ''}`.trim()}`)
}

const navigateToEdit = (userId) => {
  navigateTo(`/admin/users/${userId}/edit`)
}

const confirmDeleteUser = (user) => {
  userToDelete.value = user
  showDeleteDialog.value = true
}

const revokeTokens = async (user) => {
  try {
    await revokeUserTokens(user.id)
  }
  catch (error) {
    console.error('Failed to revoke tokens:', error)
  }
}

const navigateToAdd = () => {
  navigateTo('/admin/users/add')
}

const handleDeleteUser = async () => {
  if (!userToDelete.value) return

  isDeleting.value = true
  try {
    await deleteUser(userToDelete.value.id)
    showDeleteDialog.value = false
    userToDelete.value = null
  }
  catch (error) {
    console.error('Failed to delete user:', error)
  }
  finally {
    isDeleting.value = false
  }
}

const formatDate = (timestamp) => {
  if (!timestamp) return 'N/A'
  return new Date(timestamp * 1000).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

// Load users on mount
onMounted(async () => {
  await refreshUsers()
})
</script>
