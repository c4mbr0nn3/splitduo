<template>
  <UCard
    class="sd-surface sd-surface-hover"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <div class="space-y-4">
      <!-- Email Header -->
      <div class="flex items-start justify-between gap-3">
        <div class="flex items-center gap-3 min-w-0">
          <UIcon
            name="i-lucide-mail"
            class="text-muted shrink-0"
          />
          <span class="font-semibold text-primary truncate">
            {{ pending.email }}
          </span>
        </div>

        <UBadge
          variant="soft"
          color="warning"
          icon="i-lucide-clock"
        >
          Pending
        </UBadge>
      </div>

      <USeparator />

      <!-- Groups -->
      <div class="space-y-2">
        <p class="text-xs uppercase tracking-wide font-medium text-muted">
          Invited to:
        </p>
        <div class="flex flex-wrap gap-1.5">
          <UBadge
            v-for="group in pending.groups"
            :key="group.id"
            variant="subtle"
            color="neutral"
            size="sm"
            :label="group.name"
          />
        </div>
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { PendingUser } from '~/types/domain'

interface Props {
  pending: PendingUser
}
defineProps<Props>()
</script>
