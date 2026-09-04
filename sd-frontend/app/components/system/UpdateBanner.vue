<template>
  <UAlert
    v-if="notification"
    color="info"
    variant="soft"
    icon="i-lucide-rocket"
    class="mb-6"
    :ui="{ actions: 'flex flex-wrap items-center gap-2' }"
  >
    <template #title>
      {{ $t('notifications.updateAvailable') }}
    </template>
    <template #description>
      {{ $t('notifications.updateFromTo', { current: payload?.current, latest: payload?.latest }) }}
    </template>
    <template #actions>
      <UButton
        size="sm"
        color="neutral"
        variant="outline"
        icon="i-lucide-external-link"
        :label="$t('notifications.viewRelease')"
        :to="releaseUrl"
        target="_blank"
      />
      <UButton
        size="sm"
        color="neutral"
        variant="ghost"
        :label="$t('notifications.dismiss')"
        :loading="isDismissing"
        @click="onDismiss"
      />
    </template>
  </UAlert>
</template>

<script setup lang="ts">
import type { AdminNotification } from '~/types/domain'

const props = defineProps<{
  notification: AdminNotification | null
}>()

const { dismiss } = useSystemNotifications()

const isDismissing = ref(false)

interface UpdatePayload {
  current?: string
  latest?: string
  releaseUrl?: string
}

const payload = computed<UpdatePayload | undefined>(() => {
  const raw = props.notification?.payload
  if (!raw || typeof raw !== 'object') return undefined
  return raw as UpdatePayload
})

const releaseUrl = computed(() => {
  const url = payload.value?.releaseUrl
  if (!url) return undefined
  // Only allow http(s) URLs from the server payload.
  return /^https?:\/\//.test(url) ? url : undefined
})

const onDismiss = async () => {
  if (!props.notification) return
  isDismissing.value = true
  try {
    await dismiss(props.notification.type, props.notification.targetKey)
  }
  finally {
    isDismissing.value = false
  }
}
</script>
