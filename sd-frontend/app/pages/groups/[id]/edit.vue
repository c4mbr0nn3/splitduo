<template>
  <GroupsGroupForm
    title="Edit Group"
    submit-label="Update"
    :initial-data="initialData"
    :loading="isLoading"
    @submit="onSubmit"
    @cancel="onCancel"
  />
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup, updateGroup, isLoading } = useGroups()

const initialData = computed(() => ({
  name: currentGroup.value?.name || '',
  description: currentGroup.value?.description || '',
}))

async function onSubmit(formData) {
  try {
    const updatedGroup = await updateGroup(groupId, formData)
    if (updatedGroup) {
      navigateTo(`/groups/${groupId}`)
    }
  }
  catch (err) {
    console.error('Error updating group:', err)
  }
}

function onCancel() {
  navigateTo(`/groups/${groupId}`)
}

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
  }
})

useHead({
  title: computed(() => `Edit ${currentGroup.value?.name || 'Group'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
