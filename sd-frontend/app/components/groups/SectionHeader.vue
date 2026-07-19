<template>
  <div class="grid grid-cols-1 items-start">
    <div class="flex items-center justify-between mb-2">
      <div class="flex items-center gap-2">
        <UButton
          v-if="group?.memberCount"
          variant="soft"
          icon="i-lucide-users"
          size="sm"
          :label="`${group.memberCount}`"
          @click="navigateTo(`/groups/${group.id}/members`)"
        />
      </div>
      <div class="flex items-center gap-2">
        <GroupsActionsDropdown
          :group="group"
          :is-exporting="isExporting"
          :is-deleting="isDeletingGroup"
          @export="onExport"
          @delete="confirmDeleteGroup"
        />
      </div>
    </div>
    <div class="flex">
      <div class="flex min-w-0">
        <h1 class="text-xl font-bold text-primary truncate">
          {{ group?.name || 'Group Details' }}
        </h1>
      </div>
    </div>
    <p
      v-if="group?.description"
      class="text-sm text-muted mt-1"
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
