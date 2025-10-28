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
          @export="onExport"
        />
        <UiConfirmDialog
          title="Delete Group"
          :message="`Are you sure you want to delete the group '${group?.name}'?`"
          subtitle="This action cannot be undone and will remove all associated data."
          confirm-text="Delete Group"
          confirm-color="error"
          icon="i-lucide-trash-2"
          icon-color-class="text-error-500"
          :is-processing="isDeletingGroup"
          @confirm="deleteGroup"
        >
          <template #button>
            <UButton
              icon="i-lucide-trash-2"
              color="error"
              variant="soft"
              size="sm"
            />
          </template>
        </UiConfirmDialog>
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

const isDeletingGroup = ref(false)

const onExport = () => {
  emit('export')
}

const deleteGroup = async () => {
  if (!props.group) return

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
