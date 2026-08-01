<template>
  <div class="flex flex-col items-center py-6 sm:py-8">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <div class="flex items-center justify-between">
          <UiCardHeader
            :title="$t('profile.title')"
            :subtitle="$t('profile.subtitle')"
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
            {{ $t('profile.preferences') }}
          </p>
          <p class="text-sm text-muted mt-1">
            {{ $t('profile.preferencesDescription') }}
          </p>
          <UFormField
            :label="$t('profile.theme')"
            class="mt-4"
          >
            <SettingsThemeSwitcher />
          </UFormField>
          <UFormField
            :label="$t('profile.language')"
            class="mt-4"
          >
            <SettingsLanguageSwitcher />
          </UFormField>
        </UCard>

        <UCard
          class="sd-surface"
          :ui="{ body: 'p-4 sm:p-5' }"
        >
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="text-sm font-medium text-highlighted">
                {{ $t('profile.twoFactorAuth') }}
              </p>
              <p class="text-sm text-muted mt-1">
                {{ user.twoFactorEnabled ? $t('profile.twoFactorEnabled') : $t('profile.twoFactorNotEnabled') }}
              </p>
            </div>
            <UBadge
              :color="user.twoFactorEnabled ? 'success' : 'neutral'"
              variant="subtle"
            >
              {{ user.twoFactorEnabled ? $t('profile.on') : $t('profile.off') }}
            </UBadge>
          </div>
          <div class="mt-4">
            <UButton
              :to="user.twoFactorEnabled ? '/settings/2fa' : '/settings/2fa/setup'"
              :label="user.twoFactorEnabled ? $t('profile.twoFactorSettings') : $t('profile.setUp2FA')"
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
                  :text="$t('profile.copyToClipboard')"
                  :content="{ side: 'right' }"
                >
                  <UButton
                    :color="copied ? 'success' : 'neutral'"
                    variant="link"
                    size="sm"
                    :icon="copied ? 'i-lucide-copy-check' : 'i-lucide-copy'"
                    :aria-label="$t('profile.copyToClipboard')"
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
          {{ $t('profile.unableToLoad') }}
        </p>
        <UButton
          class="mt-4 w-full sm:w-auto"
          @click="refreshProfile"
        >
          {{ $t('profile.retry') }}
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

const { t } = useI18n()

const { user, isLoading } = useAuth()
const { copy, copied } = useClipboard()

// Password change modal state
const isPasswordModalOpen = ref(false)

const profileActions = computed(() => [
  [
    {
      label: t('profile.changePassword'),
      icon: 'i-lucide-key',
      onSelect: () => { isPasswordModalOpen.value = true },
    },
    {
      label: user.value?.twoFactorEnabled ? t('profile.twoFactorSettings') : t('profile.setUp2FA'),
      icon: user.value?.twoFactorEnabled ? 'i-lucide-shield-check' : 'i-lucide-shield',
      to: '/settings/2fa/setup',
    },
  ],
])

const handlePasswordChangeSuccess = () => {
  // Modal will handle redirection to login
  // This is just here for potential future use
}

const userForm = computed(() => [
  {
    label: t('profile.firstName'),
    value: computed(() => user.value.firstName),
  },
  {
    label: t('profile.lastName'),
    value: computed(() => user.value.lastName || t('profile.notProvided')),
  },
  {
    label: t('profile.emailAddress'),
    value: computed(() => user.value.email),
  },
  {
    label: t('profile.userId'),
    value: computed(() => user.value.id),
    copyable: true,
  },
])

const refreshProfile = async () => {
  const { initialize } = useAuth()
  await initialize()
}

useHead({
  title: computed(() => t('profile.title')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
