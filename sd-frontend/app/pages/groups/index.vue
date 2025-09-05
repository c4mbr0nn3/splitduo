<template>
  <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Header with Create Group Button -->
    <UButton
      label="Create Group"
      icon="i-lucide-plus"
      block
      class="mb-6"
      @click="createNewGroup"
    />
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-primary">
        Groups
      </h1>
      <p class="text-sm text-muted mt-1">
        Manage your expense sharing groups
      </p>
    </div>
    <!-- Loading State -->
    <UiLoadingSpinner
      v-if="isLoadingGroups"
      text="Loading groups..."
    />

    <!-- Empty State -->
    <UiEmptyState
      v-else-if="groups.length === 0"
      icon="i-lucide-users"
      title="No groups yet"
      subtitle="Get started by creating your first group to track shared expenses"
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
        v-for="group in groups"
        :key="group.id"
        class="cursor-pointer hover:border-primary/50 transition-colors"
        variant="outline"
        @click="navigateToGroup(group.id)"
      >
        <div class="space-y-4">
          <!-- Group Header -->
          <div class="flex items-start justify-between">
            <div class="flex items-center gap-3">
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
            <UIcon
              name="i-lucide-chevron-right"
              class="size-5 text-muted"
            />
          </div>

          <!-- Group Description -->
          <div
            v-if="group.description"
            class="text-sm text-muted"
          >
            {{ group.description }}
          </div>

          <!-- Group Metadata -->
          <div class="flex items-center justify-between text-xs text-muted pt-2 border-t border-border">
            <span>Created {{ formatDate(group.createdAt) }}</span>
            <span>Updated {{ formatDate(group.updatedAt) }}</span>
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup>
const { groups, fetchGroups, isLoading: isLoadingGroups } = useGroups()

// Fetch groups on component mount
onMounted(async () => {
  try {
    await fetchGroups()
  }
  catch (error) {
    console.error('Failed to fetch groups:', error)
  }
})

// Navigation handlers
const navigateToGroup = (groupId) => {
  navigateTo(`/groups/${groupId}`)
}

const createNewGroup = () => {
  navigateTo('/groups/add')
}

// Utility function to format dates
const formatDate = (timestamp) => {
  if (!timestamp) return 'Unknown'
  const date = new Date(timestamp * 1000) // Convert Unix timestamp to Date
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

// Page meta
useHead({
  title: 'Groups',
})

definePageMeta({
  middleware: 'auth',
})
</script>
