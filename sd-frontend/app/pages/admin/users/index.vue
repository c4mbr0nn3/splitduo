<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      :title="$t('admin.users')"
      :subtitle="$t('admin.subtitle')"
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
        :placeholder="$t('admin.search')"
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
      :title="$t('admin.noUsersFound')"
      :subtitle="$t('admin.noUsersSubtitle')"
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
        @refresh="refreshUsers"
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
          {{ $t('admin.pendingInvitations') }}
        </h2>
        <p class="text-sm text-muted mt-1">
          {{ $t('admin.pendingInvitationsSubtitle') }}
        </p>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <AdminPendingUserCard
          v-for="pending in pendingUsers"
          :key="pending.email"
          :pending="pending"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
const { t } = useI18n()

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
  label: t('admin.totalUsers'),
  value: users.value.length,
  color: 'teal',
}))

const adminUsersStats = computed(() => ({
  label: t('admin.adminUsers'),
  value: adminUsers.value.length,
  color: 'rose',
}))

const regularUsersStats = computed(() => ({
  label: t('admin.regularUsers'),
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
  title: computed(() => t('admin.userManagement')),
})
</script>
