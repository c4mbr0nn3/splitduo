<template>
  <UCard
    class="hover:border-primary/50 transition-colors"
    variant="outline"
  >
    <div class="space-y-4">
      <!-- User Header -->
      <div class="flex items-start justify-between">
        <div class="flex items-center gap-3">
          <UAvatar
            :alt="user.fullName || `${user.firstName} ${user.lastName || ''}`.trim()"
            icon="i-lucide-user"
            size="lg"
          />

          <div>
            <h3 class="font-semibold text-primary text-lg">
              {{ user.fullName || `${user.firstName} ${user.lastName || ''}`.trim() }}
            </h3>
            <p class="text-sm text-muted">
              {{ user.email }}
            </p>
          </div>
        </div>
        <div class="flex items-center gap-1">
          <UButton
            variant="ghost"
            color="info"
            size="sm"
            icon="i-lucide-edit-2"
            @click="onEdit"
          />
          <UButton
            variant="ghost"
            :color="isSameUser ? '' : 'warning'"
            size="sm"
            icon="i-lucide-rotate-ccw-key"
            :disabled="isSameUser"
            @click="onRevokeTokens"
          />
          <UButton
            variant="ghost"
            :color="isSameUser ? '' : 'error'"
            size="sm"
            icon="i-lucide-trash-2"
            :disabled="isSameUser"
            :loading="isDeleting"
            @click.stop="confirmDeleteUser"
          />
        </div>
      </div>

      <!-- User Role Badge -->
      <div class="flex items-center justify-between">
        <UBadge
          :color="user.globalRoleId == 2 ? 'success' : 'neutral'"
          variant="soft"
          :icon="user.globalRoleId == 2 ? 'i-lucide-crown' : 'i-lucide-user'"
        >
          {{ user.globalRoleId == 2 ? 'Admin' : 'User' }}
        </UBadge>
      </div>

      <!-- User Metadata -->
      <USeparator />
      <div class="flex items-center justify-between text-xs text-muted">
        <span>Created {{ formatDate(user.createdAt) }}</span>
        <span>Updated {{ formatDate(user.updatedAt) }}</span>
      </div>
    </div>
  </UCard>
</template>

<script setup>
const props = defineProps({
  user: {
    type: Object,
    required: true,
  },
  isDeleting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['edit', 'revoke-tokens', 'delete'])

const { user: authUser } = useAuth()
const modal = useModal()

const isSameUser = computed(() => {
  return authUser.value?.id === props.user.id
})

const confirmDeleteUser = async () => {
  const userName = props.user.fullName || `${props.user.firstName} ${props.user.lastName || ''}`.trim()

  const confirmed = await modal.error({
    title: 'Delete User',
    subtitle: 'This action cannot be undone.',
    content: `The user '${userName}' will be permanently deleted. Are you sure you want to delete this user?`,
    confirmText: 'Delete User',
    cancelText: 'Cancel',
  })

  if (confirmed) {
    emit('delete', props.user)
  }
}

const onRevokeTokens = () => {
  emit('revoke-tokens', props.user)
}

const onEdit = () => {
  navigateTo(`/admin/users/${props.user.id}/edit`)
}
</script>
