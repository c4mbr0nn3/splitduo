<template>
  <!-- Section idiom, not a UCard: on the profile page this sits between
       <USeparator />s inside the page's single card, matching the page's
       other <section class="p-5 sm:p-6"> blocks. Standalone elsewhere it
       still renders as a padded, self-contained block. -->
  <section class="p-5 sm:p-6">
    <div class="flex items-start justify-between gap-3">
      <div>
        <h2 class="text-sm font-medium text-highlighted">
          {{ $t('push.settings.title') }}
        </h2>
        <p class="text-sm text-muted mt-1">
          {{ $t('push.settings.description') }}
        </p>
      </div>
      <USwitch
        :model-value="pushEnabled"
        :disabled="!canToggle"
        :loading="isLoading"
        :aria-label="$t('push.settings.title')"
        @update:model-value="onToggle"
      />
    </div>

    <UAlert
      v-if="!isSupported"
      class="mt-4"
      color="neutral"
      variant="soft"
      icon="i-lucide-bell-off"
    >
      <template #description>
        {{ $t('push.settings.unsupported') }}
      </template>
    </UAlert>
    <UAlert
      v-else-if="isConfigured === false"
      class="mt-4"
      color="neutral"
      variant="soft"
      icon="i-lucide-server-off"
    >
      <template #description>
        {{ $t('push.settings.notConfigured') }}
      </template>
    </UAlert>

    <UAlert
      v-if="showIosHint"
      class="mt-4"
      color="info"
      variant="soft"
      icon="i-lucide-smartphone"
    >
      <template #description>
        {{ $t('push.settings.iosHint') }}
      </template>
    </UAlert>

    <template v-if="pushEnabled">
      <div class="mt-4 pt-4 border-t border-default">
        <p class="text-sm text-muted">
          {{ $t('push.settings.devicesTitle') }}
        </p>
        <USkeleton
          v-if="isLoadingDevices"
          class="mt-3 h-12 w-full"
        />
        <ul
          v-else-if="devices.length"
          class="mt-3 space-y-3"
        >
          <li
            v-for="device in devices"
            :key="device.id"
            class="flex items-center justify-between gap-3"
          >
            <div class="min-w-0">
              <div class="flex items-center gap-2 flex-wrap">
                <p class="text-sm text-highlighted truncate">
                  {{ device.deviceName }}
                </p>
                <UBadge
                  v-if="device.isCurrent"
                  color="primary"
                  variant="subtle"
                  size="sm"
                  :label="$t('push.settings.thisDevice')"
                />
              </div>
              <p class="text-xs text-dimmed mt-0.5">
                {{ $t('push.settings.registeredOn', { date: device.registeredOn }) }}
              </p>
            </div>
            <UButton
              :label="$t('push.settings.remove')"
              icon="i-lucide-trash-2"
              color="error"
              variant="outline"
              size="sm"
              :loading="removingEndpoint === device.endpoint"
              :disabled="removingEndpoint !== null"
              @click="removeDevice(device.endpoint)"
            />
          </li>
        </ul>
        <p
          v-else
          class="text-sm text-muted mt-3"
        >
          {{ $t('push.settings.noDevices') }}
        </p>
      </div>
    </template>
  </section>
</template>

<script setup lang="ts">
import type { PushSubscriptionListItem } from '~/composables/push/usePush'

interface DeviceRow {
  id: string
  endpoint: string
  deviceName: string
  registeredOn: string
  isCurrent: boolean
}

const BROWSERS = [
  { pattern: /Edg(?:e|A|iOS)?\//, name: 'Edge' },
  { pattern: /OPR\/|Opera/, name: 'Opera' },
  { pattern: /SamsungBrowser\//, name: 'Samsung Internet' },
  { pattern: /Firefox\//, name: 'Firefox' },
  { pattern: /Chrome\//, name: 'Chrome' },
  { pattern: /Version\/.*Safari\//, name: 'Safari' },
] as const

const OSES = [
  { pattern: /Windows NT/, name: 'Windows' },
  { pattern: /iPhone|iPad|iPod/, name: 'iOS' },
  { pattern: /Android/, name: 'Android' },
  { pattern: /Macintosh|Mac OS X/, name: 'macOS' },
  { pattern: /Linux/, name: 'Linux' },
] as const

const { t } = useI18n()
const {
  isSupported,
  permission,
  isConfigured,
  isSubscribed,
  subscribe,
  unsubscribe,
  checkConfiguration,
  fetchSubscriptions,
  removeSubscription,
} = usePush()

const isLoading = ref(false)
const isLoadingDevices = ref(false)
const removingEndpoint = ref<string | null>(null)
const devices = ref<DeviceRow[]>([])
const currentUa = navigator.userAgent

const pushEnabled = computed(() =>
  isSupported.value && permission.value === 'granted' && isSubscribed.value,
)

const canToggle = computed(() =>
  isSupported.value && isConfigured.value !== false,
)

const showIosHint = computed(() => {
  if (!canToggle.value || pushEnabled.value) return false
  if (!('matchMedia' in window)) return false
  const isAppleMobile = /iPad|iPhone|iPod/.test(currentUa)
    || (currentUa.includes('Macintosh') && 'ontouchend' in document)
  if (!isAppleMobile) return false
  return !window.matchMedia('(display-mode: standalone)').matches
})

const parseDeviceName = (userAgent: string | null): string => {
  if (!userAgent) return t('push.settings.unknownDevice')
  const browser = BROWSERS.find(({ pattern }) => pattern.test(userAgent))?.name
  const os = OSES.find(({ pattern }) => pattern.test(userAgent))?.name
  if (browser && os) return `${browser} ${t('push.settings.on')} ${os}`
  return browser ?? os ?? t('push.settings.unknownDevice')
}

// createdAt/lastUsedAt are unix seconds (backend `long`)
const formatTimestamp = (value: number | null): string => {
  if (!value) return t('push.settings.unknownDevice')
  return formatDateString(new Date(value * 1000))
}

const toDeviceRow = (item: PushSubscriptionListItem): DeviceRow => ({
  id: item.id,
  endpoint: item.endpoint,
  deviceName: parseDeviceName(item.userAgent),
  registeredOn: formatTimestamp(item.createdAt),
  isCurrent: item.userAgent !== null && item.userAgent === currentUa,
})

const loadDevices = async (): Promise<void> => {
  isLoadingDevices.value = true
  devices.value = (await fetchSubscriptions()).map(toDeviceRow)
  isLoadingDevices.value = false
}

const onToggle = async (value: boolean): Promise<void> => {
  isLoading.value = true
  try {
    const ok = value ? await subscribe() : await unsubscribe()
    if (!ok) await checkConfiguration()
  }
  finally {
    isLoading.value = false
  }
}

const removeDevice = async (endpoint: string): Promise<void> => {
  removingEndpoint.value = endpoint
  const removed = await removeSubscription(endpoint)
  if (removed) {
    devices.value = devices.value.filter(device => device.endpoint !== endpoint)
  }
  removingEndpoint.value = null
}

onMounted(() => {
  if (pushEnabled.value) void loadDevices()
})

watch(pushEnabled, (enabled) => {
  if (enabled) void loadDevices()
  else devices.value = []
})
</script>
