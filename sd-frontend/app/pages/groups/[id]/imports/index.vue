<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard
      class="w-full max-w-4xl"
      variant="soft"
    >
      <template #header>
        <div class="flex items-center justify-between">
          <div>
            <h2 class="text-xl font-semibold text-primary">
              {{ group?.name }}
            </h2>
            <p class="text-sm text-gray-600 dark:text-gray-400">
              Manage your group imports and view import history
            </p>
          </div>
        </div>
      </template>

      <div>
        <div class="flex items-center justify-between mb-4">
          <h3 class="text-lg font-semibold text-primary">
            Imports
          </h3>
          <UButton
            label="Add Import"
            icon="i-lucide-upload"
            size="sm"
            @click="navigateTo(`/groups/${groupId}/imports/add`)"
          />
        </div>
        <UiLoadingSpinner
          v-if="isLoading"
          text="Loading imports..."
        />
        <div v-else-if="imports.length">
          <div class="space-y-4">
            <div
              v-for="importItem in imports"
              :key="importItem.fileHash"
              class="border dark:border-gray-700 rounded-lg p-4 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
            >
              <div class="flex items-start justify-between">
                <div class="flex-1">
                  <div class="flex items-center gap-3 mb-2">
                    <div
                      class="w-3 h-3 rounded-full"
                      :class="getStatusColor(importItem.importStatusId)"
                    />
                    <h3 class="font-medium text-gray-900 dark:text-white">
                      {{ importItem.fileName }}
                    </h3>
                    <UBadge
                      :label="getStatusLabel(importItem.importStatusId)"
                      :variant="getStatusVariant(importItem.importStatusId)"
                      size="xs"
                    />
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-3 gap-2 text-sm text-gray-600 dark:text-gray-400">
                    <div>
                      <span class="font-medium">Records:</span>
                      {{ importItem.recordsCount }}
                    </div>
                    <div>
                      <span class="font-medium">Import Date:</span>
                      {{ formatDate(importItem.importDate) }}
                    </div>
                    <div v-if="importItem.duration">
                      <span class="font-medium">Duration:</span>
                      {{ formatDuration(importItem.duration) }}
                    </div>
                  </div>
                  <div
                    v-if="importItem.errorDetails && importItem.importStatusId === 3"
                    class="mt-2 p-2 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded text-sm text-red-700 dark:text-red-400"
                  >
                    <strong>Error:</strong> {{ importItem.errorDetails }}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <UiEmptyState
          v-else
          icon="i-lucide-file-text"
          title="No imports found"
          subtitle="Start by importing your data to see import history here"
        />
      </div>

      <template #footer>
        <div
          v-if="pagination.totalPages > 1"
          class="flex justify-center"
        >
          <UPagination
            v-model:page="currentPage"
            :items-per-page="pagination.limit"
            :total="pagination.total"
            :sibling-count="1"
          />
        </div>
      </template>
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup } = useGroups()
const { imports, fetchImports, pagination, isLoading } = useImportExport(groupId)

const group = computed(() => currentGroup.value)
const currentPage = ref(1)

// Import status mapping (based on backend ImportStatus enum)
const importStatusMap = {
  1: { label: 'Pending', color: 'bg-yellow-500', variant: 'soft' },
  2: { label: 'Processing', color: 'bg-blue-500', variant: 'soft' },
  3: { label: 'Failed', color: 'bg-red-500', variant: 'soft' },
  4: { label: 'Completed', color: 'bg-green-500', variant: 'soft' },
}

const getStatusLabel = (statusId) => {
  return importStatusMap[statusId]?.label || 'Unknown'
}

const getStatusColor = (statusId) => {
  return importStatusMap[statusId]?.color || 'bg-gray-500'
}

const getStatusVariant = (statusId) => {
  return importStatusMap[statusId]?.variant || 'soft'
}

const formatDate = (dateString) => {
  if (!dateString) return 'N/A'
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const formatDuration = (durationMs) => {
  if (!durationMs) return 'N/A'
  const seconds = Math.round(durationMs / 1000)
  return `${seconds}s`
}

watch(currentPage, async (newPage) => {
  await fetchImports({ page: newPage })
}, { immediate: false })

onMounted(async () => {
  if (groupId) {
    await Promise.all([
      fetchGroup(groupId),
      fetchImports({ page: 1 }),
    ])
  }
})

useHead({
  title: computed(() => `${group.value?.name || 'Group'} - Imports`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
