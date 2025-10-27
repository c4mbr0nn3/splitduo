<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <div class="space-y-4">
          <div>
            <h1 class="text-xl font-bold text-primary">
              Invite Users
            </h1>
            <p
              v-if="group"
              class="text-sm text-muted mt-1"
            >
              {{ group.name }}
            </p>
          </div>
        </div>
      </template>

      <UiLoadingSpinner
        v-if="isLoading"
        text="Loading group details..."
      />

      <GroupsInviteUsersForm
        v-else-if="group"
        :group-id="groupId"
        :group-members="groupMembers"
        @success="onSuccess"
        @cancel="onCancel"
      />
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup, fetchGroupMembers, isLoading } = useGroups()

const group = computed(() => currentGroup.value)
const groupMembers = ref([])

const onCancel = () => {
  navigateTo(`/groups/${groupId}`)
}

const onSuccess = async (_users) => {
  // Navigate to members page after successfully adding users
  await navigateTo(`/groups/${groupId}/members`)
}

const loadGroupMembers = async () => {
  try {
    const data = await fetchGroupMembers(groupId)
    groupMembers.value = data.map(item => item.user)
  }
  catch (error) {
    console.error('Failed to load group members:', error)
    groupMembers.value = []
  }
}

onMounted(async () => {
  if (groupId) {
    await Promise.all([
      fetchGroup(groupId),
      loadGroupMembers(),
    ])
  }
})

useHead({
  title: computed(() => `Invite Users - ${group.value?.name || 'Group'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
