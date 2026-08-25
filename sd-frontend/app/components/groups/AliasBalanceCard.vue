<template>
  <UCard variant="outline">
    <div class="flex items-start justify-between">
      <div class="flex items-center gap-3 min-w-0">
        <UAvatar
          icon="i-lucide-users"
          size="md"
          class="shrink-0 text-primary bg-primary/10"
          :alt="balance.aliasName"
        />
        <div class="min-w-0">
          <p class="font-semibold text-primary truncate">
            {{ balance.aliasName }}
          </p>
          <div
            v-if="balance.members?.length"
            class="flex items-center gap-1.5 flex-wrap mt-1"
          >
            <UserAvatar
              v-for="member in visibleMembers"
              :key="member.id"
              :user="member as UserBasicInfo"
              size="xs"
              class="shrink-0"
            />
            <span
              v-if="balance.members.length > 3"
              class="text-xs text-muted"
            >
              +{{ balance.members.length - 3 }}
            </span>
          </div>
        </div>
      </div>
      <span
        class="font-bold text-lg shrink-0"
        :class="Number(balance.balance) >= 0 ? 'text-success' : 'text-error'"
      >
        {{ Number(balance.balance) >= 0 ? '+' : '' }}{{ formatCurrency(balance.balance) }}
      </span>
    </div>
    <div class="grid grid-cols-2 gap-4 text-center mt-3">
      <div>
        <p class="text-xs text-dimmed mb-1">
          {{ $t('stats.paid') }}
        </p>
        <p class="font-semibold text-success sd-tabular">
          {{ formatCurrency(balance.totalPaid) }}
        </p>
      </div>
      <div>
        <p class="text-xs text-dimmed mb-1">
          {{ $t('stats.owes') }}
        </p>
        <p class="font-semibold text-warning sd-tabular">
          {{ formatCurrency(balance.totalOwed) }}
        </p>
      </div>
    </div>
    <div
      v-if="balance.isSingleton"
      class="mt-3"
    >
      <UBadge
        variant="soft"
        color="secondary"
        :label="$t('members.singleton')"
        size="xs"
      />
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { AliasBalance, UserBasicInfo } from '~/types/domain'
import { formatCurrency } from '~/utils/currency'

interface Props {
  balance: AliasBalance
}
const props = defineProps<Props>()

const visibleMembers = computed(() => {
  return props.balance.members?.slice(0, 3) || []
})
</script>
