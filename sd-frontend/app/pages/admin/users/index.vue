<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      title="Users"
      subtitle="Manage platform users"
      class="mb-6"
    >
      <template #actions>
        <UBadge
          color="neutral"
          variant="subtle"
        >
          v{{ appVersion }}
        </UBadge>
      </template>
    </UiCardHeader>

    <!-- User Stats Cards -->
    <div class="grid grid-cols-2 lg:grid-cols-3 gap-3 sm:gap-4 mb-8">
      <template v-if="showSkeleton">
        <DashboardStatCardSkeleton
          v-for="i in 3"
          :key="i"
        />
      </template>
      <template v-else>
        <DashboardStatCard
          :stats="totalUsersStats"
          icon="i-lucide-users"
          color="teal"
        />
        <DashboardStatCard
          :stats="adminUsersStats"
          icon="i-lucide-crown"
          color="rose"
        />
        <DashboardStatCard
          :stats="regularUsersStats"
          icon="i-lucide-user"
          color="green"
        />
      </template>
    </div>
    <div class="flex justify-between items-center mb-6 w-full">
      <UInput
        v-model="searchInput"
        icon="i-lucide-search"
        placeholder="Search users..."
        class="w-full sm:w-64 md:w-80"
      />
      <UButton
        icon="i-lucide-refresh-cw"
        variant="ghost"
        size="sm"
        square
        :loading="isLoading"
        @click="refreshUsers"
      />
    </div>
    <div
      v-if="showSkeleton"
      class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6"
    >
      <AdminUserCardSkeleton
        v-for="i in 6"
        :key="i"
      />
    </div>
    <UiEmptyState
      v-else-if="filteredUsers.length === 0"
      icon="i-lucide-users"
      title="No users found"
      subtitle="No users match your search criteria or no users exist yet"
    />

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

    <!-- Pending Invitations Section -->
    <div
      v-if="pendingUsers.length"
      class="mt-12"
    >
      <USeparator class="mb-8" />
      <div class="mb-6">
        <h2 class="text-xl font-bold text-primary">
          Pending Invitations
        </h2>
        <p class="text-sm text-muted mt-1">
          Users who have been invited but haven't registered yet
        </p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <UCard
          v-for="pending in pendingUsers"
          :key="pending.email"
          variant="outline"
        >
          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UIcon
                  name="i-lucide-mail"
                  class="text-muted"
                />
                <span class="font-medium text-sm">{{ pending.email }}</span>
              </div>
              <UBadge
                variant="soft"
                color="warning"
                label="Pending"
                size="xs"
              />
            </div>
            <div class="space-y-1">
              <p class="text-xs text-muted">
                Invited to:
              </p>
              <div class="flex flex-wrap gap-1">
                <UBadge
                  v-for="group in pending.groups"
                  :key="group.id"
                  variant="subtle"
                  size="xs"
                  :label="group.name"
                />
              </div>
            </div>
          </div>
        </UCard>
      </div>
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

const { pendingUsers, fetchPendingInvitations } = useInvitations()

// Search functionality
const { searchInput, debouncedSearchQuery } = useDebounceSearch()

// State
const showSkeleton = ref(true)
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

const appVersion = computed(() => useRuntimeConfig().public.appVersion)

// Stats objects for StatCard component
const totalUsersStats = computed(() => ({
  label: 'Total Users',
  value: users.value.length,
  color: 'teal',
}))

const adminUsersStats = computed(() => ({
  label: 'Admin Users',
  value: adminUsers.value.length,
  color: 'rose',
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
  try {
    await withMinDuration(async () => {
      await Promise.all([refreshUsers(), fetchPendingInvitations()])
    })
  }
  finally {
    showSkeleton.value = false
  }
})

useHead({
  title: 'User Management',
})
</script>
