<template>
  <div class="space-y-6">
    <!-- Finalize banner -->
    <UCard
      v-if="isGroupAdmin && !group?.aliasSetupFinalized"
      variant="soft"
      color="warning"
      :ui="{ body: 'p-4 sm:p-5' }"
    >
      <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div class="flex items-start gap-3">
          <UIcon
            name="i-lucide-alert-triangle"
            class="size-6 text-warning shrink-0"
          />
          <div>
            <p class="font-semibold text-highlighted">
              Finalize alias setup
            </p>
            <p class="text-sm text-muted mt-1">
              Aliases must be finalized before expenses can be created in this group.
            </p>
            <p
              v-if="!hasMultiPersonAlias"
              class="text-sm text-warning mt-1"
            >
              Create at least one alias with 2+ members to finalize.
            </p>
          </div>
        </div>
        <UButton
          label="Finalize Alias Setup"
          color="warning"
          variant="solid"
          :loading="isFinalizing"
          :disabled="!hasMultiPersonAlias || isFinalizing"
          @click="$emit('finalize')"
        />
      </div>
    </UCard>

    <!-- Aliases -->
    <div class="space-y-4">
      <UCard
        v-for="alias in aliasesWithMembers"
        :key="alias.id"
        variant="outline"
        class="sd-surface"
      >
        <div class="space-y-4">
          <div class="flex items-start justify-between gap-3">
            <div class="flex items-center gap-2 flex-wrap min-w-0">
              <UIcon
                name="i-lucide-layers"
                class="size-5 text-secondary shrink-0"
              />
              <h4 class="text-base font-semibold text-highlighted truncate">
                {{ alias.name }}
              </h4>
              <UBadge
                v-if="alias.isSingleton"
                variant="soft"
                color="secondary"
                label="singleton"
                size="xs"
              />
            </div>
            <div
              v-if="isGroupAdmin"
              class="flex items-center gap-2 shrink-0"
            >
              <UButton
                icon="i-lucide-pencil"
                variant="ghost"
                size="sm"
                square
                @click="$emit('rename', alias)"
              />
              <UButton
                icon="i-lucide-trash-2"
                variant="ghost"
                color="error"
                size="sm"
                square
                :loading="aliasLoading"
                @click="$emit('delete', alias)"
              />
            </div>
          </div>

          <div class="space-y-1">
            <p
              v-if="!alias.members?.length"
              class="text-sm text-muted"
            >
              No members
            </p>
            <GroupsMembersRow
              v-for="member in alias.members"
              :key="member.id"
              :member="member"
            >
              <template
                v-if="isGroupAdmin"
                #actions
              >
                <UButton
                  icon="i-lucide-user-minus"
                  variant="ghost"
                  color="neutral"
                  size="xs"
                  square
                  :loading="aliasLoading"
                  @click="$emit('remove', alias, member.id)"
                />
              </template>
            </GroupsMembersRow>
          </div>

          <UFormField
            v-if="isGroupAdmin && availableMembers.length"
            label="Add member"
            :name="`assign-${alias.id}`"
          >
            <div class="flex items-center gap-2">
              <USelect
                :model-value="selectedAssignee[alias.id]"
                :items="availableMembers"
                value-key="value"
                label-key="label"
                placeholder="Select member..."
                class="w-full"
                @update:model-value="value => selectedAssignee[alias.id] = value"
              />
              <UButton
                icon="i-lucide-user-plus"
                variant="outline"
                size="sm"
                square
                :loading="aliasLoading"
                :disabled="!selectedAssignee[alias.id]"
                @click="onAssign(alias)"
              />
            </div>
          </UFormField>
        </div>
      </UCard>
    </div>

    <!-- Empty aliases state -->
    <UiEmptyState
      v-if="!aliases.length && !aliasLoading"
      icon="i-lucide-users-round"
      title="No aliases yet"
      subtitle="Create your first alias to group members together."
    />

    <!-- Pending Invitations -->
    <div
      v-if="isGroupAdmin && pendingInvitations.length"
      class="space-y-2"
    >
      <USeparator />
      <h3 class="text-sm font-medium text-muted">
        Pending Invitations
      </h3>
      <UCard
        v-for="invitation in pendingInvitations"
        :key="invitation.id"
        variant="outline"
      >
        <div class="flex items-center justify-between">
          <div>
            <p class="font-semibold">
              {{ invitation.email }}
            </p>
            <p class="text-sm text-muted">
              Invited {{ formatDate(invitation.invitedAt) }}
            </p>
          </div>
          <div class="flex items-center gap-3">
            <UBadge
              variant="soft"
              color="warning"
              label="Pending"
              icon="i-lucide-clock"
            />
            <UButton
              icon="i-lucide-refresh-cw"
              variant="ghost"
              size="sm"
              square
              :loading="invitationLoading"
              @click="$emit('resend', invitation)"
            />
            <UButton
              icon="i-lucide-x"
              variant="ghost"
              color="error"
              size="sm"
              square
              :loading="invitationLoading"
              @click="$emit('revoke', invitation)"
            />
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup>
import { formatDate } from '~/utils/date'

const props = defineProps({
  aliases: {
    type: Array,
    required: true,
  },
  members: {
    type: Array,
    required: true,
  },
  group: {
    type: Object,
    default: null,
  },
  isGroupAdmin: {
    type: Boolean,
    default: false,
  },
  aliasLoading: {
    type: Boolean,
    default: false,
  },
  pendingInvitations: {
    type: Array,
    default: () => [],
  },
  invitationLoading: {
    type: Boolean,
    default: false,
  },
  isFinalizing: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['rename', 'delete', 'assign', 'remove', 'finalize', 'resend', 'revoke'])

const selectedAssignee = reactive({})

const memberMap = computed(() => {
  const map = new Map()
  for (const member of props.members) {
    map.set(member.id, member)
  }
  return map
})

const aliasesWithMembers = computed(() => {
  return props.aliases.map((alias) => {
    const enriched = (alias.members || []).map((m) => {
      const rich = memberMap.value.get(m.id)
      return rich ? { ...m, ...rich } : m
    })
    return { ...alias, members: enriched }
  })
})

const hasMultiPersonAlias = computed(() => {
  return props.aliases.some(a => (a.members?.length || 0) >= 2)
})

const assignedIds = computed(() => {
  return new Set(props.aliases.flatMap(a => a.members?.map(m => m.id) || []))
})

const availableMembers = computed(() => {
  return props.members
    .filter(m => !assignedIds.value.has(m.id))
    .map(m => ({
      value: m.id,
      label: m.fullName || `${m.firstName} ${m.lastName || ''}`.trim(),
    }))
})

const onAssign = (alias) => {
  const userId = selectedAssignee[alias.id]
  if (!userId) return
  emit('assign', alias, userId)
  selectedAssignee[alias.id] = null
}
</script>
