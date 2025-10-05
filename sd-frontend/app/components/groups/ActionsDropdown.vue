<template>
  <div class="flex items-center gap-1">
    <!-- Add User Modal -->
    <GroupsAddUserModal
      v-if="group"
      ref="addUserModalRef"
      :group-id="group.id"
      :group-members="groupMembers"
      @user-added="onUserAdded"
    />

    <!-- Delete Confirmation Dialog -->
    <UiConfirmDialog
      ref="deleteDialogRef"
      title="Delete Group"
      :message="`Are you sure you want to delete the group '${group?.name}'?`"
      subtitle="This action cannot be undone and will remove all associated data."
      confirm-text="Delete Group"
      confirm-color="error"
      icon="i-lucide-trash-2"
      icon-color-class="text-error-500"
      :is-processing="isDeleting"
      @confirm="onDeleteConfirmed"
    />

    <!-- Actions Dropdown -->
    <UiButtonDropdown
      label="Actions"
      :items="dropdownItems"
      size="sm"
      variant="soft"
    />
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
  isDeleting: {
    type: Boolean,
    default: false,
  },
  isExporting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['user-added', 'delete-confirmed', 'export'])

const addUserModalRef = ref(null)
const deleteDialogRef = ref(null)

const navigateToEdit = () => {
  if (!props.group?.id) return
  navigateTo(`/groups/${props.group.id}/edit/`)
}

const navigateToImports = () => {
  if (!props.group?.id) return
  navigateTo(`/groups/${props.group.id}/imports`)
}

const openAddUserModal = () => {
  // The modal should open when its button slot is clicked
  // This is handled by the GroupsAddUserModal component itself
}

const openDeleteDialog = () => {
  // The dialog should open when its button slot is clicked
  // This is handled by the UiConfirmDialog component itself
}

const onUserAdded = (users) => {
  emit('user-added', users)
}

const onDeleteConfirmed = () => {
  emit('delete-confirmed')
}

const handleExport = () => {
  emit('export')
}

const dropdownItems = computed(() => [
  {
    label: 'Add User',
    icon: 'i-lucide-user-plus',
    onSelect: () => {
      // Trigger the modal programmatically
      addUserModalRef.value?.$el?.querySelector('button')?.click()
    },
  },
  {
    label: 'Import File',
    icon: 'i-lucide-upload',
    onSelect: navigateToImports,
  },
  {
    label: 'Export to CSV',
    icon: 'i-lucide-download',
    onSelect: handleExport,
    disabled: props.isExporting,
  },
  {
    type: 'separator',
  },
  {
    label: 'Edit Group',
    icon: 'i-lucide-edit-2',
    color: 'info',
    onSelect: navigateToEdit,
  },
  {
    label: 'Delete Group',
    icon: 'i-lucide-trash-2',
    color: 'error',
    onSelect: () => {
      // Trigger the delete dialog programmatically
      deleteDialogRef.value?.$el?.querySelector('button')?.click()
    },
  },
])
</script>
