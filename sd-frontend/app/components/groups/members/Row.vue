<template>
  <div class="flex items-center justify-between gap-3 py-2">
    <div class="flex items-center gap-3 min-w-0">
      <UserAvatar
        :user="member"
        size="sm"
        class="shrink-0"
      />
      <div class="min-w-0">
        <div class="flex items-center gap-1.5 min-w-0">
          <p class="text-sm font-medium text-highlighted truncate">
            {{ member.fullName || `${member.firstName} ${member.lastName || ''}`.trim() }}
          </p>
          <UIcon
            v-if="member.role === 'admin'"
            name="i-lucide-crown"
            class="size-3.5 text-primary shrink-0"
          />
        </div>
        <p class="text-xs text-muted truncate">
          {{ member.email }}
        </p>
      </div>
    </div>

    <div
      v-if="$slots.actions"
      class="flex items-center gap-2 shrink-0"
    >
      <slot name="actions" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface FlattenedMember {
  id: string
  firstName?: string
  lastName?: string | null
  email?: string
  fullName?: string | null
  role: string
}

interface Props {
  member: FlattenedMember
}
defineProps<Props>()
</script>
