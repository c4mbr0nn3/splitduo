<template>
  <UModal
    v-model:open="isOpen"
    :title="$t('expenses.scan.title')"
    :close="false"
    :dismissible="false"
    :fullscreen="isSmallScreen"
    :ui="{
      content: 'sm:max-w-md',
      body: 'flex-1 overflow-y-auto flex flex-col justify-center p-4 sm:p-6',
      footer: 'justify-end gap-2',
    }"
  >
    <template #body>
      <div
        role="status"
        aria-live="polite"
        class="sd-fade-in flex w-full items-center gap-4"
      >
        <!-- Receipt anchor: compact thumbnail beside the status, so the photo stays
             present without dominating. Fixed size keeps the modal height stable
             across phases; the mat/placeholder keeps it anchored when the image
             hasn't loaded yet. -->
        <div
          class="relative flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-default bg-elevated"
          :class="phase === 'error' ? 'ring-2 ring-inset ring-error' : ''"
        >
          <img
            v-if="receiptImageUrl"
            :src="receiptImageUrl"
            :alt="$t('expenses.scan.imageAlt')"
            class="size-full rounded-md object-cover p-0.5"
            :class="phase === 'error' ? 'opacity-40' : 'opacity-100'"
          >
          <UIcon
            v-else
            name="i-lucide-receipt"
            class="size-8 text-dimmed"
          />

          <!-- Success confirmation: scrim + check badge over the thumbnail -->
          <div
            v-if="phase === 'success'"
            class="sd-fade-in absolute inset-0 flex items-center justify-center bg-inverted/60"
          >
            <span class="sd-fade-in flex size-8 items-center justify-center rounded-full bg-success shadow-md">
              <UIcon
                name="i-lucide-check"
                class="size-5 text-inverted"
              />
            </span>
          </div>
        </div>

        <!-- Status column: honest two-phase progress only — no fake percentages or steps -->
        <div class="flex min-w-0 flex-1 flex-col gap-2 text-left">
          <UProgress
            v-if="phase === 'preparing' || phase === 'analyzing'"
            class="w-full"
            size="xs"
            color="primary"
          />

          <template v-if="phase === 'preparing' || phase === 'analyzing' || phase === 'success'">
            <div class="flex items-center gap-2">
              <UIcon
                :name="phase === 'success' ? 'i-lucide-check-circle' : 'i-lucide-loader-2'"
                class="size-4 shrink-0"
                :class="phase === 'success' ? 'text-success' : 'motion-safe:animate-spin text-primary'"
              />
              <p class="text-sm text-muted">
                {{ statusText }}
              </p>
            </div>
            <p
              v-if="phase === 'analyzing'"
              class="sd-fade-in text-xs text-muted"
            >
              {{ $t('expenses.scan.analyzingHint') }}
            </p>
            <p
              v-if="phase === 'success'"
              class="sd-fade-in text-xs text-muted"
            >
              {{ $t('expenses.scan.successHint') }}
            </p>
          </template>

          <template v-else-if="phase === 'error'">
            <div class="flex items-center gap-2">
              <UIcon
                name="i-lucide-alert-triangle"
                class="size-4 shrink-0 text-warning"
              />
              <p class="text-sm font-medium text-highlighted">
                {{ $t('expenses.scan.errorTitle') }}
              </p>
            </div>
            <p class="sd-fade-in line-clamp-2 text-xs text-muted">
              {{ scanError || $t('expenses.scan.errorHint') }}
            </p>
          </template>
        </div>
      </div>
    </template>
    <template
      v-if="phase !== 'idle' && phase !== 'success'"
      #footer
    >
      <div class="flex w-full justify-end gap-2">
        <UButton
          v-if="phase === 'preparing' || phase === 'analyzing'"
          variant="outline"
          color="neutral"
          icon="i-lucide-x"
          :label="$t('expenses.scan.cancel')"
          @click="onCancel"
        />
        <template v-else-if="phase === 'error'">
          <UButton
            variant="outline"
            color="neutral"
            :label="$t('common.close')"
            @click="onClose"
          />
          <UButton
            color="primary"
            icon="i-lucide-rotate-cw"
            :label="$t('common.retry')"
            @click="onRetry"
          />
        </template>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { useMediaQuery } from '@vueuse/core'

interface Props {
  modelValue: boolean
}
const props = defineProps<Props>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

const { phase, scanError, receiptImageUrl, cancelScan, retryScan, dismissScan } = useReceiptScan()
const { t } = useI18n()

const isSmallScreen = useMediaQuery('(max-width: 639px)')

const isOpen = computed({
  get: () => props.modelValue,
  set: val => emit('update:modelValue', val),
})

const statusText = computed(() => {
  if (phase.value === 'preparing') return t('expenses.scan.preparing')
  if (phase.value === 'analyzing') return t('expenses.scan.analyzing')
  if (phase.value === 'success') return t('expenses.scan.success')
  return ''
})

const onCancel = () => {
  cancelScan()
  emit('update:modelValue', false)
}

const onRetry = () => {
  void retryScan()
}

const onClose = () => {
  dismissScan()
  emit('update:modelValue', false)
}
</script>
