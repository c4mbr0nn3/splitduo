<template>
  <UModal v-model:open="isOpen">
    <template #header>
      <div class="flex items-start gap-3">
        <div class="shrink-0">
          <UIcon
            name="i-lucide-bell"
            class="size-6 text-primary"
          />
        </div>
        <div class="flex-1 min-w-0">
          <h3 class="text-lg font-semibold">
            {{ $t('push.optIn.title') }}
          </h3>
        </div>
      </div>
    </template>

    <template #body>
      <p class="text-sm text-muted">
        {{ message }}
      </p>
    </template>

    <template #footer>
      <div class="flex gap-2 w-full">
        <UButton
          color="neutral"
          variant="outline"
          class="ml-auto"
          @click="handleNotNow"
        >
          {{ $t('push.optIn.notNow') }}
        </UButton>
        <UButton
          :loading="isSubscribing"
          @click="handleEnable"
        >
          {{ $t('push.optIn.enable') }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
interface Props {
  groupName?: string
}

const props = withDefaults(defineProps<Props>(), {
  groupName: '',
})

const { t } = useI18n()

const emit = defineEmits<{
  close: [value: boolean]
}>()

const isOpen = defineModel<boolean>('open', { default: true })

// Set when a handler already emitted `close`, so the watcher below doesn't
// emit a second time for the same close (the promise must resolve exactly once).
const didEmitClose = ref(false)

const { subscribe } = usePush()

const isSubscribing = ref(false)

// Generic variant when no group name is available
const message = computed(() =>
  props.groupName
    ? t('push.optIn.message', { groupName: props.groupName })
    : t('push.optIn.messageGeneric'),
)

const handleEnable = async () => {
  // Awaited within the click handler to preserve the user-gesture chain
  // required by iOS Safari for the permission prompt.
  isSubscribing.value = true
  try {
    await subscribe()
  }
  catch {
    // Outcome communicated via composable toasts
  }
  isSubscribing.value = false
  // Close on success or failure either way — toasts communicate the outcome
  didEmitClose.value = true
  emit('close', true)
  isOpen.value = false
}

const handleNotNow = () => {
  // Dismissal is only persisted via the explicit "Not now" action — closing
  // through the X / backdrop / escape leaves future prompts possible.
  localStorage.setItem('push-prompt-dismissed', '1')
  didEmitClose.value = true
  emit('close', false)
  isOpen.value = false
}

// X / backdrop / Escape only emit `update:open` on UModal — forward them as
// `close` so `useOverlay().open()` always resolves. "Not now" semantics are
// preserved: this path closes without persisting the dismissal flag.
watch(isOpen, (open) => {
  if (open) {
    didEmitClose.value = false
    return
  }
  if (!didEmitClose.value) {
    didEmitClose.value = true
    emit('close', false)
  }
})
</script>
