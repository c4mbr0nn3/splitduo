<template>
  <GroupsGroupForm
    :title="$t('groups.createTitle')"
    :submit-label="$t('groups.createSubmit')"
    :loading="isLoading"
    @submit="onSubmit"
    @cancel="onCancel"
  />
</template>

<script setup>
const { t } = useI18n()
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

const { goBack } = useSmartBack('/groups')

function onCancel() {
  goBack()
}

useHead({
  title: computed(() => t('groups.createTitle')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
