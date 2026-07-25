<template>
  <div class="flex flex-col items-center py-6 sm:py-8">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <div class="flex items-center justify-between">
          <UiCardHeader
            title="Profile"
            subtitle="Manage your personal information"
          />
          <UiButtonDropdown
            v-if="user"
            icon-only
            color="neutral"
            variant="ghost"
            dropdown-icon="i-lucide-ellipsis-vertical"
            :items="profileActions"
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
        <UCard
          class="sd-surface"
          :ui="{ body: 'p-4 sm:p-5' }"
        >
          <p class="text-sm font-medium text-highlighted">
            Preferences
          </p>
          <p class="text-sm text-muted mt-1">
            Choose how the app looks for you across all your devices.
          </p>
          <UFormField
            label="Theme"
            class="mt-4"
          >
            <USelect
              v-model="themePreference"
              :items="themeOptions"
              placeholder="Select theme..."
              class="w-full sm:w-64"
            />
          </UFormField>
        </UCard>

        <UCard
          class="sd-surface"
          :ui="{ body: 'p-4 sm:p-5' }"
        >
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="text-sm font-medium text-highlighted">
                Two-Factor Authentication
              </p>
              <p class="text-sm text-muted mt-1">
                {{ user.twoFactorEnabled ? 'Enabled — your account is protected.' : 'Not enabled.' }}
              </p>
            </div>
            <UBadge
              :color="user.twoFactorEnabled ? 'success' : 'neutral'"
              variant="subtle"
            >
              {{ user.twoFactorEnabled ? 'On' : 'Off' }}
            </UBadge>
          </div>
          <div class="mt-4">
            <UButton
              :to="user.twoFactorEnabled ? '/settings/2fa' : '/settings/2fa/setup'"
              :label="user.twoFactorEnabled ? '2FA Settings' : 'Set up 2FA'"
              variant="outline"
              color="neutral"
            />
          </div>
        </UCard>

        <div class="grid grid-cols-1 sm:grid-cols-[160px_1fr] gap-x-4 gap-y-3">
          <template
            v-for="field in userForm"
            :key="field.label"
          >
            <p class="text-sm text-muted sm:text-right sm:pt-2">
              {{ field.label }}
            </p>
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
          class="mt-4 w-full sm:w-auto"
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
const { settings } = useUserSettings()
const { copy, copied } = useClipboard()

// Password change modal state
const isPasswordModalOpen = ref(false)

const themeOptions = [
  { label: 'Auto', value: 'auto' },
  { label: 'Light', value: 'light' },
  { label: 'Dark', value: 'dark' },
]

const themePreference = computed({
  get() {
    return settings.value.theme
  },
  set(value) {
    // Apply locally; AppHeader's colorMode.preference watcher persists it
    const colorMode = useColorMode()
    colorMode.preference = value === 'auto' ? 'system' : value
  },
})

const profileActions = computed(() => [
  [
    {
      label: 'Change Password',
      icon: 'i-lucide-key',
      onSelect: () => { isPasswordModalOpen.value = true },
    },
    {
      label: user.value?.twoFactorEnabled ? '2FA Settings' : 'Set up 2FA',
      icon: user.value?.twoFactorEnabled ? 'i-lucide-shield-check' : 'i-lucide-shield',
      to: '/settings/2fa/setup',
    },
  ],
])

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
