<template>
  <div class="flex flex-col items-center py-6 sm:py-8">
    <div class="w-full max-w-2xl">
      <UiCardHeader
        size="lg"
        :title="$t('profile.title')"
        :subtitle="$t('profile.subtitle')"
        class="mb-6"
      />

      <UCard
        class="sd-surface w-full"
        :ui="{ body: 'p-0 sm:p-0' }"
      >
        <template v-if="user">
          <section class="p-5 sm:p-6">
            <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:gap-5">
              <UserAvatar
                :user="user"
                size="3xl"
                class="shrink-0"
              />
              <div class="min-w-0 flex-1 space-y-1">
                <p class="truncate text-lg font-semibold text-highlighted">
                  {{ displayName }}
                </p>
                <p class="truncate text-sm text-muted">
                  {{ user.email }}
                </p>
                <UBadge
                  :color="isGlobalAdmin ? 'secondary' : 'neutral'"
                  variant="subtle"
                  size="sm"
                  class="mt-1"
                >
                  {{ roleLabel }}
                </UBadge>
              </div>
              <div class="flex flex-wrap gap-2">
                <UButton
                  :label="$t('profile.avatar.upload')"
                  icon="i-lucide-upload"
                  size="sm"
                  :loading="isAvatarLoading"
                  @click="triggerFileInput"
                />
                <UButton
                  v-if="user.hasAvatar"
                  :label="$t('profile.avatar.remove')"
                  icon="i-lucide-trash-2"
                  color="error"
                  variant="ghost"
                  size="sm"
                  :loading="isAvatarLoading"
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
          </section>

          <USeparator />

          <section class="p-5 sm:p-6">
            <div>
              <h2 class="text-sm font-medium text-highlighted">
                {{ $t('profile.personalInfo') }}
              </h2>
              <p class="text-sm text-muted mt-1">
                {{ $t('profile.personalInfoDescription') }}
              </p>
            </div>
            <UForm
              :state="personalForm"
              class="mt-4 space-y-4"
              @submit="savePersonalInfo"
            >
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <UFormField
                  :label="$t('profile.firstName')"
                  name="firstName"
                  required
                >
                  <UInput
                    v-model="personalForm.firstName"
                    required
                    size="lg"
                    class="w-full"
                    :disabled="isSavingPersonalInfo"
                  />
                </UFormField>
                <UFormField
                  :label="$t('profile.lastName')"
                  name="lastName"
                >
                  <UInput
                    v-model="personalForm.lastName"
                    size="lg"
                    class="w-full"
                    :disabled="isSavingPersonalInfo"
                  />
                </UFormField>
              </div>
              <div class="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
                <UButton
                  :label="$t('common.cancel')"
                  color="neutral"
                  variant="ghost"
                  :disabled="!isPersonalInfoDirty || isSavingPersonalInfo"
                  @click="resetPersonalInfo"
                />
                <UButton
                  type="submit"
                  :label="$t('profile.saveChanges')"
                  :loading="isSavingPersonalInfo"
                  :disabled="!isPersonalInfoDirty || isSavingPersonalInfo"
                />
              </div>
            </UForm>
          </section>

          <USeparator />

          <section class="p-5 sm:p-6">
            <div>
              <h2 class="text-sm font-medium text-highlighted">
                {{ $t('profile.preferences') }}
              </h2>
              <p class="text-sm text-muted mt-1">
                {{ $t('profile.preferencesDescription') }}
              </p>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
              <UFormField :label="$t('profile.theme')">
                <SettingsThemeSwitcher />
              </UFormField>
              <UFormField :label="$t('profile.language')">
                <SettingsLanguageSwitcher />
              </UFormField>
            </div>
          </section>

          <USeparator />

          <section class="p-5 sm:p-6 space-y-4">
            <div>
              <h2 class="text-sm font-medium text-highlighted">
                {{ $t('profile.security') }}
              </h2>
              <p class="text-sm text-muted mt-1">
                {{ $t('profile.securityDescription') }}
              </p>
            </div>
            <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
              <div class="flex items-center gap-3">
                <span class="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
                  <UIcon
                    name="i-lucide-key"
                    class="size-5"
                  />
                </span>
                <div class="min-w-0">
                  <h3 class="text-sm font-medium text-highlighted">
                    {{ $t('profile.password') }}
                  </h3>
                  <p class="text-sm text-muted mt-0.5">
                    {{ $t('profile.passwordDescription') }}
                  </p>
                </div>
              </div>
              <UButton
                :label="$t('profile.changePassword')"
                variant="outline"
                color="neutral"
                size="sm"
                class="w-full justify-center sm:w-auto"
                @click="isPasswordModalOpen = true"
              />
            </div>
            <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
              <div class="flex items-center gap-3">
                <span class="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
                  <UIcon
                    :name="user.twoFactorEnabled ? 'i-lucide-shield-check' : 'i-lucide-shield'"
                    class="size-5"
                  />
                </span>
                <div class="min-w-0">
                  <div class="flex items-center gap-2">
                    <h3 class="text-sm font-medium text-highlighted">
                      {{ $t('profile.twoFactorAuth') }}
                    </h3>
                    <UBadge
                      :color="user.twoFactorEnabled ? 'success' : 'neutral'"
                      variant="subtle"
                      size="sm"
                    >
                      {{ user.twoFactorEnabled ? $t('profile.on') : $t('profile.off') }}
                    </UBadge>
                  </div>
                  <p class="text-sm text-muted mt-0.5">
                    {{ user.twoFactorEnabled ? $t('profile.twoFactorEnabled') : $t('profile.twoFactorNotEnabled') }}
                  </p>
                </div>
              </div>
              <UButton
                :label="user.twoFactorEnabled ? $t('profile.twoFactorSettings') : $t('profile.setUp2FA')"
                variant="outline"
                color="neutral"
                size="sm"
                class="w-full justify-center sm:w-auto"
                to="/settings/2fa/setup"
              />
            </div>
          </section>

          <USeparator />

          <section class="p-5 sm:p-6">
            <div>
              <h2 class="text-sm font-medium text-highlighted">
                {{ $t('profile.accountDetails') }}
              </h2>
              <p class="text-sm text-muted mt-1">
                {{ $t('profile.accountDetailsDescription') }}
              </p>
            </div>
            <dl class="mt-4 space-y-3">
              <div class="flex flex-col gap-1.5 sm:flex-row sm:items-center sm:justify-between sm:gap-3">
                <dt class="text-sm text-muted">
                  {{ $t('profile.userId') }}
                </dt>
                <dd class="flex min-w-0 items-center gap-2">
                  <span class="min-w-0 flex-1 truncate font-mono text-xs text-highlighted sm:max-w-80 sm:flex-none sm:text-sm">{{ user.id }}</span>
                  <UTooltip
                    :text="$t('profile.copyToClipboard')"
                    :content="{ side: 'left' }"
                  >
                    <UButton
                      :color="copied ? 'success' : 'neutral'"
                      variant="link"
                      size="sm"
                      square
                      class="shrink-0"
                      :icon="copied ? 'i-lucide-copy-check' : 'i-lucide-copy'"
                      :aria-label="$t('profile.copyToClipboard')"
                      @click="copy(user.id)"
                    />
                  </UTooltip>
                </dd>
              </div>
              <div class="flex flex-col gap-1.5 sm:flex-row sm:items-center sm:justify-between sm:gap-3">
                <dt class="text-sm text-muted">
                  {{ $t('profile.memberSince') }}
                </dt>
                <dd class="text-sm text-highlighted sm:text-right">
                  {{ formatDate(user.createdAt) }}
                </dd>
              </div>
            </dl>
          </section>
        </template>

        <div
          v-else
          class="py-8 text-center"
        >
          <p class="text-muted">
            {{ $t('profile.unableToLoad') }}
          </p>
          <UButton
            class="mt-4 w-full sm:w-auto"
            :loading="isRefreshing"
            @click="refreshProfile"
          >
            {{ $t('profile.retry') }}
          </UButton>
        </div>
      </UCard>
    </div>

    <ChangePasswordModal
      v-model:open="isPasswordModalOpen"
    />
  </div>
