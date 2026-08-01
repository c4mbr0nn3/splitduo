<template>
  <div class="py-6 px-4 sm:py-8 flex flex-col items-center">
    <UCard class="w-full max-w-2xl">
      <template #header>
        <UiCardHeader
          :title="$t('twoFactor.title')"
          :subtitle="$t('twoFactor.subtitle')"
          back-to="/profile"
        />
      </template>

      <!-- Status view -->
      <div v-if="view === 'status'">
        <div class="space-y-4">
          <div class="flex items-center gap-3">
            <UIcon
              :name="user.twoFactorEnabled ? 'i-lucide-shield-check' : 'i-lucide-shield-off'"
              :class="user.twoFactorEnabled ? 'text-success' : 'text-error'"
              class="size-5 shrink-0"
            />
            <span class="text-sm text-muted">
              {{ user?.twoFactorEnabled ? $t('twoFactor.active') : $t('twoFactor.notSetUp') }}
            </span>
          </div>

          <UButton
            v-if="user?.twoFactorEnabled"
            block
            color="error"
            variant="outline"
            @click="view = 'disabling'"
          >
            {{ $t('twoFactor.disable') }}
          </UButton>
          <UButton
            v-else
            block
            :loading="isLoading"
            @click="startSetup"
          >
            {{ $t('twoFactor.setUpAuthenticator') }}
          </UButton>
        </div>
      </div>

      <!-- Enrolling view -->
      <div
        v-if="view === 'enrolling'"
        class="space-y-6"
      >
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">
              {{ $t('twoFactor.step1ScanQR') }}
            </h2>
          </template>
          <div class="space-y-4">
            <p class="text-sm text-muted">
              {{ $t('twoFactor.scanQRDescription') }}
            </p>

            <!-- QR code — rendered from uqr SVG string -->
            <div
              v-if="qrSvg"
              class="flex justify-center p-4 rounded-xl bg-white border border-neutral-200 dark:border-neutral-700 shadow-[var(--sd-card-shadow)]"
              v-html="qrSvg"
            />

            <div class="space-y-1">
              <p class="text-xs text-muted">
                {{ $t('twoFactor.enterSecretManually') }}
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
            <h2 class="text-lg font-semibold">
              {{ $t('twoFactor.step2SaveBackupCodes') }}
            </h2>
          </template>
          <div class="space-y-4">
            <p class="text-sm text-muted">
              {{ $t('twoFactor.saveBackupCodesDescription') }}
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
              :label="$t('twoFactor.savedCodes')"
            />

            <UButton
              block
              :disabled="!savedCodes"
              @click="view = 'verifying'"
            >
              {{ $t('twoFactor.continue') }}
            </UButton>
          </div>
        </UCard>
      </div>

      <!-- Verifying view -->
      <div v-if="view === 'verifying'">
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">
              {{ $t('twoFactor.step3Verify') }}
            </h2>
          </template>
          <div class="space-y-4">
            <p class="text-sm text-muted">
              {{ $t('twoFactor.verifyDescription') }}
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
                {{ $t('twoFactor.back') }}
              </UButton>
              <UButton
                class="flex-1"
                :loading="isLoading"
                :disabled="verifyCode.length < 6"
                @click="confirmSetup"
              >
                {{ $t('twoFactor.enable2FA') }}
              </UButton>
            </div>
          </div>
        </UCard>
      </div>

      <!-- Disabling view -->
      <div v-if="view === 'disabling'">
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">
              {{ $t('twoFactor.disableTitle') }}
            </h2>
          </template>
          <div class="space-y-4">
            <p class="text-sm text-muted">
              {{ $t('twoFactor.disableDescription') }}
            </p>

            <UInput
              v-model="disablePassword"
              type="password"
              :placeholder="$t('twoFactor.currentPasswordPlaceholder')"
              :disabled="isLoading"
              class="w-full"
            />

            <div class="flex gap-2">
              <UButton
                variant="outline"
                class="flex-1"
                @click="view = 'status'"
              >
                {{ $t('twoFactor.cancel') }}
              </UButton>
              <UButton
                color="error"
                class="flex-1"
                :loading="isLoading"
                :disabled="!disablePassword"
                @click="confirmDisable"
              >
                {{ $t('twoFactor.disable2FA') }}
              </UButton>
            </div>
          </div>
        </UCard>
      </div>
    </UCard>
  </div>
</template>

<script setup>
const { t } = useI18n()
useHead({ title: computed(() => t('twoFactor.title')) })

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

const modal = useModal()

const confirmDisable = async () => {
  const confirmed = await modal.error({
    title: t('twoFactor.disableConfirmTitle'),
    subtitle: t('twoFactor.disableConfirmSubtitle'),
    content: t('twoFactor.disableConfirmContent'),
    confirmText: t('twoFactor.disableConfirmButton'),
    cancelText: t('common.cancel'),
  })

  if (!confirmed) return

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
