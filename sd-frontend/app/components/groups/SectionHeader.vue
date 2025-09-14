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
        <GroupsAddUserModal
          v-if="group"
          :group-id="group.id"
          :group-members="groupMembers"
          @user-added="onUserAdded"
        >
          <template #button>
            <UButton
              variant="ghost"
              color="neutral"
              size="sm"
              icon="i-lucide-user-plus"
            />
          </template>
        </GroupsAddUserModal>
      </div>
      <div class="flex items-center gap-1">
        <UButton
          variant="ghost"
          color="neutral"
          size="sm"
          icon="i-lucide-upload"
          @click="navigateToImports(group?.id)"
        />
        <UButton
          variant="ghost"
          color="info"
          size="sm"
          icon="i-lucide-edit-2"
          @click="navigateToEdit(group?.id)"
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
              variant="ghost"
              color="error"
              size="sm"
              icon="i-lucide-trash-2"
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
  groupMembers: {
    type: Array,
    default: () => [],
  },
})

const emit = defineEmits(['user-added'])

const { deleteGroup: deleteGroupAPI } = useGroups()

const isDeletingGroup = ref(false)

const navigateToEdit = (groupId) => {
  if (!groupId) return
  navigateTo(`/groups/${groupId}/edit/`)
}

const navigateToImports = (groupId) => {
  if (!groupId) return
  navigateTo(`/groups/${groupId}/imports`)
}

const onUserAdded = (users) => {
  emit('user-added', users)
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
