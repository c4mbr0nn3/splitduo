<template>
  <UCard variant="outline">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3 min-w-0">
        <UAvatar
          :alt="member.fullName || `${member.firstName} ${member.lastName || ''}`.trim()"
          icon="i-lucide-user"
          size="md"
          class="shrink-0"
        />
        <div class="min-w-0">
          <p class="font-semibold text-primary truncate">
            {{ member.fullName || `${member.firstName} ${member.lastName || ''}`.trim() }}
          </p>
          <p class="text-sm text-muted truncate">
            {{ member.email }}
          </p>
          <p
            v-if="member.aliasName"
            class="text-sm text-secondary truncate"
          >
            {{ member.aliasName }}
          </p>
        </div>
      </div>
      <UBadge
        variant="soft"
        :color="member.role === 'admin' ? 'primary' : 'secondary'"
        :label="member.role"
        :icon="member.role === 'admin' ? 'i-lucide-crown' : 'i-lucide-user'"
        class="capitalize"
      />
    </div>
  </UCard>
</template>

<script setup lang="ts">
interface FlattenedMember {
  id: string
  firstName?: string
  lastName?: string | null
  email?: string
  fullName?: string | null
  role: string
  aliasName?: string
}

interface Props {
  member: FlattenedMember
}
defineProps<Props>()
</script>
