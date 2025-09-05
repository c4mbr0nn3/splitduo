<template>
  <UCard class="hover:shadow-md transition-shadow">
    <!-- Row 1: Title, Description, Amount & Date -->
    <div class="flex justify-between text-xs text-dimmed items-start gap-4 mb-3">
      <span>
        Paid by {{ expense.paidByUserId === currentUser?.id ? 'you' : `${expense.paidByUser.firstName} ${expense.paidByUser.lastName}` }}
      </span>
      <div>
        {{ formattedDate }}
      </div>
    </div>
    <div class="flex justify-between items-start gap-4 mb-1">
      <div class="flex min-w-0">
        <h4 class="font-medium truncate">
          {{ expense.title }}
        </h4>
      </div>
      <div class="flex items-end flex-shrink-0">
        <div
          class="font-bold whitespace-nowrap"
          :class="expense.paidByUserId === currentUser?.id ? 'text-green-600' : 'text-red-600'"
        >
          {{ expense.amount.toFixed(2) }}€
        </div>
      </div>
    </div>
    <div class="flex justify-between items-start gap-4 mb-3">
      <p
        v-if="expense.description"
        class="text-xs text-gray-400 truncate"
      >
        {{ expense.description }}
      </p>
    </div>

    <!-- Row 2: Who paid & Method -->
    <div class="flex flex-wrap items-center gap-4 text-xs text-dimmed mb-2">
      <div class="flex items-center gap-1">
        <UIcon
          name="i-lucide-credit-card"
          class="w-3 h-3"
        />
        <span class="capitalize">{{ expense.paymentMode }}</span>
      </div>
    </div>

    <!-- Row 3: How many people -->
    <div class="flex items-center gap-1 text-xs text-dimmed mb-3">
      <UIcon
        name="i-lucide-users"
        class="w-3 h-3"
      />
      <span>{{ expense.splits.length }} people</span>
    </div>

    <!-- Row 4: Category & Split -->
    <div class="flex justify-between items-center">
      <UBadge
        variant="soft"
        :color="categoryColor"
        :icon="categoryIcon"
        class="capitalize"
      >
        {{ expense.category }}
      </UBadge>
      <UBadge
        v-if="userSplit"
        variant="soft"
        color="neutral"
      >
        Your share: {{ userSplit.splitAmount.toFixed(2) }}€
      </UBadge>
    </div>
  </UCard>
</template>

<script setup>
const props = defineProps({
  expense: {
    type: Object,
    required: true,
  },
  currentUser: {
    type: Object,
    default: null,
  },
})

const formattedDate = computed(() => {
  return new Date(props.expense.expenseDate).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
})

const categoryIcon = computed(() => {
  const icons = {
    groceries: 'i-lucide-shopping-cart',
    transportation: 'i-lucide-car',
    utilities: 'i-lucide-zap',
    entertainment: 'i-lucide-gamepad-2',
    health: 'i-lucide-heart-pulse',
    education: 'i-lucide-graduation-cap',
    travel: 'i-lucide-plane',
    shopping: 'i-lucide-shopping-bag',
    housing: 'i-lucide-home',
    dining: 'i-lucide-utensils',
    other: 'i-lucide-more-horizontal',
  }
  return icons[props.expense.category.toLowerCase()] || icons.other
})

const categoryColor = computed(() => {
  const colors = {
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
  }
  return colors[props.expense.category.toLowerCase()] || colors.other
})

const userSplit = computed(() => {
  if (!props.currentUser) return null
  return props.expense.splits.find(split => split.userId === props.currentUser.id)
})
</script>
