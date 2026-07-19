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
})

const emit = defineEmits(['export'])

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
])
</script>
