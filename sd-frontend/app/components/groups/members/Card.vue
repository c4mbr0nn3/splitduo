<template>
  <UCard variant="outline">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <div>
          <p class="font-semibold">
            {{ member.fullName || `${member.firstName} ${member.lastName || ''}`.trim() }}
          </p>
          <p class="text-sm text-muted">
            {{ member.email }}
          </p>
        </div>
      </div>
      <UBadge
        variant="soft"
        :color="member.role === 'admin' ? 'success' : 'primary'"
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
