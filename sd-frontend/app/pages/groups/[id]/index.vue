<template>
  <div class="py-6 sm:py-8">
    <UiLoadingSpinner
      v-if="isLoading"
      :text="$t('groups.loadingGroup')"
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-users"
      :title="$t('groups.unableToLoad')"
    >
      <template #action>
        <UButton
          color="primary"
          variant="outline"
          size="sm"
          @click="retryLoad"
        >
          {{ $t('groups.retry') }}
        </UButton>
      </template>
    </UiEmptyState>

    <template v-else>
      <UiCardHeader
        :title="$t('groups.groupDetails')"
        back-to="/groups"
        class="mb-6"
      />

      <UCard
        class="sd-surface"
        :ui="{ body: 'p-4 sm:p-6' }"
      >
        <template #header>
          <GroupsSectionHeader
            :group="group"
            :alias-count="group?.useAliases ? aliasCount : null"
            :is-exporting="isExporting"
            @export="handleExport"
          />
        </template>

        <GroupsTabsNav :group-id="groupId" />
        <Transition
          name="sd-fade"
          mode="out-in"
        >
          <GroupsExpensesTab
            v-if="activeTab === 'expenses'"
            :key="'expenses'"
            :group-id="groupId"
          />
          <GroupsStatsTab
            v-else-if="activeTab === 'stats'"
            :key="'stats'"
            :group-id="groupId"
          />
        </Transition>
      </UCard>
    </template>
  </div>
</template>

<script setup>
const { t } = useI18n()
const route = useRoute()
const groupId = route.params.id
const { currentGroup, fetchGroup, isLoading } = useGroups()
const { aliases, fetchAliases } = useAliases()
const { exportToCsv, isExporting } = useImportExport(groupId)

const activeTab = computed(() => route.query.tab === 'stats' ? 'stats' : 'expenses')
const group = computed(() => currentGroup.value)
const aliasCount = computed(() => aliases.value?.length || 0)
const loadError = ref(false)

const handleExport = async () => {
  try {
    await exportToCsv()
  }
  catch (error) {
    console.error('Failed to export group data:', error)
  }
}

const retryLoad = async () => {
  loadError.value = false
  if (groupId) {
    await fetchGroup(groupId).catch(() => {
      loadError.value = true
    })
  }
}

onMounted(async () => {
  if (groupId) {
    try {
      await fetchGroup(groupId)
      if (group.value?.useAliases) {
        await fetchAliases(groupId)
      }
    }
    catch {
      loadError.value = true
    }
  }
})

useHead({
  title: computed(() => group.value?.name || t('groups.title')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
