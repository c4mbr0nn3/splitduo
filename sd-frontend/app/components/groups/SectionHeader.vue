<template>
  <div class="space-y-2">
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <h1 class="text-xl font-semibold text-highlighted truncate max-w-[16rem] sm:max-w-none">
          {{ group?.name || $t('groups.groupDetails') }}
        </h1>
      </div>
      <div class="flex items-center gap-2 shrink-0">
        <GroupsActionsDropdown
          :group="group"
          :is-exporting="isExporting"
          :is-deleting="isDeletingGroup"
          @export="onExport"
          @delete="confirmDeleteGroup"
        />
      </div>
    </div>

    <p
      v-if="group?.description"
      class="text-sm text-muted"
    >
      {{ group.description }}
    </p>

    <div class="flex flex-wrap items-center gap-2 pt-1">
      <UButton
        v-if="group?.memberCount"
        variant="soft"
        icon="i-lucide-users"
        size="xs"
        :label="t('groups.memberCount', { count: Number(group.memberCount) }, Number(group.memberCount))"
        @click="navigateTo(`/groups/${group.id}/members`)"
      />

      <template v-if="group?.useAliases">
        <UButton
          v-if="group?.aliasSetupFinalized && aliasCount !== null"
          variant="soft"
          color="neutral"
          icon="i-lucide-layers"
          size="xs"
          :label="$t('groups.aliasCount', { count: aliasCount }, aliasCount)"
          @click="navigateTo(`/groups/${group.id}/members`)"
        />
        <UBadge
          v-else
          color="warning"
          variant="soft"
          icon="i-lucide-alert-triangle"
          :label="$t('groups.aliasPending')"
        />
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Group } from '~/types/domain'

const { t } = useI18n()

interface Props {
  group?: Group | null
  aliasCount?: number | null
  isExporting?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  group: null,
  aliasCount: null,
  isExporting: false,
})

const emit = defineEmits<{
  export: []
}>()

const { deleteGroup: deleteGroupAPI } = useGroups()
const modal = useModal()

const isDeletingGroup = ref(false)

const onExport = () => {
  emit('export')
}

const confirmDeleteGroup = async () => {
  if (!props.group) return

  const confirmed = await modal.error({
    title: t('groups.deleteTitle'),
    subtitle: t('groups.deleteConfirm'),
    content: t('groups.deleteContent', { name: props.group.name }),
    confirmText: t('groups.deleteButton'),
    cancelText: t('common.cancel'),
  })

  if (confirmed) {
    await deleteGroup()
  }
}

const deleteGroup = async () => {
  isDeletingGroup.value = true
  try {
    await deleteGroupAPI(props.group!.id)
    await navigateTo('/groups')
  }
  catch (error) {
    console.error('Failed to delete group:', error)
  }
  finally {
    isDeletingGroup.value = false
  }
}
</script>
