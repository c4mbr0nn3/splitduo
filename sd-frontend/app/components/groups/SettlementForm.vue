<template>
  <UForm
    :state="model"
    :validate="validate"
    class="space-y-4"
    @submit="onSubmit"
  >
    <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <!-- Parties: payer → recipient -->
      <div class="sm:col-span-2">
        <div class="grid grid-cols-1 sm:grid-cols-[1fr_auto_1fr] items-start gap-4">
          <UFormField
            :label="$t('settle.fromLabel')"
            name="from"
            required
          >
            <USelect
              v-model="model.from"
              :items="fromOptions"
              :placeholder="$t('settle.selectFrom')"
              size="lg"
              class="w-full"
            />
          </UFormField>
          <div
            class="hidden sm:flex items-center justify-center self-end"
            aria-hidden="true"
          >
            <span class="flex size-10 items-center justify-center rounded-full bg-primary/10 text-primary">
              <UIcon
                name="i-lucide-arrow-right"
                class="size-5"
              />
            </span>
          </div>
          <div
            class="flex items-center justify-center sm:hidden"
            aria-hidden="true"
          >
            <UIcon
              name="i-lucide-arrow-right"
              class="size-5 text-dimmed rotate-90"
            />
          </div>
          <UFormField
            :label="$t('settle.toLabel')"
            name="to"
            required
          >
            <USelect
              v-model="model.to"
              :items="toOptions"
              :placeholder="$t('settle.selectTo')"
              size="lg"
              class="w-full"
            />
          </UFormField>
        </div>
      </div>

      <UFormField
        :label="$t('settle.amountLabel')"
        name="amount"
        required
      >
        <UInput
          :model-value="displayValue('amount', model.amount)"
          type="text"
          inputmode="decimal"
          :placeholder="$t('settle.amountPlaceholder')"
          size="lg"
          class="w-full"
          @focus="onAmountFocus('amount', model.amount)"
          @update:model-value="v => onAmountInput(v)"
          @blur="onAmountBlur"
        />
      </UFormField>
      <UFormField
        :label="$t('settle.paymentModeLabel')"
        name="paymentModeId"
        required
      >
        <USelect
          v-model="model.paymentModeId"
          :items="paymentModeOptions"
          :placeholder="$t('expenses.selectPaymentMethod')"
          size="lg"
          :loading="isLoadingPaymentModes"
          class="w-full"
        />
      </UFormField>
      <UFormField
        class="sm:col-span-2"
        :label="$t('settle.dateLabel')"
        name="date"
        required
      >
        <UiInputDate
          v-model="model.date"
          size="lg"
        />
      </UFormField>
      <UFormField
        class="sm:col-span-2"
        :label="$t('settle.descriptionLabel')"
        name="description"
      >
        <UTextarea
          v-model="model.description"
          :rows="2"
          size="lg"
          class="w-full"
          maxlength="500"
        />
      </UFormField>
    </div>

    <div class="flex flex-col-reverse sm:flex-row sm:justify-end gap-3 mt-6">
      <UButton
        :label="$t('common.cancel')"
        variant="ghost"
        color="neutral"
        size="lg"
        class="grow sm:grow-0"
        @click="emit('cancel')"
      />
      <UButton
        type="submit"
        size="lg"
        :label="$t('settle.confirmButton')"
        :loading="loading"
        class="grow sm:grow-0"
      />
    </div>
  </UForm>
</template>

<script setup lang="ts">
import type { CreateSettlementRequest } from '~/types/domain'
import { formatAmount } from '~/utils/currency'

interface SettlementPrefill {
  from?: string
  to?: string
  fromAlias?: string
  toAlias?: string
  amount?: number
}

// Structural prop types matching the shape the form actually consumes —
// composable state arrives via readonly() (deep-frozen), so the domain Alias
// type (mutable nested members) can't be used directly here.
interface AliasOption {
  readonly id: string
  readonly name: string
  readonly members?: readonly { readonly id?: string }[]
}

interface MemberOption {
  readonly userId: string
  readonly user: { readonly firstName?: string | null, readonly lastName?: string | null }
}

interface Props {
  groupId: string
  isAliasMode: boolean
  members: readonly MemberOption[]
  aliases: readonly AliasOption[]
  currentUserId: string
  prefill?: SettlementPrefill
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  prefill: undefined,
  loading: false,
})

const emit = defineEmits<{
  submit: [payload: CreateSettlementRequest]
  cancel: []
}>()

const { t } = useI18n()
const { paymentModes, isLoading: isLoadingPaymentModes } = usePaymentModes()

// Form state — dates are plain YYYY-MM-DD strings; the amount stays numeric in
// the model and is only formatted while displayed (see amount machinery below).
const model = reactive({
  from: '',
  to: '',
  amount: null as number | null,
  date: new Date().toISOString().slice(0, 10),
  description: '',
  paymentModeId: 4,
})

// Select options ---------------------------------------------------------------

interface SelectOption {
  value: string | number
  label: string
}

const fromOptions = computed<SelectOption[]>(() => {
  if (props.isAliasMode) {
    return props.aliases.map(alias => ({
      value: alias.id,
      label: alias.name,
    }))
  }
  return props.members.map(member => ({
    value: member.userId,
    label: [member.user.firstName, member.user.lastName].filter(Boolean).join(' '),
  }))
})

