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
      <UCard
        v-for="invitation in pendingInvitations"
        :key="invitation.id"
        variant="outline"
      >
        <div class="flex items-center justify-between">
          <div>
            <p class="font-semibold">
              {{ invitation.email }}
            </p>
            <p class="text-sm text-muted">
              Invited {{ formatDate(invitation.invitedAt) }}
            </p>
          </div>
          <div class="flex items-center gap-3">
            <UBadge
              variant="soft"
              color="warning"
              label="Pending"
              icon="i-lucide-clock"
            />
            <UButton
              icon="i-lucide-refresh-cw"
              variant="ghost"
              size="sm"
              square
              :loading="invitationLoading"
              @click="$emit('resend', invitation)"
            />
            <UButton
              icon="i-lucide-x"
              variant="ghost"
              color="error"
              size="sm"
              square
              :loading="invitationLoading"
              @click="$emit('revoke', invitation)"
            />
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup>
import { formatDate } from '~/utils/date'

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
