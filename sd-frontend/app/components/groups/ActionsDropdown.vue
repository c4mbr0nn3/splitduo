<template>
  <UiButtonDropdown
    :items="dropdownItems"
    icon-only
    dropdown-icon="i-lucide-ellipsis-vertical"
    size="sm"
    variant="outline"
    color="neutral"
  />
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
  isDeleting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['export', 'delete'])

const navigateToEdit = () => {
  if (!props.group?.id) return
  navigateTo(`/groups/${props.group.id}/edit/`)
}

const navigateToImports = () => {
  if (!props.group?.id) return
  navigateTo(`/groups/${props.group.id}/imports`)
}

const navigateToInvite = () => {
  if (!props.group?.id) return
  navigateTo(`/groups/${props.group.id}/invite`)
}

const handleExport = () => {
  emit('export')
}

const handleDelete = () => {
  emit('delete')
}

const dropdownItems = computed(() => [
  {
    label: 'Invite Users',
    icon: 'i-lucide-user-plus',
    onSelect: navigateToInvite,
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
    type: 'separator',
  },
  {
    label: 'Delete Group',
    icon: 'i-lucide-trash-2',
    color: 'error',
    disabled: props.isDeleting,
    onSelect: handleDelete,
  },
])
</script>
