<template>
  <div class="py-6 sm:py-8">
    <UiCardHeader
      size="lg"
      :title="$t('groups.title')"
      :subtitle="$t('groups.subtitle')"
      class="mb-6"
    />

    <!-- Search Controls -->
    <div class="flex items-center gap-3 mb-6 w-full">
      <UInput
        v-model="searchInput"
        icon="i-lucide-search"
        :placeholder="$t('groups.search')"
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
        :label="$t('groups.newGroup')"
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
      :title="debouncedSearchQuery ? $t('groups.notFound') : $t('groups.noGroupsYet')"
      :subtitle="debouncedSearchQuery ? $t('groups.noMatch') : $t('groups.createFirst')"
    >
      <template #action>
        <UButton
          :label="$t('dashboard.createFirstGroup')"
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

<script setup lang="ts">
import type { Group } from '~/types/domain'

const { t } = useI18n()

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
  catch (error: unknown) {
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
const confirmDeleteGroup = async (group: Group) => {
  const confirmed = await modal.error({
    title: t('groups.deleteTitle'),
    subtitle: t('groups.deleteConfirm'),
    content: t('groups.deleteContent', { name: group.name }),
    confirmText: t('groups.deleteButton'),
    cancelText: t('common.cancel'),
  })

  if (confirmed) {
    await deleteGroup(group.id)
  }
}

const deleteGroup = async (groupId: string) => {
  isDeletingGroup.value = true
  try {
    await deleteGroupAPI(groupId)
  }
  catch (error: unknown) {
    console.error('Failed to delete group:', error)
  }
  finally {
    isDeletingGroup.value = false
  }
}

// Page meta
useHead({
  title: computed(() => t('groups.title')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
