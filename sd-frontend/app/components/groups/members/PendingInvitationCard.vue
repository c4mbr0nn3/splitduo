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
          {{ $t('members.invited', { date: formatDate(invitation.invitedAt) }) }}
        </p>
      </div>
      <div class="flex items-center justify-between sm:justify-start gap-3 shrink-0">
        <UBadge
          variant="soft"
          color="warning"
          :label="$t('members.pending')"
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

<script setup lang="ts">
import type { Invitation } from '~/types/domain'
import { formatDate } from '~/utils/date'

interface Props {
  invitation: Invitation
  invitationLoading?: boolean
}
withDefaults(defineProps<Props>(), {
  invitationLoading: false,
})

defineEmits<{
  resend: [invitation: Invitation]
  revoke: [invitation: Invitation]
}>()
</script>
