<script setup lang="ts">
const { t } = useI18n()
const route = useRoute()
const { confirmEmailChange } = useUsers()
const { clearSession } = useAuth()

const token = computed(() =>
  typeof route.query.token === 'string' ? route.query.token : '',
)

const state = ref<'idle' | 'loading' | 'done' | 'error'>('idle')

async function confirm() {
  if (!token.value) {
    state.value = 'error'
    return
  }

  state.value = 'loading'

  try {
    await confirmEmailChange(token.value)
    // The refresh token was revoked server-side; clear the local session too
    // so the old address does not linger in the UI until the next API call.
    // Do not call logout(): it navigates home and would hide the success copy.
    clearSession()
    state.value = 'done'
  }
  catch {
    state.value = 'error'
  }
}

useHead({
  title: computed(() => t('confirmEmailChange.title')),
})
</script>

<template>
  <div class="flex min-h-screen items-center justify-center px-4 py-8">
    <UCard class="w-full max-w-md">
      <template #header>
        <UiCardHeader
          :title="$t('confirmEmailChange.title')"
          :subtitle="$t('confirmEmailChange.description')"
        />
      </template>

      <div
        v-if="state === 'done'"
        class="space-y-4 text-center"
      >
        <UIcon
          name="i-lucide-mail-check"
          class="mx-auto size-10 text-success"
        />
        <p class="text-highlighted">
          {{ $t('confirmEmailChange.success') }}
        </p>
        <UButton
          to="/"
          class="w-full justify-center"
        >
          {{ $t('confirmEmailChange.login') }}
        </UButton>
      </div>

      <div
        v-else-if="state === 'error'"
        class="space-y-4 text-center"
      >
        <UIcon
          name="i-lucide-circle-alert"
          class="mx-auto size-10 text-error"
        />
        <p class="text-highlighted">
          {{ $t('confirmEmailChange.error') }}
        </p>
        <UButton
          to="/"
          variant="outline"
          color="neutral"
          class="w-full justify-center"
        >
          {{ $t('confirmEmailChange.login') }}
        </UButton>
      </div>

      <div
        v-else
        class="space-y-4"
      >
        <UButton
          :label="$t('confirmEmailChange.confirm')"
          :loading="state === 'loading'"
          :disabled="!token"
          class="w-full justify-center"
          @click="confirm"
        />
      </div>
    </UCard>
  </div>
</template>
