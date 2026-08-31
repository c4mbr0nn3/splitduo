<template>
  <div class="flex flex-col items-center py-6 sm:py-8">
    <UiLoadingSpinner
      v-if="isLoading"
      :text="$t('settle.loading')"
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-users"
      :title="$t('groups.unableToLoad')"
    >
      <template #action>
        <UButton
          color="primary"
          variant="outline"
          size="sm"
          @click="retryLoad"
        >
          {{ $t('groups.retry') }}
        </UButton>
      </template>
    </UiEmptyState>

    <div
      v-else
      class="w-full max-w-2xl"
    >
      <UiCardHeader
        :title="$t('settle.confirmTitle')"
        :subtitle="$t('settle.confirmSubtitle')"
        :back-to="`/groups/${groupId}/settle`"
        class="mb-6"
      />
      <UCard
        class="sd-surface w-full"
        :ui="{ body: 'p-4 sm:p-6' }"
      >
        <GroupsSettlementForm
          :group-id="groupId"
          :is-alias-mode="isAliasMode"
          :members="members"
          :aliases="aliases"
          :current-user-id="currentUserId"
          :prefill="prefill"
          :loading="isSubmitting"
          @submit="onSubmit"
          @cancel="onCancel"
        />
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { CreateSettlementRequest, GroupMember } from '~/types/domain'

definePageMeta({
  middleware: 'auth',
})

const route = useRoute()
const groupId = String(route.params.id)
const { t } = useI18n()
const { user } = useAuth()
const { createSettlement } = useSettlements(groupId)
const { isAliasMode, fetchGroup } = useBalances(groupId)
const { fetchGroupMembers } = useGroups()
const { aliases, fetchAliases } = useAliases()

// Query params are optional prefill only — no validation/redirect when missing.
const prefill = computed(() => ({
  from: typeof route.query.from === 'string' ? route.query.from : undefined,
  to: typeof route.query.to === 'string' ? route.query.to : undefined,
  fromAlias: typeof route.query.fromAlias === 'string' ? route.query.fromAlias : undefined,
  toAlias: typeof route.query.toAlias === 'string' ? route.query.toAlias : undefined,
  amount: Number(route.query.amount) || undefined,
}))

const currentUserId = computed(() => user.value?.id || '')

const members = ref<GroupMember[]>([])

const isLoading = ref(true)
const loadError = ref(false)
const isSubmitting = ref(false)

const retryLoad = async () => {
  loadError.value = false
  isLoading.value = true
  await loadData()
}

const loadData = async () => {
  try {
    await fetchGroup(groupId)
    if (isAliasMode.value) {
      await fetchAliases(groupId)
    }
    else {
      members.value = (await fetchGroupMembers(groupId)) ?? []
    }
  }
  catch {
    loadError.value = true
  }
  finally {
    isLoading.value = false
  }
}

onMounted(async () => {
  await loadData()
})

const onSubmit = async (payload: CreateSettlementRequest) => {
  isSubmitting.value = true
  try {
    await createSettlement(payload)
    await navigateTo(`/groups/${groupId}/settle`)
  }
  catch {
    // Toasts handled by the composable — stay on the page.
  }
  finally {
    isSubmitting.value = false
  }
}

const onCancel = () => {
  const { goBack } = useSmartBack(`/groups/${groupId}/settle`)
  goBack()
}

useHead({
  title: computed(() => t('settle.confirmTitle')),
})
</script>
