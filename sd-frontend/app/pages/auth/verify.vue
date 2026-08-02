<template>
  <div class="min-h-dvh flex items-center justify-center p-4">
    <div class="w-full max-w-md space-y-4">
      <UCard>
        <template #header>
          <div class="text-center space-y-1">
            <h1 class="text-xl font-semibold">
              {{ $t('auth.twoFactorVerification') }}
            </h1>
            <p class="text-sm text-muted">
              {{ $t('auth.enterVerificationCode') }}
            </p>
          </div>
        </template>

        <UTabs
          v-model="activeTab"
          :items="tabs"
          class="w-full"
        >
          <template #totp>
            <div class="flex flex-col items-center space-y-4 pt-3">
              <p class="text-sm text-muted">
                {{ $t('auth.enter6DigitCode') }}
              </p>
              <UPinInput
                v-model="totpCode"
                :length="6"
                type="number"
                otp
                autofocus
                autocomplete="one-time-code"
                class="pb-3"
                :disabled="isLoading"
              />
              <UButton
                block
                :loading="isLoading"
                :disabled="totpCode.length < 6"
                @click="verify('totp', totpCode.join(''))"
              >
                {{ $t('auth.verify') }}
              </UButton>
            </div>
          </template>

          <template #backup>
            <div class="flex flex-col items-center space-y-4 pt-3">
              <p class="text-sm text-muted">
                {{ $t('auth.enterBackupCode') }}
              </p>
              <UInput
                v-model="backupCode"
                :placeholder="$t('auth.backupCodePlaceholder')"
                class="w-full pb-3"
                :disabled="isLoading"
              />
              <UButton
                block
                :loading="isLoading"
                :disabled="!backupCode"
                @click="verify('backup', backupCode)"
              >
                {{ $t('auth.verify') }}
              </UButton>
            </div>
          </template>
        </UTabs>
      </UCard>

      <div class="text-center">
        <NuxtLink
          to="/"
          class="text-sm text-muted hover:text-primary transition-colors"
        >
          {{ $t('auth.backToLogin') }}
        </NuxtLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { AuthResponse } from '~/types/domain'

const { t } = useI18n()
useHead({ title: computed(() => t('auth.twoFactorVerification')) })

definePageMeta({
  layout: 'auth',
})

const { pendingChallengeToken, completeTwoFactorLogin } = useAuth()
const api = useApi()
const { showError, showSuccess } = useNotifications()

// Guard: must have a pending challenge token
onMounted(() => {
  if (!pendingChallengeToken.value) {
    navigateTo('/')
  }
})

const tabs = computed(() => [
  { label: t('auth.authenticatorApp'), slot: 'totp' },
  { label: t('auth.backupCode'), slot: 'backup' },
])
const activeTab = ref('0')

const totpCode = ref<number[]>([])
const backupCode = ref('')
const isLoading = ref(false)

// Auto-submit when 6 digits are entered
watch(totpCode, (val) => {
  if (val.length === 6) {
    verify('totp', val.join(''))
  }
})

const verify = async (codeType: string, code: string) => {
  if (!pendingChallengeToken.value) {
    await navigateTo('/')
    return
  }

  isLoading.value = true
  try {
    const response = await api.post<AuthResponse>('/auth/verify-2fa', {
      challengeToken: pendingChallengeToken.value,
      code,
      codeType,
    })

    if (response.success && response.data) {
      completeTwoFactorLogin(response.data)
      showSuccess(t('auth.loginSuccessfulRedirect'))
      await navigateTo('/dashboard')
    }
    else {
      showError(response.error?.message || t('auth.verificationFailed'))
      resetCode(codeType)
    }
  }
  catch (error: unknown) {
    showError(error instanceof Error ? error.message : t('auth.verificationFailed'))
    resetCode(codeType)
  }
  finally {
    isLoading.value = false
  }
}

const resetCode = (codeType: string) => {
  if (codeType === 'totp') totpCode.value = []
  else backupCode.value = ''
}
</script>