</template>

<script setup lang="ts">
import type { User } from '~/types/domain'
import { useClipboard } from '@vueuse/core'

const { t } = useI18n()

const { user, initialize, isGlobalAdmin } = useAuth()
const { copy, copied } = useClipboard()
const { uploadAvatar, deleteAvatar, isLoading: isAvatarLoading } = useUserAvatar()
const { updateCurrentUser } = useUsers()
const modal = useModal()

// ─── Identity (section 1) ────────────────────────────────────────────────────

const displayName = computed(() => {
  const u = user.value
  if (!u) return ''
  return u.fullName?.trim() || `${u.firstName} ${u.lastName ?? ''}`.trim() || u.email
})

// globalRoleId may arrive as number or string from the backend (union in the DTO),
// so reuse the composable's canonical admin check instead of a local comparison.
const roleLabel = computed(() => isGlobalAdmin.value ? t('profile.roleAdmin') : t('profile.roleUser'))

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
    await initialize() // refresh user data (hasAvatar flag)
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
    await initialize() // refresh user data (hasAvatar flag)
  }
  catch {
    // Error shown via toast by the composable
  }
}

// ─── Personal information (section 2) ────────────────────────────────────────

interface PersonalInfoForm {
  firstName: string
  lastName: string
}

const personalForm = ref<PersonalInfoForm>({ firstName: '', lastName: '' })
const personalBaseline = ref<PersonalInfoForm>({ firstName: '', lastName: '' })

