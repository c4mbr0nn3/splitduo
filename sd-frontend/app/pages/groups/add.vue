<template>
  <GroupsGroupForm
    title="Create Group"
    submit-label="Create"
    :loading="isLoading"
    @submit="onSubmit"
  />
</template>

<script setup>
const { createGroup, isLoading } = useGroups()

async function onSubmit(formData) {
  try {
    const group = await createGroup(formData)
    if (group) {
      navigateTo(`/groups/${group.id}`)
    }
  }
  catch (err) {
    console.error('Error creating group:', err)
  }
}

useHead({
  title: 'Create Group',
})

definePageMeta({
  middleware: 'auth',
})
</script>
