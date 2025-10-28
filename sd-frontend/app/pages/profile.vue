<template>
  <div class="py-6">
    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <UiCardHeader
            title="Profile"
            subtitle="Manage your personal information"
          />
        </div>
      </template>
      <div
        v-if="isLoading"
        class="flex justify-center py-8"
      >
        <USkeleton class="h-4 w-full" />
      </div>

      <div
        v-else-if="user"
        class="space-y-6"
      >
        <UButton
          v-if="user"
          icon="i-lucide-key"
          color="primary"
          block
          @click="isPasswordModalOpen = true"
        >
          Change Password
        </UButton>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <template
            v-for="field in userForm"
            :key="field.label"
          >
            <UFormField :label="field.label">
              <UInput
                v-if="!field.copyable"
                :value="field.value.value"
                disabled
                class="w-full"
              />
              <UInput
                v-else
                :value="field.value.value"
                disabled
                class="w-full"
                :ui="{ trailing: 'pr-0.5' }"
              >
                <template
                  v-if="field.value.value?.length"
                  #trailing
                >
                  <UTooltip
                    text="Copy to clipboard"
                    :content="{ side: 'right' }"
                  >
                    <UButton
                      :color="copied ? 'success' : 'neutral'"
                      variant="link"
                      size="sm"
                      :icon="copied ? 'i-lucide-copy-check' : 'i-lucide-copy'"
                      aria-label="Copy to clipboard"
                      @click="copy(field.value.value)"
                    />
                  </UTooltip>
                </template>
              </UInput>
            </UFormField>
          </template>
        </div>
      </div>

      <div
        v-else
        class="text-center py-8"
      >
        <p class="text-muted">
          Unable to load profile information
        </p>
        <UButton
          class="mt-4"
          @click="refreshProfile"
        >
          Retry
        </UButton>
      </div>
    </UCard>
    <ChangePasswordModal
      v-model:open="isPasswordModalOpen"
      @success="handlePasswordChangeSuccess"
    />
  </div>
</template>

<script setup>
import { useClipboard } from '@vueuse/core'

const { user, isLoading } = useAuth()
const { copy, copied } = useClipboard()

// Password change modal state
const isPasswordModalOpen = ref(false)

const handlePasswordChangeSuccess = () => {
  // Modal will handle redirection to login
  // This is just here for potential future use
}

const userForm = [
  {
    label: 'First Name',
    value: computed(() => user.value.firstName),
  },
  {
    label: 'Last Name',
    value: computed(() => user.value.lastName || 'Not provided'),
  },
  {
    label: 'Email Address',
    value: computed(() => user.value.email),
  },
  {
    label: 'User ID',
    value: computed(() => user.value.id),
    copyable: true,
  },
]

const refreshProfile = async () => {
  const { initialize } = useAuth()
  await initialize()
}

useHead({
  title: 'Profile',
})

definePageMeta({
  middleware: 'auth',
})
</script>
