<template>
  <div class="space-y-6">
    <UCard
      variant="outline"
      class="sd-surface"
    >
      <div class="space-y-4">
        <div class="flex items-center gap-2 min-w-0">
          <UIcon
            name="i-lucide-users"
            class="size-5 text-secondary shrink-0"
          />
          <h4 class="text-base font-semibold text-highlighted">
            Members
          </h4>
        </div>

        <div class="space-y-1">
          <GroupsMembersRow
            v-for="member in members"
            :key="member.id"
            :member="member"
          >
            <template
              v-if="isGroupAdmin && member.id !== currentUserId"
              #actions
            >
              <span @click.stop>
                <UiButtonDropdown
                  :items="getRoleItems(member)"
                  icon-only
                  dropdown-icon="i-lucide-ellipsis-vertical"
                  size="sm"
                  square
                  variant="ghost"
                  color="neutral"
                />
              </span>
            </template>
          </GroupsMembersRow>
        </div>
      </div>
    </UCard>

    <div
      v-if="isGroupAdmin && pendingInvitations.length"
      class="space-y-2"
    >
      <USeparator />
      <h3 class="text-sm font-medium text-muted">
        Pending Invitations
      </h3>
      <GroupsMembersPendingInvitationCard
        v-for="invitation in pendingInvitations"
        :key="invitation.id"
        :invitation="invitation"
        :invitation-loading="invitationLoading"
        @resend="$emit('resend', $event)"
        @revoke="$emit('revoke', $event)"
      />
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  members: {
    type: Array,
    required: true,
  },
  isGroupAdmin: {
    type: Boolean,
    default: false,
  },
  currentUserId: {
    type: String,
    default: '',
  },
  groupId: {
    type: String,
    default: '',
  },
  pendingInvitations: {
    type: Array,
    default: () => [],
  },
  invitationLoading: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['resend', 'revoke', 'refresh'])

const { changeMemberRole } = useGroups()
const modal = useModal()

const getRoleItems = (member) => {
  const items = []

  if (member.role === 'member') {
    items.push({
      label: 'Promote to Admin',
      icon: 'i-lucide-arrow-up-circle',
      color: 'success',
      onSelect: () => confirmRoleChange(member, 'admin'),
    })
  }
  else if (member.role === 'admin') {
    items.push({
      label: 'Demote to Member',
      icon: 'i-lucide-arrow-down-circle',
      color: 'warning',
      onSelect: () => confirmRoleChange(member, 'member'),
    })
  }

  return items
}

const confirmRoleChange = async (member, newRole) => {
  const isPromote = newRole === 'admin'
  const firstName = member.firstName || member.fullName || ''

  const confirmed = await modal.warning({
    title: isPromote ? 'Promote to Admin' : 'Demote to Member',
    content: isPromote
      ? `${firstName} will be able to edit group settings, manage members, and delete this group.`
      : `${firstName} will no longer be able to manage this group.`,
    confirmText: isPromote ? 'Promote' : 'Demote',
    cancelText: 'Cancel',
  })

  if (!confirmed) return

  try {
    await changeMemberRole(props.groupId, member.id, newRole)
    emit('refresh')
  }
  catch {
    // Error shown via toast
  }
}
</script>
