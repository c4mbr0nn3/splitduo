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
          />
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
defineProps({
  members: {
    type: Array,
    required: true,
  },
  isGroupAdmin: {
    type: Boolean,
    default: false,
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

defineEmits(['resend', 'revoke'])
</script>
