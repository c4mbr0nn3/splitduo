<template>
  <div class="py-6 sm:py-8">
    <UCard>
      <template #header>
        <UiCardHeader
          :title="group?.name || $t('imports.title')"
          :subtitle="$t('imports.subtitle')"
          :back-to="`/groups/${groupId}`"
        />
      </template>

      <div>
        <div class="flex items-center justify-between mb-4">
          <h3 class="text-lg font-semibold text-primary">
            {{ $t('imports.imports') }}
          </h3>
          <UButton
            :label="$t('imports.addImport')"
            icon="i-lucide-upload"
            size="sm"
            @click="navigateTo(`/groups/${groupId}/imports/add`)"
          />
        </div>
        <UiLoadingSpinner
          v-if="isLoading"
          :text="$t('common.loading')"
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
                      <span class="font-medium">{{ $t('imports.records') }}</span>
                      {{ importItem.recordsCount }}
                    </div>
                    <div>
                      <span class="font-medium">{{ $t('imports.importDate') }}</span>
                      {{ formatDateString(importItem.importDate) }}
                    </div>
                    <div v-if="importItem.duration">
                      <span class="font-medium">{{ $t('imports.duration') }}</span>
                      {{ formatDuration(importItem.duration) }}
                    </div>
                  </div>
                  <UAlert
                    v-if="importItem.errorDetails && importItem.importStatusId === 4"
                    color="error"
                    :title="$t('imports.importFailed')"
                    :description="importItem.errorDetails"
                    variant="subtle"
                    icon="i-lucide-triangle-alert"
                    class="mt-4"
                  />
                  <UAlert
                    v-if="importItem.importStatusId === 5"
                    color="warning"
                    :title="$t('imports.configurationRequired')"
                    :description="$t('imports.configurationDescription')"
                    variant="subtle"
                    icon="i-lucide-settings"
                    class="mt-4"
                  >
                    <template #actions>
                      <UButton
                        :label="$t('imports.configure')"
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
          :title="$t('imports.noImportsFound')"
          :subtitle="$t('imports.noImportsSubtitle')"
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
const { t } = useI18n()
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup } = useGroups()
const { imports, fetchImports, pagination, isLoading } = useImportExport(groupId)

const group = computed(() => currentGroup.value)
const currentPage = ref(1)

// Import status mapping (based on backend ImportStatus enum)
const importStatusMap = computed(() => ({
  1: { label: t('imports.pending'), color: 'neutral', variant: 'soft' },
  2: { label: t('imports.processing'), color: 'info', variant: 'soft' },
  3: { label: t('imports.completed'), color: 'success', variant: 'soft' },
  4: { label: t('imports.failed'), color: 'error', variant: 'soft' },
  5: { label: t('imports.analysisComplete'), color: 'warning', variant: 'soft' },
}))

// Import type mapping (based on backend ImportType enum)
const importTypeMap = {
  1: 'Cospend',
  2: 'SplitDuo',
  4: 'SplitDuo Alias',
}

const getStatusLabel = (statusId) => {
  return importStatusMap.value[statusId]?.label || t('imports.unknown')
}

const getStatusColor = (statusId) => {
  return importStatusMap.value[statusId]?.color || 'neutral'
}

const getStatusVariant = (statusId) => {
  return importStatusMap.value[statusId]?.variant || 'soft'
}

const getImportTypeLabel = (typeId) => {
  return importTypeMap[typeId] || t('imports.unknown')
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
  title: computed(() => `${group.value?.name || ''} - ${t('imports.imports')}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
