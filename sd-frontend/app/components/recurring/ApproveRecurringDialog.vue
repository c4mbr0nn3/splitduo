<template>
  <UModal
    v-model:open="isOpen"
    :dismissible="!isSubmitting"
  >
    <template #header>
      <div class="flex items-start gap-3">
        <UIcon
          name="i-lucide-check-circle"
          class="size-6 text-success shrink-0 mt-0.5"
        />
        <div class="flex-1 min-w-0">
          <h3 class="text-lg font-semibold">
            {{ $t('recurring.approveTitle') }}
          </h3>
          <p class="text-sm text-muted">
            {{ instance.templateTitle }} · {{ formatDateString(instance.periodDate) }}
          </p>
        </div>
      </div>
    </template>

    <template #body>
      <div class="space-y-4">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <UFormField
            :label="$t('expenses.amount')"
            name="amount"
            required
          >
            <UInput
              v-model="amount"
              type="text"
              inputmode="decimal"
              :placeholder="$t('expenses.enterAmount')"
              size="lg"
              class="w-full"
            />
          </UFormField>
          <UFormField
            :label="$t('recurring.expenseDate')"
            name="expenseDate"
            required
          >
            <UiInputDate
              v-model="expenseDate"
              size="lg"
            />
          </UFormField>
        </div>

        <!-- Split amounts -->
        <UFormField
          name="splits"
          :error="splitsError ?? undefined"
          class="w-full"
        >
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <p class="text-sm font-medium text-muted">
                {{ isAliasMode ? $t('expenses.splitBetweenAliases') : $t('expenses.splitBetween') }}
              </p>
              <UButton
                :label="$t('expenses.splitEqually')"
                icon="i-lucide-equal"
                variant="ghost"
                color="primary"
                size="sm"
                :disabled="splitMillisList.length < 2 || amountMillis === 0"
                @click="splitEqually"
              />
            </div>
            <div class="space-y-0">
              <template v-if="isAliasMode">
                <div
                  v-for="split in aliasSplits"
                  :key="split.aliasId"
                  class="grid grid-cols-[1fr_auto] items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0"
                >
                  <span class="text-sm truncate">
                    {{ aliasName(split.aliasId) }}
                  </span>
                  <UInput
                    v-model.number="split.splitAmount"
                    type="number"
                    size="sm"
                    class="w-24 text-right sd-tabular"
                    :min="0"
                    :step="0.01"
                  />
                </div>
              </template>
              <template v-else>
                <div
                  v-for="split in splits"
                  :key="split.userId"
                  class="grid grid-cols-[1fr_auto] items-center gap-3 py-2 border-b border-[var(--sd-surface-border)] last:border-0"
                >
                  <div class="flex items-center gap-2 min-w-0">
                    <UserAvatar
                      :user="memberUser(split.userId)"
                      size="sm"
                    />
                    <span class="text-sm truncate">
                      {{ memberName(split.userId) }}
                    </span>
                  </div>
                  <UInput
                    v-model.number="split.splitAmount"
                    type="number"
                    size="sm"
                    class="w-24 text-right sd-tabular"
                    :min="0"
                    :step="0.01"
                  />
                </div>
              </template>
            </div>
            <div class="text-xs space-y-1">
              <div class="flex justify-between items-center">
                <span class="text-toned">{{ $t('expenses.splitTotal') }}</span>
                <span class="font-medium">{{ formatCurrency(splitTotal, { fullPrecision: true }) }}</span>
              </div>
              <div
                v-if="remainingMillis !== 0"
                class="flex justify-between items-center font-semibold"
                :class="remainingMillis < 0 ? 'text-error' : 'text-warning'"
              >
                <span>{{ remainingMillis > 0 ? $t('expenses.remaining') : $t('expenses.overBy') }}</span>
                <span>{{ formatCurrency(Math.abs(remaining), { fullPrecision: true }) }}</span>
              </div>
              <div
                v-else
                class="flex justify-between items-center text-success font-semibold"
              >
                <span>{{ $t('expenses.splitsBalanced') }}</span>
              </div>
            </div>
          </div>
        </UFormField>
      </div>
    </template>

    <template #footer>
      <div class="flex gap-2 w-full">
        <UButton
          color="error"
          variant="outline"
          :disabled="isSubmitting"
          class="ml-auto"
          :label="$t('recurring.reject')"
          @click="onReject"
        />
        <UButton
          :loading="isSubmitting"
          :disabled="isSubmitting || !!splitsError"
          :label="$t('recurring.approve')"
          @click="onApprove"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { DeepReadonly } from 'vue'
import type { RecurringExpenseInstance, ApproveRecurringExpenseInstanceRequest, GroupMember, Alias } from '~/types/domain'
import { formatCurrency, splitMillis, fromMillis, toMillis } from '~/utils/currency'

const props = defineProps<{
  instance: DeepReadonly<RecurringExpenseInstance>
  groupId: string
}>()

const emit = defineEmits<{
  approved: [instanceId: string, payload: ApproveRecurringExpenseInstanceRequest]
  rejected: [instanceId: string]
}>()

const { t } = useI18n()

const isOpen = defineModel<boolean>('open', { default: true })

const isSubmitting = ref(false)

const amount = ref('')
const expenseDate = ref<string | null>(null)