function syncPersonalInfo(u: User) {
  const next: PersonalInfoForm = {
    firstName: u.firstName,
    lastName: u.lastName ?? '',
  }
  personalForm.value = { ...next }
  personalBaseline.value = { ...next }
}

// Declared before the watcher below: with `immediate: true` the callback runs
// synchronously, so referencing this computed after the watch would hit its TDZ.
const isPersonalInfoDirty = computed(() =>
  personalForm.value.firstName !== personalBaseline.value.firstName
  || personalForm.value.lastName !== personalBaseline.value.lastName,
)

watch(user, (u) => {
  // External user updates (avatar upload, refresh) re-sync the form, but never
  // clobber it while the user has unsaved edits in progress.
  if (u && !isPersonalInfoDirty.value) syncPersonalInfo(u)
}, { immediate: true })

const isSavingPersonalInfo = ref(false)

const savePersonalInfo = async () => {
  if (isSavingPersonalInfo.value) return
  isSavingPersonalInfo.value = true
  try {
    const updated = await updateCurrentUser({
      firstName: personalForm.value.firstName,
      lastName: personalForm.value.lastName,
    })
    if (!updated) return
    await initialize() // refresh the global user + cookie
    // Re-sync both form and baseline from the server response: the backend
    // normalizes names (e.g. trims whitespace), so the submitted values may
    // differ from what was stored and would otherwise leave the form dirty.
    syncPersonalInfo(updated)
  }
  catch {
    // Error shown via toast by the composable
  }
  finally {
    isSavingPersonalInfo.value = false
  }
}

const resetPersonalInfo = () => {
  personalForm.value = { ...personalBaseline.value }
}

// ─── Security (section 4) ────────────────────────────────────────────────────

const isPasswordModalOpen = ref(false)

// ─── Error/retry state ───────────────────────────────────────────────────────

const isRefreshing = ref(false)

const refreshProfile = async () => {
  isRefreshing.value = true
  try {
    await initialize()
  }
  finally {
    isRefreshing.value = false
  }
}

useHead({
  title: computed(() => t('profile.title')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
