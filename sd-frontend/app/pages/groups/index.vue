<template>
  <div class="py-8">
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-primary">
        Groups
      </h1>
      <p class="text-sm text-muted mt-1">
        Manage your expense sharing groups
      </p>
    </div>

    <!-- Search Controls -->
    <div class="flex justify-between items-center mb-6 w-full">
      <UInput
        v-model="searchInput"
        icon="i-lucide-search"
        placeholder="Search groups..."
        class="w-64"
      />
      <div class="flex gap-2">
        <UButton
          icon="i-lucide-refresh-cw"
          variant="ghost"
          :loading="isLoadingGroups"
          @click="refreshGroups"
        />
        <UButton
          icon="i-lucide-plus"
          variant="outline"
          color="success"
          square
          @click="createNewGroup"
        />
      </div>
    </div>

    <!-- Loading State -->
    <UiLoadingSpinner
      v-if="isLoadingGroups"
      text="Loading groups..."
    />

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
      class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6"
    >
      <UCard
        v-for="group in filteredGroups"
        :key="group.id"
        class="hover:border-primary/50 transition-colors"
        variant="outline"
      >
        <div class="space-y-4">
          <!-- Group Header -->
          <div class="flex items-start justify-between">
            <div
              class="flex items-center gap-3 cursor-pointer"
              @click="navigateToGroup(group.id)"
            >
              <div class="border border-primary text-primary rounded-full flex items-center justify-center w-12 h-12">
                <UIcon
                  name="i-lucide-users"
                  class="size-6"
                />
              </div>
              <div>
                <h3 class="font-semibold text-primary text-lg">
                  {{ group.name }}
                </h3>
                <p class="text-sm text-muted">
                  {{ group.memberCount || 0 }} member{{ group.memberCount === 1 ? '' : 's' }}
                </p>
              </div>
            </div>
            <div class="flex items-center gap-1">
              <UButton
                variant="ghost"
                color="info"
                size="sm"
                icon="i-lucide-edit-2"
                @click="navigateToEdit(group.id)"
              />
              <UiConfirmDialog
                title="Delete Group"
                :message="`Are you sure you want to delete the group '${groupToDelete?.name}'?`"
                subtitle="This action cannot be undone and will remove all associated data."
                confirm-text="Delete Group"
                confirm-color="error"
                icon="i-lucide-trash-2"
                icon-color-class="text-error-500"
                :is-processing="isDeletingGroup"
                @confirm="deleteGroup"
              >
                <template #button>
                  <UButton
                    variant="ghost"
                    color="error"
                    size="sm"
                    icon="i-lucide-trash-2"
                    @click.stop="confirmDeleteGroup(group)"
                  />
                </template>
              </UiConfirmDialog>
            </div>
          </div>

          <!-- Group Description -->
          <div
            v-if="group.description"
            class="text-sm text-muted"
          >
            {{ group.description }}
          </div>

          <!-- Group Metadata -->
          <USeparator />
          <div class="flex items-center justify-between text-xs text-muted">
            <span>Created {{ formatDate(group.createdAt) }}</span>
            <span>Updated {{ formatDate(group.updatedAt) }}</span>
          </div>
        </div>
      </UCard>
    </div>
  </div>

  <!-- Delete Confirmation Dialog -->
</template>

<script setup>
const { groups, fetchGroups, isLoading: isLoadingGroups, deleteGroup: deleteGroupAPI } = useGroups()

// Search functionality
const { searchInput, debouncedSearchQuery } = useDebounceSearch()

// Delete group state
const groupToDelete = ref(null)
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
  await refreshGroups()
})

// Navigation handlers
const navigateToGroup = (groupId) => {
  navigateTo(`/groups/${groupId}`)
}

const createNewGroup = () => {
  navigateTo('/groups/add')
}

const navigateToEdit = (groupId) => {
  navigateTo(`/groups/${groupId}/edit/`)
}

// Delete group handlers
const confirmDeleteGroup = (group) => {
  groupToDelete.value = group
}

const deleteGroup = async () => {
  if (!groupToDelete.value) return

  isDeletingGroup.value = true
  try {
    await deleteGroupAPI(groupToDelete.value.id)
    groupToDelete.value = null
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
