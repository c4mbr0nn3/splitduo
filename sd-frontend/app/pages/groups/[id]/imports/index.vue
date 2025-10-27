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
            <p class="text-sm text-muted">
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
            <UCard
              v-for="importItem in imports"
              :key="importItem.fileHash"
            >
              <div class="flex items-start justify-between">
                <div class="flex-1">
                  <div class="flex flex-col gap-1 mb-2">
                    <div class="flex justify-between items-center gap-2 mb-1">
                      <UBadge
                        :label="getImportTypeLabel(importItem.importTypeId)"
                        color="primary"
                        variant="soft"
                        icon="i-lucide-folder"
                      />
                      <UBadge
                        :label="getStatusLabel(importItem.importStatusId)"
                        :variant="getStatusVariant(importItem.importStatusId)"
                        :color="getStatusColor(importItem.importStatusId)"
                        icon="i-lucide-info"
                      />
                    </div>
                    <h3 class="font-medium">
                      {{ importItem.fileName }}
                    </h3>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-4 gap-2 text-sm text-muted">
                    <div>
                      <span class="font-medium">Records:</span>
                      {{ importItem.recordsCount }}
                    </div>
                    <div>
                      <span class="font-medium">Import Date:</span>
                      {{ formatDateString(importItem.importDate) }}
                    </div>
                    <div v-if="importItem.duration">
                      <span class="font-medium">Duration:</span>
                      {{ formatDuration(importItem.duration) }}
                    </div>
                  </div>
                  <UAlert
                    v-if="importItem.errorDetails && importItem.importStatusId === 4"
                    color="error"
                    title="Import Failed"
                    :description="importItem.errorDetails"
                    variant="subtle"
                    icon="i-lucide-triangle-alert"
                    class="mt-4"
                  />
                  <UAlert
                    v-if="importItem.importStatusId === 5"
                    color="warning"
                    title="Configuration Required"
                    description="File analysis is complete. Click 'Configure' to set up mappings and continue with the import."
                    variant="subtle"
                    icon="i-lucide-settings"
                    class="mt-4"
                  >
                    <template #actions>
                      <UButton
                        label="Configure"
                        color="warning"
                        variant="outline"
                        size="xs"
                        icon="i-lucide-settings"
                        @click="continueImport(importItem)"
                      />
                    </template>
                  </UAlert>
                </div>
              </div>
            </UCard>
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
  1: { label: 'Pending', color: 'neutral', variant: 'soft' },
  2: { label: 'Processing', color: 'info', variant: 'soft' },
  3: { label: 'Completed', color: 'success', variant: 'soft' },
  4: { label: 'Failed', color: 'error', variant: 'soft' },
  5: { label: 'Analysis Complete', color: 'warning', variant: 'soft' },
}

// Import type mapping (based on backend ImportType enum)
const importTypeMap = {
  1: 'Cospend',
  2: 'SplitDuo',
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

const getImportTypeLabel = (typeId) => {
  return importTypeMap[typeId] || 'Unknown'
}

const continueImport = (importItem) => {
  // Navigate to add page with the import ID as a query parameter
  // This will allow the add page to load the existing analysis results
  navigateTo(`/groups/${groupId}/imports/add?continue=${importItem.guid}`)
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
