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
            {{ $t('members.title') }}
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
        {{ $t('members.pendingInvitations') }}
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

<script setup lang="ts">
import type { Invitation } from '~/types/domain'

const { t } = useI18n()

interface FlattenedMember {
  id: string
  firstName?: string
  lastName?: string | null
  email?: string
  fullName?: string | null
  role: string
}

interface Props {
  members: FlattenedMember[]
  isGroupAdmin?: boolean
  currentUserId?: string
  groupId?: string
  pendingInvitations?: Invitation[]
  invitationLoading?: boolean
}
const props = withDefaults(defineProps<Props>(), {
  isGroupAdmin: false,
  currentUserId: '',
  groupId: '',
  pendingInvitations: () => [],
  invitationLoading: false,
})

const emit = defineEmits<{
  resend: [invitation: Invitation]
  revoke: [invitation: Invitation]
  refresh: []
}>()

const { changeMemberRole } = useGroups()
const modal = useModal()

const getRoleItems = (member: FlattenedMember) => {
  const items = []

  if (member.role === 'member') {
    items.push({
      label: t('members.promoteToAdmin'),
      icon: 'i-lucide-arrow-up-circle',
      color: 'success',
      onSelect: () => confirmRoleChange(member, 'admin'),
    })
  }
  else if (member.role === 'admin') {
    items.push({
      label: t('members.demoteToMember'),
      icon: 'i-lucide-arrow-down-circle',
      color: 'warning',
      onSelect: () => confirmRoleChange(member, 'member'),
    })
  }

  return items
}

const confirmRoleChange = async (member: FlattenedMember, newRole: string) => {
  const isPromote = newRole === 'admin'
  const firstName = member.firstName || member.fullName || ''

  const confirmed = await modal.warning({
    title: isPromote ? t('members.promoteConfirmTitle') : t('members.demoteConfirmTitle'),
    content: isPromote
      ? t('members.promoteConfirmContent', { name: firstName })
      : t('members.demoteConfirmContent', { name: firstName }),
    confirmText: isPromote ? t('members.promote') : t('members.demote'),
    cancelText: t('common.cancel'),
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
