<template>
  <div class="py-8">
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
    <div class="flex justify-between items-center mb-6 w-full">
      <UInput
        v-model="searchInput"
        icon="i-lucide-search"
        placeholder="Search users..."
        class="w-64"
      />
      <div class="flex gap-2">
        <UButton
          icon="i-lucide-refresh-cw"
          variant="ghost"
          square
          :loading="isLoading"
          @click="refreshUsers"
        />
        <UButton
          icon="i-lucide-user-plus"
          square

          color="success"
          variant="outline"
          @click="navigateToAdd"
        />
      </div>
    </div>
    <UiLoadingSpinner
      v-if="isLoading && !users.length"
      text="Loading users..."
    />
    <UiEmptyState
      v-else-if="filteredUsers.length === 0"
      icon="i-lucide-users"
      title="No users found"
      subtitle="No users match your search criteria or no users exist yet"
    >
      <template #action>
        <UButton
          label="Create Your First User"
          icon="i-lucide-user-plus"
          @click="navigateToAdd"
        />
      </template>
    </UiEmptyState>

    <!-- Users Grid -->
    <div
      v-else
      class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6"
    >
      <AdminUserCard
        v-for="user in filteredUsers"
        :key="user.id"
        :user="user"
        :is-deleting="isDeleting && userToDelete?.id === user.id"
        @edit="navigateToEdit"
        @revoke-tokens="revokeTokens"
        @delete="handleDeleteUser"
      />
    </div>
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

// Search functionality
const { searchInput, debouncedSearchQuery } = useDebounceSearch()

// State
const userToDelete = ref(null)
const isDeleting = ref(false)

// Computed
const filteredUsers = computed(() => {
  if (!debouncedSearchQuery.value) return users.value

  const query = debouncedSearchQuery.value.toLowerCase()
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

const navigateToEdit = (userId) => {
  navigateTo(`/admin/users/${userId}/edit`)
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

const handleDeleteUser = async (user) => {
  userToDelete.value = user
  isDeleting.value = true
  try {
    await deleteUser(user.id)
    userToDelete.value = null
  }
  catch (error) {
    console.error('Failed to delete user:', error)
  }
  finally {
    isDeleting.value = false
  }
}

// Load users on mount
onMounted(async () => {
  await refreshUsers()
})

useHead({
  title: 'User Management',
})
</script>
