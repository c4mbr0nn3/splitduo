<template>
  <UCard
    class="sd-surface"
    :class="isSettlement ? '' : 'sd-surface-hover cursor-pointer'"
    :ui="{ body: isSettlement ? 'p-3 sm:p-3.5' : 'p-4 sm:p-5' }"
  >
    <component
      :is="isSettlement ? 'div' : resolveComponent('NuxtLink')"
      v-bind="isSettlement ? {} : { to: `/groups/${expense.groupId}/expenses/${expense.id}/edit` }"
      class="block"
    >
      <!-- Settlement: compact ledger entry -->
      <template v-if="isSettlement">
        <div class="flex items-center justify-between gap-3">
          <div class="flex items-center gap-2 min-w-0">
            <h3
              v-if="settlementHasReceiver"
              class="flex items-center min-w-0 text-base font-medium text-highlighted"
            >
              <span class="truncate">{{ settlementFromName }}</span>
              <UIcon
                name="i-lucide-arrow-right"
                class="size-4 shrink-0 text-muted self-center mx-0.5"
                aria-hidden="true"
              />
              <span class="truncate">{{ settlementToName }}</span>
            </h3>
            <h3
              v-else
              class="text-base font-medium text-highlighted truncate"
            >
              {{ expense.title }}
            </h3>
          </div>
          <div class="flex items-center gap-1.5 shrink-0">
            <span class="text-lg font-semibold sd-tabular text-highlighted">
              {{ formatCurrency(expense.amount) }}
            </span>
            <span @click.stop>
              <UiButtonDropdown
                icon-only
                dropdown-icon="i-lucide-ellipsis-vertical"
                size="md"
                square
                variant="ghost"
                color="neutral"
                :items="dropdownItems"
                :disabled="isDeletingExpense"
              />
            </span>
          </div>
        </div>
        <div class="flex flex-wrap items-center gap-2 mt-1.5">
          <p class="text-xs text-dimmed">
            {{ formattedDate }}
          </p>
          <UBadge
            color="info"
            variant="subtle"
            size="sm"
            icon="i-lucide-arrow-right-left"
          >
            {{ t('expenses.settlementBadge') }}
          </UBadge>
          <UBadge
            v-if="paymentModeName"
            color="neutral"
            variant="outline"
            size="sm"
          >
            {{ paymentModeName }}
          </UBadge>
        </div>
      </template>

      <!-- Expense -->
      <template v-else>
        <!-- Row 1: date + payer · amount -->
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs text-dimmed">
              {{ formattedDate }}
            </p>
            <p class="text-sm text-muted mt-0.5">
              {{ payerName }}
            </p>
          </div>
          <div class="flex items-center gap-1.5 shrink-0">
            <span
              class="size-2 rounded-full"
              :class="youOwe ? 'bg-error' : 'bg-success'"
              aria-hidden="true"
            />
            <span class="text-lg font-semibold sd-tabular text-highlighted">
              {{ formatCurrency(expense.amount) }}
            </span>
          </div>
        </div>

        <!-- Row 2: title -->
        <h3 class="text-base font-medium text-highlighted mt-2 truncate">
          {{ expense.title }}
        </h3>

        <!-- Row 3: metadata -->
        <div class="flex flex-wrap items-center gap-2 mt-3">
          <UBadge
            :color="categoryColor"
            variant="subtle"
            size="sm"
          >
            {{ categoryName }}
          </UBadge>
          <UBadge
            v-if="paymentModeName"
            color="neutral"
            variant="outline"
            size="sm"
          >
            {{ paymentModeName }}
          </UBadge>
          <UBadge
            v-if="attachmentCount > 0"
            color="neutral"
            variant="outline"
            size="sm"
            icon="i-lucide-paperclip"
            :title="t('expenses.attachments.hasAttachments')"
          >
            {{ attachmentCount }}
          </UBadge>
          <div class="w-full">
            <span
              class="text-xs text-dimmed truncate max-w-[12rem] sm:max-w-[16rem]"
              :title="splitLabel"
            >
              {{ splitLabel }}
            </span>
          </div>
        </div>
      </template>
    </component>
    <div
      v-if="!isSettlement"
      class="flex items-center justify-end -mt-2"
    >
      <span @click.stop>
        <UiButtonDropdown
          icon-only
          dropdown-icon="i-lucide-ellipsis-vertical"
          size="md"
          square
          variant="ghost"
          color="neutral"
          :items="dropdownItems"
          :disabled="isDeletingExpense"
        />
      </span>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { DeepReadonly } from 'vue'
import type { Alias, Expense, User } from '~/types/domain'

const { t } = useI18n()

