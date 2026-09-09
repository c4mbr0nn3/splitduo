<template>
  <div class="py-6 sm:py-8">
    <UiLoadingSpinner
      v-if="pageLoading"
      :text="$t('recurring.loading')"
    />

    <UiEmptyState
      v-else-if="loadError"
      icon="i-lucide-repeat"
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

    <template v-else>
      <UiCardHeader
        :title="$t('recurring.title')"
        :subtitle="$t('recurring.subtitle')"
        :back-to="`/groups/${groupId}`"
        class="mb-6"
      >
        <template #actions>
          <UButton
            icon="i-lucide-plus"
            size="sm"
            :to="`/groups/${groupId}/recurring/add`"
          >
            <span class="hidden sm:inline">{{ $t('recurring.newTemplate') }}</span>
          </UButton>
        </template>
      </UiCardHeader>

      <UCard
        class="sd-surface"
        :ui="{ body: 'p-4 sm:p-6' }"
      >
        <div class="space-y-6">
          <!-- Approval queue -->
          <RecurringApprovalQueueSection
            v-if="pendingInstances.length > 0"
            :pending-instances="pendingInstances"
            :is-approving="approvingInstanceId"
            :is-rejecting="rejectingInstanceId"
            @approve="openApproveDialog"
            @reject="onReject"
          />

          <!-- Template list -->
          <div>
            <div class="flex items-center justify-between mb-4">
              <h2 class="text-lg font-semibold text-primary">
                {{ $t('recurring.templatesTitle') }}
              </h2>
            </div>
            <div
              v-if="templates.length"
              class="space-y-3"
            >
              <UCard
                v-for="template in templates"
                :key="template.id"
                class="sd-surface"
                :ui="{ body: 'p-4 sm:p-5' }"
              >
                <RecurringTemplateCard
                  :template="template"
                  @toggle="value => onToggle(template, value)"
                  @edit="goToEdit(template)"
                  @delete="confirmDeleteTemplate(template)"
                />
              </UCard>
            </div>
            <UiEmptyState
              v-else
              icon="i-lucide-repeat"
              :title="$t('recurring.noTemplates')"
              :subtitle="$t('recurring.noTemplatesSubtitle')"
            >
              <template #action>
                <UButton
                  :to="`/groups/${groupId}/recurring/add`"
                  color="primary"
                  variant="outline"
                  size="sm"
                  icon="i-lucide-plus"
                  class="mt-4"
                >
                  {{ $t('recurring.newTemplate') }}
                </UButton>
              </template>
            </UiEmptyState>
          </div>
        </div>
      </UCard>
    </template>

    <!-- Approve dialog -->
    <RecurringApproveRecurringDialog
      v-model:open="isApproveDialogOpen"
      :instance="selectedInstance ?? placeholderInstance"
      :group-id="groupId"
      @approved="onApprove"
      @rejected="onReject"
    />

    <!-- Resume-choice dialog (skip vs backfill) -->
    <RecurringResumeRecurringDialog
      v-model:open="isResumeDialogOpen"
      :template="resumeTemplateChoice ?? placeholderTemplate"
      @resume="onResumeChoice"
      @cancel="onResumeCancel"
    />
  </div>
</template>

<script setup lang="ts">
import type { DeepReadonly } from 'vue'
import type { RecurringExpenseTemplate, RecurringExpenseInstance, ApproveRecurringExpenseInstanceRequest, ResumeStrategy } from '~/types/domain'

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)

const {
  templates,
  pendingInstances,
  fetchTemplates,
  fetchPendingInstances,
  toggleTemplateActive,
  resumeTemplate,
  deleteTemplate,
  approveInstance,
  rejectInstance,
} = useRecurringExpenses(groupId)

const { fetchGroup, currentGroup } = useGroups()

const pageLoading = ref(true)
const loadError = ref(false)

const approvingInstanceId = ref<string | null>(null)
const rejectingInstanceId = ref<string | null>(null)
const isResuming = ref(false)

const isApproveDialogOpen = ref(false)
const selectedInstance = ref<DeepReadonly<RecurringExpenseInstance> | null>(null)

// The dialog requires an instance prop; render it closed until one is selected
const placeholderInstance = computed<DeepReadonly<RecurringExpenseInstance>>(() => ({
  id: '',
  templateId: '',
  templateTitle: '',
  periodDate: '',
  status: 1,
  amount: 0,
  createdAt: 0,
}))

