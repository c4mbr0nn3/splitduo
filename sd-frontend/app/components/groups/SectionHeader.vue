<template>
  <div class="grid grid-cols-1 items-start">
    <div class="flex items-center justify-between mb-2">
      <div class="flex items-center gap-2">
        <UBadge
          v-if="group?.memberCount"
          variant="soft"
          icon="i-lucide-users"
          :label="`${group.memberCount}`"
        />
      </div>
      <GroupsActionsDropdown
        :group="group"
        :group-members="groupMembers"
        :is-deleting="isDeletingGroup"
        :is-exporting="isExporting"
        @user-added="onUserAdded"
        @delete-confirmed="deleteGroup"
        @export="onExport"
      />
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
  groupMembers: {
    type: Array,
    default: () => [],
  },
  isExporting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['user-added', 'export'])

const { deleteGroup: deleteGroupAPI } = useGroups()

const isDeletingGroup = ref(false)

const onUserAdded = (users) => {
  emit('user-added', users)
}

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
