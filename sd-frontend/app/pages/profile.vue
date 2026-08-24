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
          <div class="flex items-center gap-4">
            <UserAvatar
              :user="user"
              size="3xl"
            />
            <div class="space-y-2">
              <p class="text-sm text-muted">
                {{ $t('profile.avatar.description') }}
              </p>
              <div class="flex gap-2">
                <UButton
                  :label="$t('profile.avatar.upload')"
                  icon="i-lucide-upload"
                  size="sm"
                  :loading="avatarLoading"
                  @click="triggerFileInput"
                />
                <UButton
                  v-if="user.hasAvatar"
                  :label="$t('profile.avatar.remove')"
                  icon="i-lucide-trash-2"
                  color="error"
                  variant="outline"
                  size="sm"
                  :loading="avatarLoading"
                  @click="confirmRemoveAvatar"
                />
              </div>
              <input
                ref="fileInput"
                type="file"
                accept="image/jpeg,image/png,image/webp"
                class="hidden"
                @change="onFileSelected"
              >
            </div>
          </div>
        </UCard>

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

<script setup lang="ts">
import { useClipboard } from '@vueuse/core'

const { t } = useI18n()

const { user, isLoading } = useAuth()
const { copy, copied } = useClipboard()
const { uploadAvatar, deleteAvatar, isLoading: avatarLoading } = useUserAvatar()
const modal = useModal()
const fileInput = ref<HTMLInputElement | null>(null)

const triggerFileInput = () => {
  fileInput.value?.click()
}

const onFileSelected = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return
  try {
    await uploadAvatar(file)
    await useAuth().initialize() // refresh user data (hasAvatar flag)
  }
  catch {
    // Error shown via toast by the composable
  }
  target.value = '' // reset input so the same file can be re-selected
}

const confirmRemoveAvatar = async () => {
  const confirmed = await modal.error({
    title: t('profile.avatar.removeConfirm'),
    content: t('profile.avatar.removeConfirmDescription'),
    confirmText: t('profile.avatar.remove'),
    cancelText: t('common.cancel'),
  })

  if (!confirmed) return

  try {
    await deleteAvatar()
    await useAuth().initialize() // refresh user data (hasAvatar flag)
  }
  catch {
    // Error shown via toast by the composable
  }
}

// Password change modal state
const isPasswordModalOpen = ref(false)

const profileActions = computed(() => [
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
] as { label: string, icon: string, [key: string]: unknown }[])

const handlePasswordChangeSuccess = () => {
  // Modal will handle redirection to login
  // This is just here for potential future use
}

const userForm = computed(() => {
  const u = user.value
  if (!u) return []
  return [
    {
      label: t('profile.firstName'),
      value: computed(() => u.firstName),
    },
    {
      label: t('profile.lastName'),
      value: computed(() => u.lastName || t('profile.notProvided')),
    },
    {
      label: t('profile.emailAddress'),
      value: computed(() => u.email),
    },
    {
      label: t('profile.userId'),
      value: computed(() => u.id),
      copyable: true,
    },
  ]
})

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
