<template>
  <UCard
    class="sd-surface sd-surface-hover"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <div class="flex items-start justify-between">
      <NuxtLink
        :to="`/admin/users/${user.id}/edit`"
        class="block flex-1 min-w-0"
      >
        <div class="space-y-4">
          <!-- User Header -->
          <div class="flex items-start">
            <div class="flex items-center gap-3 min-w-0">
              <UAvatar
                :alt="user.fullName || `${user.firstName} ${user.lastName || ''}`.trim()"
                icon="i-lucide-user"
                size="lg"
                class="shrink-0"
              />

              <div class="min-w-0">
                <h3 class="font-semibold text-primary text-lg truncate">
                  {{ user.fullName || `${user.firstName} ${user.lastName || ''}`.trim() }}
                </h3>
                <p class="text-sm text-muted truncate">
                  {{ user.email }}
                </p>
              </div>
            </div>
          </div>

          <!-- User Role Badge -->
          <div class="flex flex-wrap items-center justify-between gap-2">
            <UBadge
              :color="user.globalRoleId == 2 ? 'primary' : 'secondary'"
              variant="soft"
              :icon="user.globalRoleId == 2 ? 'i-lucide-crown' : 'i-lucide-user'"
            >
              {{ user.globalRoleId == 2 ? 'Admin' : 'User' }}
            </UBadge>
          </div>

          <!-- User Metadata -->
          <USeparator />
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-1 text-xs text-muted">
            <span>Created {{ formatDate(user.createdAt) }}</span>
            <span>Updated {{ formatDate(user.updatedAt) }}</span>
          </div>
        </div>
      </NuxtLink>
      <div class="shrink-0 ml-2">
        <span @click.stop>
          <UiButtonDropdown
            icon-only
            dropdown-icon="i-lucide-ellipsis-vertical"
            size="md"
            square
            variant="ghost"
            color="neutral"
            :items="dropdownItems"
            :disabled="isSameUser || isDeleting || isRevokingTokens || isChangingRole"
          />
        </span>
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
  isRevokingTokens: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['edit', 'revoke-tokens', 'delete', 'refresh'])

const { user: authUser } = useAuth()
const modal = useModal()
const { changeUserRole } = useUsers()

const isSameUser = computed(() => {
  return authUser.value?.id === props.user.id
})

const isChangingRole = ref(false)

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

const confirmRevokeTokens = async () => {
  const userName = props.user.fullName || `${props.user.firstName} ${props.user.lastName || ''}`.trim()

  const confirmed = await modal.warning({
    title: 'Revoke User Tokens',
    subtitle: 'This will invalidate all active sessions',
    content: `All active sessions for '${userName}' will be terminated and the user will need to log in again. Are you sure you want to revoke all tokens?`,
    confirmText: 'Revoke Tokens',
    cancelText: 'Cancel',
  })

  if (confirmed) {
    emit('revoke-tokens', props.user)
  }
}

const confirmPromote = async () => {
  const firstName = props.user.firstName || props.user.fullName || ''

  const confirmed = await modal.warning({
    title: 'Promote to System Admin',
    content: `${firstName} will have full access to manage all users and platform settings.`,
    confirmText: 'Promote',
    cancelText: 'Cancel',
  })

  if (confirmed) {
    isChangingRole.value = true
    try {
      await changeUserRole(props.user.id, UserRole.SYSTEM_ADMIN)
      emit('refresh')
    }
    catch {
      // Error shown via toast
    }
    finally {
      isChangingRole.value = false
    }
  }
}

const confirmDemote = async () => {
  const firstName = props.user.firstName || props.user.fullName || ''

  const confirmed = await modal.warning({
    title: 'Demote to Regular User',
    content: `${firstName} will no longer have access to the admin panel or manage platform users.`,
    confirmText: 'Demote',
    cancelText: 'Cancel',
  })

  if (confirmed) {
    isChangingRole.value = true
    try {
      await changeUserRole(props.user.id, UserRole.BASE_USER)
      emit('refresh')
    }
    catch {
      // Error shown via toast
    }
    finally {
      isChangingRole.value = false
    }
  }
}

const onEdit = () => {
  navigateTo(`/admin/users/${props.user.id}/edit`)
}

const dropdownItems = computed(() => {
  const items = [
    {
      label: 'Edit',
      icon: 'i-lucide-edit-2',
      color: 'info',
      onSelect: onEdit,
    },
    {
      label: 'Revoke Tokens',
      icon: 'i-lucide-rotate-ccw-key',
      color: 'warning',
      onSelect: confirmRevokeTokens,
    },
    {
      type: 'separator',
    },
    {
      label: 'Delete',
      icon: 'i-lucide-trash-2',
      color: 'error',
      onSelect: confirmDeleteUser,
    },
  ]

  // Promote/demote items for role management
  if (props.user.globalRoleId == UserRole.BASE_USER) {
    items.unshift({
      label: 'Promote to Admin',
      icon: 'i-lucide-arrow-up-circle',
      color: 'success',
      onSelect: confirmPromote,
    })
  }
  else if (props.user.globalRoleId == UserRole.SYSTEM_ADMIN && !isSameUser.value) {
    items.unshift({
      label: 'Demote to User',
      icon: 'i-lucide-arrow-down-circle',
      color: 'warning',
      onSelect: confirmDemote,
    })
  }

  return items
})
</script>