const retryLoad = async (): Promise<void> => {
  loadError.value = false
  await loadData()
}

const loadData = async (): Promise<void> => {
  pageLoading.value = true
  loadError.value = false
  try {
    await Promise.all([
      fetchGroup(groupId),
      // Paused templates stay visible so users can resume them (with the
      // backfill/skip choice) — the resume flow is unreachable otherwise.
      fetchTemplates(true),
      fetchPendingInstances(),
    ])
  }
  catch {
    loadError.value = true
  }
  finally {
    pageLoading.value = false
  }
}

onMounted(async () => {
  await loadData()
})

// --- Template actions --------------------------------------------------------

const onToggle = async (template: DeepReadonly<RecurringExpenseTemplate>, isActive: boolean): Promise<void> => {
  // Resuming a template with missed occurrences: ask the user whether to skip
  // or backfill them (spec D7). With 0 missed, resume directly via the toggle.
  if (isActive && Number(template.missedCount) > 0) {
    resumeTemplateChoice.value = template
    isResumeDialogOpen.value = true
    return
  }

  try {
    await toggleTemplateActive(template.id, isActive)
  }
  catch {
    // Error shown via toast
  }
}

// --- Resume-choice dialog ----------------------------------------------------

const isResumeDialogOpen = ref(false)
const resumeTemplateChoice = ref<DeepReadonly<RecurringExpenseTemplate> | null>(null)

// The dialog requires a template prop; render it closed until one is selected
const placeholderTemplate = computed<DeepReadonly<RecurringExpenseTemplate>>(() => ({
  id: '',
  groupId: '',
  ownerId: '',
  title: '',
  amount: 0,
  paidByUserId: '',
  recurrenceMode: 1,
  weekdays: 0,
  anchorDate: '',
  isActive: false,
}))

const onResumeChoice = async (strategy: ResumeStrategy): Promise<void> => {
  const templateId = resumeTemplateChoice.value?.id
  isResumeDialogOpen.value = false
  resumeTemplateChoice.value = null
  if (!templateId) return

  isResuming.value = true
  try {
    await resumeTemplate(templateId, strategy)
  }
  catch {
    // Error shown via toast
  }
  finally {
    isResuming.value = false
  }
}

const onResumeCancel = (): void => {
  // Cancel leaves the template paused
  isResumeDialogOpen.value = false
  resumeTemplateChoice.value = null
}

const goToEdit = (template: DeepReadonly<RecurringExpenseTemplate>): void => {
  navigateTo(`/groups/${groupId}/recurring/${template.id}/edit`)
}

const confirmDeleteTemplate = async (template: DeepReadonly<RecurringExpenseTemplate>): Promise<void> => {
  try {
    await deleteTemplate(template.id)
  }
  catch {
    // Error shown via toast
  }
}

// --- Approval actions --------------------------------------------------------

const openApproveDialog = (instanceId: string): void => {
  selectedInstance.value = pendingInstances.value.find(inst => inst.id === instanceId) ?? null
  isApproveDialogOpen.value = !!selectedInstance.value
}

const onApprove = async (instanceId: string, payload: ApproveRecurringExpenseInstanceRequest): Promise<void> => {
  approvingInstanceId.value = instanceId
  try {
    await approveInstance(instanceId, payload)
    isApproveDialogOpen.value = false
    selectedInstance.value = null
    await refreshTemplatePendingCounts()
  }
  catch {
    // Error shown via toast
  }
  finally {
    approvingInstanceId.value = null
  }
}

const onReject = async (instanceId: string): Promise<void> => {
  rejectingInstanceId.value = instanceId
  try {
    await rejectInstance(instanceId)
    isApproveDialogOpen.value = false
    selectedInstance.value = null
    await refreshTemplatePendingCounts()
  }
  catch {
    // Error shown via toast
  }
  finally {
    rejectingInstanceId.value = null
  }
}

// The queue list updates optimistically via the composable, but each template's
// pendingCount badge lives on the template record — refresh to keep both in sync.
const refreshTemplatePendingCounts = async (): Promise<void> => {
  await fetchTemplates(true).catch(() => {})
}

useHead({
  title: computed(() => currentGroup.value?.name
    ? t('recurring.titleForGroup', { name: currentGroup.value.name })
    : t('recurring.title')),
})

definePageMeta({
  middleware: 'auth',
})
</script>
