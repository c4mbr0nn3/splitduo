<template>
  <div class="space-y-2">
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <h1 class="text-xl font-semibold text-highlighted truncate max-w-[16rem] sm:max-w-none">
          {{ group?.name || 'Group Details' }}
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
        :label="`${group.memberCount} member${group.memberCount === 1 ? '' : 's'}`"
        @click="navigateTo(`/groups/${group.id}/members`)"
      />

      <template v-if="group?.useAliases">
        <UButton
          v-if="group?.aliasSetupFinalized && aliasCount !== null"
          variant="soft"
          color="neutral"
          icon="i-lucide-layers"
          size="xs"
          :label="`${aliasCount} alias${aliasCount === 1 ? '' : 'es'}`"
          @click="navigateTo(`/groups/${group.id}/members`)"
        />
        <UBadge
          v-else
          color="warning"
          variant="soft"
          icon="i-lucide-alert-triangle"
          label="Alias setup pending"
        />
      </template>
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  group: {
    type: Object,
    default: null,
  },
  aliasCount: {
    type: Number,
    default: null,
  },
  isExporting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['export'])

const { deleteGroup: deleteGroupAPI } = useGroups()
const modal = useModal()

const isDeletingGroup = ref(false)

const onExport = () => {
  emit('export')
}

const confirmDeleteGroup = async () => {
  if (!props.group) return

  const confirmed = await modal.error({
    title: 'Delete Group',
    subtitle: 'This action cannot be undone.',
    content: `The group '${props.group.name}' will be permanently deleted. Are you sure you want to delete this group?`,
    confirmText: 'Delete Group',
    cancelText: 'Cancel',
  })

  if (confirmed) {
    await deleteGroup()
  }
}

const deleteGroup = async () => {
  isDeletingGroup.value = true
  try {
    await deleteGroupAPI(props.group.id)
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
