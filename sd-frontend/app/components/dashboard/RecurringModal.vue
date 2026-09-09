<script setup lang="ts">
import type { Group } from '~/types/domain'

const isOpen = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const { groups, fetchGroups } = useGroups()

const isLoadingGroups = ref(false)
const isSelecting = ref(false)
const loadError = ref(false)

// Fetch the full group list each time the picker opens (dashboard only
// preloads 3; the picker must list all of the user's groups).
watch(isOpen, async (open) => {
  if (!open) return
  loadError.value = false
  isLoadingGroups.value = true
  try {
    await fetchGroups({ limit: 100 })
  }
  catch {
    loadError.value = true // toast already shown by useGroups
  }
  finally {
    isLoadingGroups.value = false
  }
})

const onSelectGroup = async (group: Group) => {
  if (isSelecting.value) return
  isSelecting.value = true
  try {
    isOpen.value = false
    await navigateTo(`/groups/${group.id}/recurring/add`)
  }
  catch {
    loadError.value = true // toasts already shown by composables
  }
  finally {
    isSelecting.value = false
  }
}
</script>

<template>
  <UModal
    v-model:open="isOpen"
    :dismissible="!isSelecting"
    :title="t('dashboard.recurringExpense')"
    :description="t('dashboard.recurringPickGroup')"
  >
    <template #body>
      <UiLoadingSpinner
        v-if="isLoadingGroups"
        :text="t('common.loading')"
      />
      <UiEmptyState
        v-else-if="loadError"
        icon="i-lucide-circle-alert"
        :title="t('dashboard.recurringFailed')"
      />
      <UiEmptyState
        v-else-if="groups.length === 0"
        icon="i-lucide-users"
        :title="t('dashboard.noGroupsTitle')"
        :subtitle="t('dashboard.noGroupsSubtitle')"
      />
      <div
        v-else
        class="space-y-2"
      >
        <UButton
          v-for="group in groups"
          :key="group.id"
          variant="outline"
          color="neutral"
          size="lg"
          block
          class="justify-start"
          :loading="isSelecting"
          :disabled="isSelecting"
          icon="i-lucide-users"
          @click="onSelectGroup(group)"
        >
          <span class="flex-1 min-w-0 truncate text-left">{{ group.name }}</span>
        </UButton>
      </div>
    </template>
  </UModal>
</template>
