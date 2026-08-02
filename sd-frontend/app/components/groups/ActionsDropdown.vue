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

<script setup lang="ts">
import type { Group } from '~/types/domain'

const { t } = useI18n()

interface Props {
  group?: Group | null
  isExporting?: boolean
  isDeleting?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  group: null,
  isExporting: false,
  isDeleting: false,
})

const emit = defineEmits<{
  export: []
  delete: []
}>()

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
    label: t('groups.inviteUsers'),
    icon: 'i-lucide-user-plus',
    onSelect: navigateToInvite,
  },
  {
    label: t('groups.importFile'),
    icon: 'i-lucide-upload',
    onSelect: navigateToImports,
  },
  {
    label: t('groups.exportToCsv'),
    icon: 'i-lucide-download',
    onSelect: handleExport,
    disabled: props.isExporting,
  },
  {
    type: 'separator',
  },
  {
    label: t('groups.editGroup'),
    icon: 'i-lucide-edit-2',
    color: 'info',
    onSelect: navigateToEdit,
  },
  {
    type: 'separator',
  },
  {
    label: t('groups.deleteGroup'),
    icon: 'i-lucide-trash-2',
    color: 'error',
    disabled: props.isDeleting,
    onSelect: handleDelete,
  },
])
</script>
