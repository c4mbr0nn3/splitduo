<template>
  <div class="max-w-lg mx-auto space-y-6 p-4">
    <div>
      <h1 class="text-2xl font-semibold">
        Two-Factor Authentication
      </h1>
      <p class="text-sm text-muted mt-1">
        Add an extra layer of security to your account.
      </p>
    </div>

    <!-- Status view -->
    <UCard v-if="view === 'status'">
      <div class="space-y-4">
        <div class="flex items-center gap-3">
          <UIcon
            :name="user.twoFactorEnabled ? 'i-lucide-shield-check' : 'i-lucide-shield-off'"
            :class="user.twoFactorEnabled ? 'text-success-500' : 'text-error'"
            class="size-5 shrink-0"
          />
          <span class="text-sm text-muted">
            {{ user?.twoFactorEnabled ? '2FA is active on your account' : '2FA is not set up' }}
          </span>
        </div>

        <UButton
          v-if="user?.twoFactorEnabled"
          block
          color="error"
          variant="outline"
          @click="view = 'disabling'"
        >
          Disable 2FA
        </UButton>
        <UButton
          v-else
          block
          :loading="isLoading"
          @click="startSetup"
        >
          Set up authenticator
        </UButton>
      </div>
    </UCard>

    <!-- Enrolling view -->
    <template v-if="view === 'enrolling'">
      <UCard>
        <template #header>
          <h2 class="font-medium">
            Step 1: Scan QR code
          </h2>
        </template>
        <div class="space-y-4">
          <p class="text-sm text-muted">
            Scan this QR code with your authenticator app (e.g. Google Authenticator, Authy).
          </p>

          <!-- QR code — rendered from uqr SVG string -->
          <div
            v-if="qrSvg"
            class="flex justify-center p-4 bg-white rounded-lg"
            v-html="qrSvg"
          />

          <div class="space-y-1">
            <p class="text-xs text-muted">
              Or enter the secret key manually:
            </p>
            <div class="flex gap-2">
              <UInput
                :model-value="setupData?.secret"
                readonly
                class="flex-1 font-mono text-sm"
              />
              <UButton
                variant="outline"
                icon="i-lucide-copy"
                @click="copySecret"
              />
            </div>
          </div>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-medium">
            Step 2: Save backup codes
          </h2>
        </template>
        <div class="space-y-4">
          <p class="text-sm text-muted">
            Save these backup codes in a safe place. Each can only be used once.
          </p>

          <div class="grid grid-cols-2 gap-2">
            <div
              v-for="code in setupData?.backupCodes"
              :key="code"
              class="font-mono text-sm bg-muted/30 rounded px-3 py-1.5 text-center"
            >
              {{ code }}
            </div>
          </div>

          <UCheckbox
            v-model="savedCodes"
            label="I have saved these backup codes"
          />

          <UButton
            block
            :disabled="!savedCodes"
            @click="view = 'verifying'"
          >
            Continue
          </UButton>
        </div>
      </UCard>
    </template>

    <!-- Verifying view -->
    <UCard v-if="view === 'verifying'">
      <template #header>
        <h2 class="font-medium">
          Step 3: Verify your authenticator
        </h2>
      </template>
      <div class="space-y-4">
        <p class="text-sm text-muted">
          Enter the 6-digit code from your authenticator app to confirm setup.
        </p>

        <UPinInput
          v-model="verifyCode"
          :length="6"
          type="number"
          otp
          :disabled="isLoading"
          class="justify-center"
        />

        <div class="flex gap-2">
          <UButton
            variant="outline"
            class="flex-1"
            @click="view = 'enrolling'"
          >
            Back
          </UButton>
          <UButton
            class="flex-1"
            :loading="isLoading"
            :disabled="verifyCode.length < 6"
            @click="confirmSetup"
          >
            Enable 2FA
          </UButton>
        </div>
      </div>
    </UCard>

    <!-- Disabling view -->
    <UCard v-if="view === 'disabling'">
      <template #header>
        <h2 class="font-medium">
          Disable Two-Factor Authentication
        </h2>
      </template>
      <div class="space-y-4">
        <p class="text-sm text-muted">
          Enter your current password to disable 2FA.
        </p>

        <UInput
          v-model="disablePassword"
          type="password"
          placeholder="Current password"
          :disabled="isLoading"
        />

        <div class="flex gap-2">
          <UButton
            variant="outline"
            class="flex-1"
            @click="view = 'status'"
          >
            Cancel
          </UButton>
          <UButton
            color="error"
            class="flex-1"
            :loading="isLoading"
            :disabled="!disablePassword"
            @click="confirmDisable"
          >
            Disable 2FA
          </UButton>
        </div>
      </div>
    </UCard>
  </div>
</template>

<script setup>
useHead({ title: '2FA Setup' })

definePageMeta({
  layout: 'default',
  middleware: 'auth',
})

const { user, initialize } = useAuth()
const { initiateSetup, verifySetup, disable, isLoading } = use2FA()

const view = ref('status')
const setupData = ref(null)
const qrSvg = ref(null)
const savedCodes = ref(false)
const verifyCode = ref([])
const disablePassword = ref('')

const startSetup = async () => {
  const data = await initiateSetup()
  if (data) {
    setupData.value = data
    // Render QR code SVG from the TOTP URI
    try {
      const { renderSVG } = await import('uqr')
      qrSvg.value = renderSVG(data.qrCodeUri)
    }
    catch {
      // uqr not installed — show fallback text
      qrSvg.value = null
    }
    view.value = 'enrolling'
  }
}

const copySecret = () => {
  if (setupData.value?.secret) {
    navigator.clipboard.writeText(setupData.value.secret)
  }
}

const confirmSetup = async () => {
  const code = verifyCode.value.join('')
  try {
    await verifySetup(code)
    await initialize()
    view.value = 'status'
  }
  catch {
    verifyCode.value = []
  }
}

const confirmDisable = async () => {
  try {
    await disable(disablePassword.value)
    await initialize()
    disablePassword.value = ''
    view.value = 'status'
  }
  catch {
    disablePassword.value = ''
  }
}
</script>
