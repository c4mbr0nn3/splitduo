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
      <UCard
        v-for="group in filteredGroups"
        :key="group.id"
        class="sd-surface sd-surface-hover"
        :ui="{ body: 'p-4 sm:p-5' }"
      >
        <NuxtLink
          :to="`/groups/${group.id}`"
          class="block"
        >
          <div class="flex items-start justify-between gap-4">
            <div class="flex items-center gap-3 min-w-0">
              <div class="shrink-0 w-12 h-12 rounded-full border border-neutral-200 dark:border-neutral-700 bg-neutral-100/50 dark:bg-neutral-800/50 flex items-center justify-center text-muted">
                <UIcon
                  name="i-lucide-users"
                  class="size-6"
                />
              </div>
              <div class="min-w-0">
                <h3 class="font-semibold text-highlighted text-lg truncate">
                  {{ group.name }}
                </h3>
                <p class="text-sm text-muted">
                  {{ group.memberCount || 0 }} member{{ group.memberCount === 1 ? '' : 's' }}
                </p>
              </div>
            </div>
            <span @click.stop>
              <UiButtonDropdown
                icon-only
                dropdown-icon="i-lucide-ellipsis-vertical"
                size="md"
                square
                variant="ghost"
                color="neutral"
                :items="dropdownItems(group)"
                :disabled="isDeletingGroup"
              />
            </span>
          </div>

          <div
            v-if="group.description"
            class="text-sm text-muted mt-3"
          >
            {{ group.description }}
          </div>

          <div class="flex items-center justify-between mt-3 text-xs text-muted">
            <span>Updated {{ formatDate(group.updatedAt) }}</span>
            <UBadge
              :color="group.netBalance > 0 ? 'success' : group.netBalance < 0 ? 'error' : 'neutral'"
              variant="subtle"
              class="sd-tabular"
            >
              {{ group.netBalance > 0 ? `owed ${formatCurrency(group.netBalance)}` : group.netBalance < 0 ? `owes ${formatCurrency(Math.abs(group.netBalance))}` : 'settled' }}
            </UBadge>
          </div>
        </NuxtLink>
      </UCard>
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

const navigateToEdit = (groupId) => {
  navigateTo(`/groups/${groupId}/edit/`)
}

const dropdownItems = (group) => {
  return [
    {
      label: 'Edit',
      icon: 'i-lucide-edit-2',
      color: 'info',
      onSelect: () => navigateToEdit(group.id),
    },
    {
      type: 'separator',
    },
    {
      label: 'Delete',
      icon: 'i-lucide-trash-2',
      color: 'error',
      onSelect: () => confirmDeleteGroup(group),
    },
  ]
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
