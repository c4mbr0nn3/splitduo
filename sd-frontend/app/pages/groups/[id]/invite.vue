<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <UiCardHeader
          title="Invite User"
          :subtitle="group?.name"
          :back-to="`/groups/${groupId}/members`"
        />
      </template>

      <UiLoadingSpinner
        v-if="groupLoading"
        text="Loading group details..."
      />

      <GroupsInviteUsersForm
        v-else-if="group"
        :group-id="groupId"
        @success="onSuccess"
      />
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup, isLoading: groupLoading } = useGroups()

const group = computed(() => currentGroup.value)

const onSuccess = async () => {
  await navigateTo(`/groups/${groupId}/members`)
}

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
  }
})

useHead({
  title: computed(() => `Invite User - ${group.value?.name || 'Group'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
