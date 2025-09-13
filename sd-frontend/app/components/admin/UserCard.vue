<template>
  <UCard
    class="hover:border-primary/50 transition-colors"
    variant="outline"
  >
    <div class="space-y-4">
      <!-- User Header -->
      <div class="flex items-start justify-between">
        <div class="flex items-center gap-3">
          <UAvatar
            :alt="user.fullName || `${user.firstName} ${user.lastName || ''}`.trim()"
            icon="i-lucide-user"
            size="lg"
          />

          <div>
            <h3 class="font-semibold text-primary text-lg">
              {{ user.fullName || `${user.firstName} ${user.lastName || ''}`.trim() }}
            </h3>
            <p class="text-sm text-muted">
              {{ user.email }}
            </p>
          </div>
        </div>
        <div class="flex items-center gap-1">
          <UButton
            variant="ghost"
            color="info"
            size="sm"
            icon="i-lucide-edit"
            @click="onEdit"
          />
          <UButton
            variant="ghost"
            color="warning"
            size="sm"
            icon="i-lucide-rotate-ccw-key"
            @click="onRevokeTokens"
          />
          <UiConfirmDialog
            title="Delete User"
            :message="`Are you sure you want to delete ${user.fullName || `${user.firstName} ${user.lastName || ''}`.trim()}?`"
            subtitle="This action cannot be undone."
            confirm-text="Delete User"
            confirm-color="error"
            icon="i-lucide-trash-2"
            icon-color-class="text-error-500"
            :is-processing="isDeleting"
            @confirm="onConfirmDelete"
          >
            <template #button>
              <UButton
                variant="ghost"
                color="error"
                size="sm"
                icon="i-lucide-trash-2"
                @click.stop
              />
            </template>
          </UiConfirmDialog>
        </div>
      </div>

      <!-- User Role Badge -->
      <div class="flex items-center justify-between">
        <UBadge
          :color="user.globalRoleId == 2 ? 'success' : 'neutral'"
          variant="soft"
          :icon="user.globalRoleId == 2 ? 'i-lucide-crown' : 'i-lucide-user'"
        >
          {{ user.globalRoleId == 2 ? 'Admin' : 'User' }}
        </UBadge>
      </div>

      <!-- User Metadata -->
      <USeparator />
      <div class="flex items-center justify-between text-xs text-muted">
        <span>Created {{ formatDate(user.createdAt) }}</span>
        <span>Updated {{ formatDate(user.updatedAt) }}</span>
      </div>
    </div>
  </UCard>
</template>

<script setup>
defineProps({
  user: {
    type: Object,
    required: true,
  },
  isDeleting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['edit', 'revoke-tokens', 'delete'])

const onConfirmDelete = () => {
  emit('delete', user)
}

const onRevokeTokens = () => {
  emit('revoke-tokens', user)
}

const onEdit = () => {
  emit('edit', user)
}
</script>
