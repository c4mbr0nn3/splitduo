<template>
  <UModal>
    <slot name="button" />
    <template #title>
      <div class="flex items-center gap-2">
        <UIcon
          name="i-lucide-user-plus"
          class="text-primary"
        />
        <h3 class="text-lg font-semibold">
          Invite User to Group
        </h3>
      </div>
    </template>
    <template #body>
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
    </template>
    <template #footer>
      <div class="flex items-center gap-3 w-full">
        <UButton
          color="primary"
          :disabled="!selectedUsers.length"
          :loading="isProcessing"
          @click="onInviteUsers"
        >
          Invite {{ selectedUsers.length }} User{{ selectedUsers.length !== 1 ? 's' : '' }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup>
const props = defineProps({
  groupId: {
    type: String,
    required: true,
  },
  groupMembers: {
    type: Array,
    default: () => [],
  },
})

const emit = defineEmits(['user-added', 'cancel'])

const { fetchUsers, users } = useUsers()
const { addGroupMember } = useGroups()

const selectedUsers = ref([])
const isProcessing = ref(false)

const availableUsers = computed(() => {
  if (!users.value) return []

  const memberIds = new Set(props.groupMembers.map(member => member.id))
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
    for (const { user } of selectedUsers.value) {
      await addGroupMember(props.groupId, {
        userEmail: user.email,
        role: 'member',
      })
    }

    emit('user-added', selectedUsers.value)
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
    await fetchUsers()
  }
  catch (error) {
    console.error('Failed to load users:', error)
  }
})
</script>
