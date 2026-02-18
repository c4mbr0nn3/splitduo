<template>
  <div class="py-8">
    <UCard
      variant="soft"
    >
      <template #header>
        <GroupsSectionHeader
          :group="group"
          :is-exporting="isExporting"
          @export="handleExport"
        />
      </template>

      <GroupsTabsNav :group-id="groupId" />
      <GroupsExpensesTab
        v-if="activeTab === 'expenses'"
        :group-id="groupId"
      />
      <GroupsStatsTab
        v-else-if="activeTab === 'stats'"
        :group-id="groupId"
      />
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id
const { currentGroup, fetchGroup } = useGroups()
const { exportToCsv, isExporting } = useImportExport(groupId)

const activeTab = computed(() => route.query.tab === 'stats' ? 'stats' : 'expenses')
const group = computed(() => currentGroup.value)

const handleExport = async () => {
  try {
    await exportToCsv()
  }
  catch (error) {
    console.error('Failed to export group data:', error)
  }
}

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
  }
})

useHead({
  title: computed(() => group.value?.name || 'Group'),
})

definePageMeta({
  middleware: 'auth',
})
</script>