interface EditableSplit {
  userId: string
  splitAmount: number
}
interface EditableAliasSplit {
  aliasId: string
  splitAmount: number
}

const splits = ref<EditableSplit[]>([])
const aliasSplits = ref<EditableAliasSplit[]>([])

const groupMembers = ref<GroupMember[]>([])
const aliases = ref<Alias[]>([])

// One flag for alias mode: the instance carries alias splits (fallback to
// fetched aliases in case the payload omits them).
const isAliasMode = computed(() => (props.instance.aliasSplits?.length ?? 0) > 0 || aliases.value.length > 0)

// --- Initialize form state from the instance when it opens ------------------

watch(() => props.instance, async (instance) => {
  if (!instance) return

  amount.value = String(instance.amount ?? '')
  expenseDate.value = instance.periodDate

  if ((instance.aliasSplits?.length ?? 0) > 0) {
    aliasSplits.value = (instance.aliasSplits ?? []).map(s => ({
      aliasId: s.aliasId ?? '',
      splitAmount: Number(s.splitAmount) || 0,
    }))
    splits.value = []
  }
  else {
    splits.value = (instance.splits ?? []).map(s => ({
      userId: s.userId ?? '',
      splitAmount: Number(s.splitAmount) || 0,
    }))
    aliasSplits.value = []
  }
}, { immediate: true })

// Fetch members/aliases for names — needs Nuxt context, so called at setup
// scope with an immediate inner guard.
const { fetchGroupMembers } = useGroups()
const { fetchAliases } = useAliases()

const loadNames = async (): Promise<void> => {
  try {
    if (isAliasMode.value) {
      aliases.value = await fetchAliases(props.groupId) || []
    }
    else {
      groupMembers.value = await fetchGroupMembers(props.groupId) || []
    }
  }
  catch {
    // Names fall back to "Unknown member" — non-fatal
  }
}

onMounted(async () => {
  await loadNames()
})

const memberUser = (userId: string): { id: string, firstName: string, lastName: string, hasAvatar: boolean } => {
  const member = groupMembers.value.find(m => m.userId === userId)
  if (!member) {
    return { id: userId, firstName: t('recurring.unknownMember'), lastName: '', hasAvatar: false }
  }
  return {
    id: userId,
    firstName: member.user.firstName ?? t('recurring.unknownMember'),
    lastName: member.user.lastName ?? '',
    hasAvatar: member.user.hasAvatar ?? false,
  }
}

const memberName = (userId: string): string => {
  const member = groupMembers.value.find(m => m.userId === userId)
  if (!member) return t('recurring.unknownMember')
  return `${member.user.firstName} ${member.user.lastName}`
}

const aliasName = (aliasId: string): string =>
  aliases.value.find(a => a.id === aliasId)?.name || t('recurring.unknownMember')

// --- Totals ------------------------------------------------------------------

const amountMillis = computed(() => toMillis(amount.value ?? ''))

const activeSplits = computed(() => isAliasMode.value ? aliasSplits.value : splits.value)

const splitMillisList = computed(() => activeSplits.value.map(s => toMillis(s.splitAmount ?? 0)))

const splitTotalMillis = computed(() => splitMillisList.value.reduce((total, m) => total + m, 0))

const splitTotal = computed(() => fromMillis(splitTotalMillis.value))

const remainingMillis = computed(() => amountMillis.value - splitTotalMillis.value)
const remaining = computed(() => fromMillis(remainingMillis.value))

// Block approval client-side when edited splits don't sum to the amount —
// same inline feedback pattern as RecurringExpenseForm's validation.
// Only enforced when an amount is entered; an empty amount defers to the backend.
const splitsError = computed<string | null>(() => {
  if (amountMillis.value === 0 || remainingMillis.value === 0) return null
  return t('expenses.splitTotalMustEqual', {
    total: formatCurrency(splitTotal.value, { fullPrecision: true }),
    amount: formatCurrency(fromMillis(amountMillis.value), { fullPrecision: true }),
  })
})

// --- Split actions -----------------------------------------------------------

const splitEqually = (): void => {
  if (amountMillis.value === 0) return
  const shares = splitMillis(amountMillis.value, activeSplits.value.length)
  activeSplits.value.forEach((s, i) => {
    s.splitAmount = fromMillis(shares[i] ?? 0)
  })
}

// --- Actions -----------------------------------------------------------------

const onApprove = async (): Promise<void> => {
  isSubmitting.value = true
  try {
    const payload: ApproveRecurringExpenseInstanceRequest = {
      amount: amount.value || null,
      expenseDate: expenseDate.value ?? null,
    }

    if (isAliasMode.value) {
      payload.aliasSplits = aliasSplits.value
        .map(s => ({ aliasId: s.aliasId ?? '', splitAmount: s.splitAmount || 0 }))
        .filter(s => s.splitAmount > 0 && s.aliasId !== '')
    }
    else {
      payload.splits = splits.value
        .map(s => ({ userId: s.userId ?? '', splitAmount: s.splitAmount || 0 }))
        .filter(s => s.splitAmount > 0 && s.userId !== '')
    }

    emit('approved', props.instance.id, payload)
  }
  finally {
    isSubmitting.value = false
  }
}

const onReject = async (): Promise<void> => {
  emit('rejected', props.instance.id)
}
</script>
