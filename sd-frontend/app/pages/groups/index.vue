<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      title="Groups"
      subtitle="Manage your expense sharing groups"
      class="mb-6"
    />

    <!-- Search Controls -->
    <div class="flex items-center gap-3 mb-6 w-full">
      <UInput
        v-model="searchInput"
        icon="i-lucide-search"
        placeholder="Search groups..."
        class="w-full sm:w-64 md:w-80"
      />
      <UButton
        icon="i-lucide-refresh-cw"
        variant="ghost"
        :loading="isLoadingGroups"
        @click="refreshGroups"
      />
      <UButton
        to="/groups/add"
        icon="i-lucide-plus"
        square
        class="sm:hidden"
      />
      <UButton
        to="/groups/add"
        icon="i-lucide-plus"
        label="New Group"
        class="hidden sm:inline-flex"
      />
    </div>

    <!-- Loading State -->
    <div
      v-if="showSkeleton"
      class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3 sm:gap-4 lg:gap-6"
    >
      <GroupsGroupCardSkeleton
        v-for="i in 6"
        :key="i"
      />
    </div>

    <!-- Empty State -->
    <UiEmptyState
      v-else-if="filteredGroups.length === 0"
      icon="i-lucide-users"
      :title="debouncedSearchQuery ? 'No groups found' : 'No groups yet'"
      :subtitle="debouncedSearchQuery ? 'No groups match your search criteria' : 'Get started by creating your first group to track shared expenses'"
    >
      <template #action>
        <UButton
          label="Create Your First Group"
          icon="i-lucide-plus"
          @click="createNewGroup"
        />
      </template>
    </UiEmptyState>

    <!-- Groups Grid -->
    <div
      v-else
      class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3 sm:gap-4 lg:gap-6"
    >
      <GroupsGroupCard
        v-for="group in filteredGroups"
        :key="group.id"
        :group="group"
        :is-deleting="isDeletingGroup"
        @delete="confirmDeleteGroup"
      />
    </div>
  </div>
</template>

<script setup>
const { groups, fetchGroups, isLoading: isLoadingGroups, deleteGroup: deleteGroupAPI } = useGroups()

const showSkeleton = ref(true)
const modal = useModal()

// Search functionality
const { searchInput, debouncedSearchQuery } = useDebounceSearch()

// Delete group state
const isDeletingGroup = ref(false)

// Computed
const filteredGroups = computed(() => {
  if (!debouncedSearchQuery.value) return groups.value

  const query = debouncedSearchQuery.value.toLowerCase()
  return groups.value.filter((group) => {
    return (
      group.name.toLowerCase().includes(query)
      || (group.description && group.description.toLowerCase().includes(query))
    )
  })
})

// Refresh groups
const refreshGroups = async () => {
  try {
    await fetchGroups()
  }
  catch (error) {
    console.error('Failed to refresh groups:', error)
  }
}

// Fetch groups on component mount
onMounted(async () => {
  try {
    await withMinDuration(() => refreshGroups())
  }
  finally {
    showSkeleton.value = false
  }
})

// Navigation handlers
const createNewGroup = () => {
  navigateTo('/groups/add')
}

// Delete group handlers
const confirmDeleteGroup = async (group) => {
  const confirmed = await modal.error({
    title: 'Delete Group',
    subtitle: 'This action cannot be undone.',
    content: `The group '${group.name}' will be permanently deleted. Are you sure you want to delete this group?`,
    confirmText: 'Delete Group',
    cancelText: 'Cancel',
  })

  if (confirmed) {
    await deleteGroup(group.id)
  }
}

const deleteGroup = async (groupId) => {
  isDeletingGroup.value = true
  try {
    await deleteGroupAPI(groupId)
  }
  catch (error) {
    console.error('Failed to delete group:', error)
  }
  finally {
    isDeletingGroup.value = false
  }
}

// Page meta
useHead({
  title: 'Groups',
})

definePageMeta({
  middleware: 'auth',
})
</script>
