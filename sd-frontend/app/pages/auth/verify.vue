<template>
  <div class="flex justify-center items-center h-screen p-4">
    <div class="w-full sm:w-3/4 md:w-1/2 space-y-4">
      <UCard>
        <template #header>
          <div class="text-center space-y-1">
            <h1 class="text-xl font-semibold">
              Two-Factor Verification
            </h1>
            <p class="text-sm text-muted">
              Enter your verification code to continue
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
                Enter the 6-digit code from your authenticator app.
              </p>
              <UPinInput
                v-model="totpCode"
                :length="6"
                type="number"
                otp
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
                Verify
              </UButton>
            </div>
          </template>

          <template #backup>
            <div class="flex flex-col items-center space-y-4 pt-3">
              <p class="text-sm text-muted">
                Enter one of your emergency backup codes.
              </p>
              <UInput
                v-model="backupCode"
                placeholder="xxxx-yyyyy"
                class="pb-3"
                :disabled="isLoading"
              />
              <UButton
                block
                :loading="isLoading"
                :disabled="!backupCode"
                @click="verify('backup', backupCode)"
              >
                Verify
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
          Back to login
        </NuxtLink>
      </div>
    </div>
  </div>
</template>

<script setup>
useHead({ title: 'Verify Identity' })

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

const tabs = [
  { label: 'Authenticator App', slot: 'totp' },
  { label: 'Backup Code', slot: 'backup' },
]
const activeTab = ref('0')

const totpCode = ref([])
const backupCode = ref('')
const isLoading = ref(false)

// Auto-submit when 6 digits are entered
watch(totpCode, (val) => {
  if (val.length === 6 && activeTab.value === 'totp') {
    verify('totp', val.join(''))
  }
})

const verify = async (codeType, code) => {
  if (!pendingChallengeToken.value) {
    await navigateTo('/')
    return
  }

  isLoading.value = true
  try {
    const response = await api.post('/auth/verify-2fa', {
      challengeToken: pendingChallengeToken.value,
      code,
      codeType,
    })

    if (response.success && response.data) {
      completeTwoFactorLogin(response.data)
      showSuccess('Login successful! Redirecting...')
      await navigateTo('/dashboard')
    }
    else {
      showError(response.error?.message || 'Verification failed')
      resetCode(codeType)
    }
  }
  catch (error) {
    showError(error.message || 'Verification failed')
    resetCode(codeType)
  }
  finally {
    isLoading.value = false
  }
}

const resetCode = (codeType) => {
  if (codeType === 'totp') totpCode.value = []
  else backupCode.value = ''
}
</script>
