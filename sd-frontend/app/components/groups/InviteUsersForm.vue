<template>
  <div class="space-y-4">
    <UCard
      variant="soft"
      :ui="{ body: 'p-1' }"
    >
      <UCommandPalette
        v-model="selectedUsers"
        multiple
        :groups="commandGroups"
        placeholder="Search users by name or email..."
        :fuse="{
          fuseOptions: {
            includeMatches: true,
            threshold: 0.3,
            keys: ['label', 'suffix'],
          },
        }"
      />
    </UCard>

    <div class="flex space-x-3 pt-4">
      <UButton
        type="button"
        label="Back"
        variant="outline"
        size="lg"
        class="flex-1"
        @click="emit('cancel')"
      />
      <UButton
        color="primary"
        size="lg"
        :disabled="!selectedUsers.length"
        :loading="isProcessing"
        class="flex-1"
        @click="onInviteUsers"
      >
        Invite {{ selectedUsers.length }} User{{ selectedUsers.length !== 1 ? 's' : '' }}
      </UButton>
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  groupId: {
    type: String,
    required: true,
  },
  groupMembers: {
    type: Array,
    default: null,
  },
})

const emit = defineEmits(['success', 'cancel'])

const { fetchUsers, users } = useUsers()
const { addGroupMember, fetchGroupMembers } = useGroups()

const selectedUsers = ref([])
const isProcessing = ref(false)
const members = ref([])

// Watch for changes to groupMembers prop
watch(() => props.groupMembers, (newMembers) => {
  if (newMembers) {
    members.value = newMembers
  }
}, { immediate: true })

const availableUsers = computed(() => {
  if (!users.value) return []

  const memberIds = new Set(members.value.map(member => member.id))
  return users.value.filter(user => !memberIds.has(user.id))
})

const commandGroups = computed(() => {
  if (!availableUsers.value.length) {
    return [{
      id: 'no-users',
      label: 'No Users',
      items: [{
        id: 'no-users',
        label: 'No available users found',
        disabled: true,
      }],
    }]
  }

  return [{
    id: 'users',
    label: 'Available Users',
    items: availableUsers.value.map(user => ({
      id: user.id,
      label: user.fullName || `${user.firstName} ${user.lastName || ''}`.trim(),
      suffix: user.email,
      user,
    })),
  }]
})

const onInviteUsers = async () => {
  if (!selectedUsers.value.length) return

  isProcessing.value = true
  try {
    const invitePromises = selectedUsers.value.map(({ user }) =>
      addGroupMember(props.groupId, {
        userEmail: user.email,
        role: 'member',
      }),
    )

    await Promise.all(invitePromises)

    emit('success', selectedUsers.value)
    selectedUsers.value = []
  }
  catch (error) {
    console.error('Failed to invite users:', error)
  }
  finally {
    isProcessing.value = false
  }
}

onMounted(async () => {
  try {
    // Fetch all users
    await fetchUsers()

    // Fetch group members if not provided
    if (!props.groupMembers) {
      const data = await fetchGroupMembers(props.groupId)
      members.value = data.map(item => item.user)
    }
  }
  catch (error) {
    console.error('Failed to load data:', error)
  }
})
</script>