interface Props {
  expense: Expense
  currentUser?: User | null
  currentUserAliasId?: string | null
  aliases?: DeepReadonly<Alias[]>
}
const props = withDefaults(defineProps<Props>(), {
  currentUser: null,
})

const { getCategoryName } = useCategories()
const { getPaymentModeName } = usePaymentModes()
const modal = useModal()
const { deleteExpense: deleteExpenseApi } = useExpenses(props.expense.groupId)

const isDeletingExpense = ref(false)

// Computed properties for category and payment mode names
const categoryName = computed(() => {
  if (isSettlement.value || props.expense.categoryId == null) return ''
  return getCategoryName(Number(props.expense.categoryId))
})

const paymentModeName = computed(() => {
  return getPaymentModeName(Number(props.expense.paymentModeId))
})

const isSettlement = computed(() => Number(props.expense.expenseTypeId) === 1)

const settlementFromName = computed(() => props.expense.paidByUser.firstName || '')

const settlementToName = computed(() => {
  if (props.expense.aliasSplits?.length) {
    return props.expense.aliasSplits[0]?.aliasName || ''
  }
  return props.expense.splits?.[0]?.user?.firstName || ''
})

const settlementHasReceiver = computed(() => Boolean(settlementToName.value))

const formattedDate = computed(() => {
  return formatDateString(props.expense.expenseDate)
})

const categoryColor = computed(() => {
  const colors: Record<string, string> = {
    groceries: 'success',
    transportation: 'primary',
    utilities: 'warning',
    entertainment: 'secondary',
    health: 'error',
    education: 'info',
    travel: 'secondary',
    shopping: 'error',
    housing: 'warning',
    dining: 'warning',
    other: 'neutral',
    settlement: 'info',
  }
  return (colors[categoryName.value.toLowerCase()] || colors.other) as 'success' | 'error' | 'primary' | 'secondary' | 'info' | 'warning' | 'neutral'
})

const youOwe = computed(() => {
  if (props.currentUserAliasId && props.aliases?.length) {
    const myAlias = props.aliases.find(a => a.id === props.currentUserAliasId)
    const paidByMyAlias = myAlias?.members?.some(m => m.id === props.expense.paidByUserId)
    return !paidByMyAlias
  }
  return props.currentUser && props.expense.paidByUserId !== props.currentUser.id
})

const payerName = computed(() => {
  if (props.expense.paidByUserId === props.currentUser?.id) return t('expenses.you')
  return `${props.expense.paidByUser.firstName} ${props.expense.paidByUser.lastName}`
})

const isAliasSplit = computed(() => {
  return Array.isArray(props.expense.aliasSplits) && props.expense.aliasSplits.length > 0
})

const splitCount = computed(() => {
  if (isAliasSplit.value) return (props.expense.aliasSplits || []).length
  return props.expense.splits?.length || 0
})

const attachmentCount = computed(() => Number(props.expense.attachmentCount || 0))

const splitLabel = computed(() => {
  if (!isAliasSplit.value) return t('expenses.peopleCount', { count: splitCount.value })
  const names = (props.expense.aliasSplits || []).map(s => s.aliasName).join(', ')
  return t('expenses.splitAmong', { names })
})

const emit = defineEmits<{
  'expense-deleted': [expenseId: string]
}>()

const confirmDeleteExpense = async () => {
  const confirmed = await modal.error({
    title: t('expenses.deleteTitle'),
    subtitle: t('expenses.deleteConfirm'),
    content: t('expenses.deleteContent'),
    confirmText: t('expenses.deleteButton'),
    cancelText: t('common.cancel'),
  })

  if (confirmed) {
    await deleteExpense()
  }
}

const deleteExpense = async () => {
  isDeletingExpense.value = true
  try {
    await deleteExpenseApi(props.expense.id)
    emit('expense-deleted', props.expense.id)
  }
  catch (error) {
    console.error('Failed to delete expense:', error)
  }
  finally {
    isDeletingExpense.value = false
  }
}

const navigateToEdit = () => {
  if (!props.expense.id) return
  navigateTo(`/groups/${props.expense.groupId}/expenses/${props.expense.id}/edit`)
}

const dropdownItems = computed(() => {
  const items: Array<{ label?: string, icon?: string, color?: string, onSelect?: () => void, type?: string }> = []
  if (!isSettlement.value) {
    items.push({
      label: t('expenses.edit'),
      icon: 'i-lucide-edit-2',
      color: 'info',
      onSelect: navigateToEdit,
    })
    items.push({
      type: 'separator',
    })
  }
  items.push({
    label: t('expenses.delete'),
    icon: 'i-lucide-trash-2',
    color: 'error',
    onSelect: confirmDeleteExpense,
  })
  return items
})
</script>
