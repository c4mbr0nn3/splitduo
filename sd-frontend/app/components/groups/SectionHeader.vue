<template>
  <div class="space-y-2">
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0 flex items-center gap-2 flex-wrap">
        <h1 class="text-xl font-semibold text-highlighted truncate max-w-[16rem] sm:max-w-none">
          {{ group?.name || 'Group Details' }}
        </h1>
        <UButton
          v-if="group?.memberCount"
          variant="soft"
          icon="i-lucide-users"
          size="xs"
          :label="`${group.memberCount}`"
          @click="navigateTo(`/groups/${group.id}/members`)"
        />
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
  </div>
</template>

<script setup>
const props = defineProps({
  group: {
    type: Object,
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
