<template>
  <UCard variant="outline">
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
      <div class="min-w-0">
        <p
          class="font-semibold break-all sm:truncate"
          :title="invitation.email"
        >
          {{ invitation.email }}
        </p>
        <p class="text-sm text-muted">
          Invited {{ formatDate(invitation.invitedAt) }}
        </p>
      </div>
      <div class="flex items-center justify-between sm:justify-start gap-3 shrink-0">
        <UBadge
          variant="soft"
          color="warning"
          label="Pending"
          icon="i-lucide-clock"
        />
        <div class="flex items-center gap-2 sm:gap-3">
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
    </div>
  </UCard>
</template>

<script setup>
import { formatDate } from '~/utils/date'

defineProps({
  invitation: {
    type: Object,
    required: true,
  },
  invitationLoading: {
    type: Boolean,
    default: false,
  },
})

defineEmits(['resend', 'revoke'])
</script>
