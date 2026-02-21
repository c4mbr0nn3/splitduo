<template>
  <UCard variant="outline">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <UAvatar
          :alt="member.fullName || `${member.firstName} ${member.lastName || ''}`.trim()"
          icon="i-lucide-user"
          size="md"
        />
        <div>
          <p class="font-semibold text-primary">
            {{ member.fullName || `${member.firstName} ${member.lastName || ''}`.trim() }}
          </p>
          <p class="text-sm text-muted">
            {{ member.email }}
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

<script setup>
defineProps({
  member: {
    type: Object,
    required: true,
  },
})
</script>