const toOptions = computed<SelectOption[]>(() =>
  fromOptions.value.filter(option => option.value !== model.from),
)

const paymentModeOptions = computed<SelectOption[]>(() =>
  paymentModes.value.map(mode => ({
    value: mode.id,
    label: mode.name,
  })),
)

// Amount input handling ---------------------------------------------------------
// Same contract as ExpenseForm: comma and dot both act as the decimal separator,
// non-numeric characters are stripped, and the value is formatted to 2dp on
// blur so the formatter never fights typing while the field is focused.

const parseAmount = (raw: string | null | undefined): number | null => {
  if (raw === '' || raw === null || raw === undefined) return null

  let normalized = String(raw).replace(/,/g, '.')
  normalized = normalized.replace(/[^\d.]/g, '')

  const firstDot = normalized.indexOf('.')
  if (firstDot !== -1) {
    const before = normalized.slice(0, firstDot + 1)
    const after = normalized.slice(firstDot + 1).replace(/\./g, '')
    normalized = before + after
  }

  if (normalized.endsWith('.')) normalized = normalized.slice(0, -1)

  const n = Number(normalized)
  if (!Number.isFinite(n) || normalized === '') return null
  return n
}

const editingField = ref<string | null>(null) // id of the field currently being edited
const editingValue = ref<string>('') // raw string shown while editing

const displayValue = (id: string, numericVal: string | number | null | undefined): string =>
  editingField.value === id
    ? editingValue.value
    : (numericVal == null ? '' : formatAmount(numericVal))

const onAmountFocus = (id: string, numericVal: string | number | null | undefined): void => {
  editingField.value = id
  editingValue.value = numericVal == null ? '' : String(numericVal)
}

const onAmountInput = (raw: string): void => {
  editingValue.value = raw
  model.amount = parseAmount(raw)
}

const onAmountBlur = (): void => {
  editingField.value = null
  editingValue.value = ''
}

// Defaults + prefill ------------------------------------------------------------

onMounted(() => {
  // Defaults: from = the current user (or their alias); to stays empty — a
  // conscious choice so the recipient is always a deliberate pick.
  if (props.isAliasMode) {
    const userAlias = props.aliases.find(a => a.members?.some(m => m.id === props.currentUserId))
    if (userAlias) model.from = userAlias.id
  }
  else if (props.members.some(m => m.userId === props.currentUserId)) {
    model.from = props.currentUserId
  }

  // Prefill overrides defaults — unresolvable values are dropped silently.
  const pf = props.prefill
  if (pf?.from && !props.isAliasMode && fromOptions.value.some(o => o.value === pf.from)) {
    model.from = pf.from
  }
  if (pf?.fromAlias && props.isAliasMode && fromOptions.value.some(o => o.value === pf.fromAlias)) {
    model.from = pf.fromAlias
  }
  if (pf?.to && !props.isAliasMode && toOptions.value.some(o => o.value === pf.to)) {
    model.to = pf.to
  }
  if (pf?.toAlias && props.isAliasMode && toOptions.value.some(o => o.value === pf.toAlias)) {
    model.to = pf.toAlias
  }
  if (pf?.amount && pf.amount > 0) {
    model.amount = pf.amount
  }
})

// Validation ---------------------------------------------------------------------

interface ValidationError {
  name: string
  message: string
}

const validate = (): ValidationError[] => {
  const errors: ValidationError[] = []
  if (!model.from) {
    errors.push({ name: 'from', message: t('settle.fromRequired') })
  }
  else if (props.isAliasMode && !resolveAliasFromUserId()) {
    // Selected from-alias has no resolvable payer (no members).
    errors.push({ name: 'from', message: t('settle.fromRequired') })
  }
  if (!model.to) {
    errors.push({ name: 'to', message: t('settle.toRequired') })
  }
  if (model.from && model.to && model.from === model.to) {
    errors.push({ name: 'to', message: t('settle.cannotSettleSelf') })
  }
  if (!(model.amount !== null && model.amount > 0)) {
    errors.push({ name: 'amount', message: t('settle.amountRequired') })
  }
  if (!model.date) {
    errors.push({ name: 'date', message: t('settle.dateRequired') })
  }
  return errors
}

// Submit --------------------------------------------------------------------------

// Alias payer resolution is keyed off the SELECTED from-alias: the current user
// if they are a member, otherwise the alias's first member.
const resolveAliasFromUserId = (): string | null => {
  const alias = props.aliases.find(a => a.id === model.from)
  if (!alias) return null
  if (alias.members?.some(m => m.id === props.currentUserId)) return props.currentUserId
  return alias.members?.[0]?.id ?? null
}

const onSubmit = (): void => {
  if (props.isAliasMode) {
    const fromUserId = resolveAliasFromUserId()
    if (!fromUserId) return // No resolvable payer — the from field is already flagged by validate().

    emit('submit', {
      fromUserId,
      fromAliasId: model.from,
      toAliasId: model.to || null,
      amount: model.amount ?? 0,
      date: model.date,
      description: model.description || null,
      paymentModeId: model.paymentModeId,
    })
    return
  }

  emit('submit', {
    fromUserId: model.from,
    toUserId: model.to,
    amount: model.amount ?? 0,
    date: model.date,
    description: model.description || null,
    paymentModeId: model.paymentModeId,
  })
}
</script>
