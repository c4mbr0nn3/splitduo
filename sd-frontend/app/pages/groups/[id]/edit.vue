<template>
  <GroupsGroupForm
    :title="$t('groups.editTitle')"
    :submit-label="$t('groups.editSubmit')"
    :initial-data="initialData"
    :loading="isLoading"
    disabled-aliases
    @submit="onSubmit"
    @cancel="onCancel"
  />
</template>

<script setup lang="ts">
interface GroupFormData {
  name: string
  description: string
  useAliases: boolean
}

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)

const { currentGroup, fetchGroup, updateGroup, isLoading } = useGroups()

const initialData = computed<GroupFormData>(() => ({
  name: currentGroup.value?.name || '',
  description: currentGroup.value?.description || '',
  useAliases: currentGroup.value?.useAliases || false,
}))

async function onSubmit(formData: GroupFormData) {
  try {
    const updatedGroup = await updateGroup(groupId, formData)
    if (updatedGroup) {
      navigateTo(`/groups/${groupId}`)
    }
  }
  catch (err: unknown) {
    console.error('Error updating group:', err)
  }
}

const { goBack } = useSmartBack(`/groups/${groupId}`)

function onCancel() {
  goBack()
}

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
  }
})

useHead({
  title: computed(() => `${t('groups.editTitle')} - ${currentGroup.value?.name || ''}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
