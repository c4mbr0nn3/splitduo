<template>
  <div class="py-6 sm:py-8">
    <!-- Step Indicator -->
    <div class="overflow-x-auto pb-2 mb-4">
      <div class="flex items-center gap-2 sm:gap-4">
        <template
          v-for="(step, i) in steps"
          :key="step.id"
        >
          <div class="flex items-center gap-2">
            <UBadge
              :label="step.label"
              :color="step.active ? 'primary' : (step.done ? 'success' : 'neutral')"
              :variant="step.active ? 'solid' : 'soft'"
            />
          </div>
          <div
            v-if="i < steps.length - 1"
            class="h-px w-6 sm:w-12 bg-[var(--sd-surface-border)]"
          />
        </template>
      </div>
    </div>

    <!-- Step 2: Analysis Results (no extra card — it provides its own) -->
    <div v-if="currentStep === 'analysis'">
      <ImportAnalysisResults :analysis-results="analysisResultsForTemplate" />
    </div>

    <!-- Other steps: wrapped in card -->
    <UCard v-else>
      <template #header>
        <div class="flex flex-col">
          <h2 class="text-xl font-semibold text-primary">
            {{ getPageTitle() }}
          </h2>
          <p class="text-sm text-muted">
            {{ getPageDescription() }}
          </p>
        </div>
      </template>

      <!-- Step 1: File Upload -->
      <div
        v-if="currentStep === 'upload'"
        class="space-y-6"
      >
        <USelect
          v-if="!isAliasMode"
          v-model="selectedImportType"
          :items="importTypeOptions"
          option-attribute="label"
          value-attribute="value"
          :placeholder="$t('imports.selectImportType')"
          class="w-full"
        />
        <UBadge
          v-else
          label="SplitDuo Alias"
          color="primary"
          variant="soft"
          size="lg"
          class="w-full justify-center py-2"
        />
        <UFileUpload
          v-model="selectedFile"
          :accept="acceptedFileTypes"
          :multiple="false"
          :label="$t('imports.dragDrop')"
          :description="fileFormatDescription"
          layout="list"
        />
      </div>

      <!-- Step 3: Mapping Configuration -->
      <div
        v-else-if="currentStep === 'configure'"
        class="space-y-6"
      >
        <ImportMappingForm
          :analysis-results="analysisResultsForTemplate"
          :group-id="groupId"
          :is-importing="isImporting"
          @submit="onMappingSubmit"
        />
      </div>

      <!-- Step 4: Import in Progress -->
      <div
        v-else-if="currentStep === 'importing'"
        class="space-y-6"
      >
        <div class="text-center py-8">
          <UIcon
            name="i-lucide-loader-2"
            class="animate-spin text-primary mx-auto mb-4"
            size="48"
          />
          <h3 class="text-lg font-semibold mb-2">
            {{ $t('imports.importInProgress') }}
          </h3>
          <p class="text-muted">
            {{ $t('imports.importInProgressDescription') }}
          </p>
        </div>
      </div>
    </UCard>

    <!-- Navigation (always visible) -->
    <div class="mt-4 flex items-center justify-between">
      <!-- Back Button -->
      <UButton
        v-if="currentStep !== 'upload'"
        :label="$t('imports.back')"
        variant="ghost"
        icon="i-lucide-arrow-left"
        @click="onBack"
      />
      <div v-else />

      <!-- Action Buttons -->
      <div class="flex items-center gap-3">
        <UButton
          :label="$t('imports.cancel')"
          variant="ghost"
          :disabled="isAnalyzing || isImporting"
          @click="onCancel"
        />
        <UButton
          v-if="currentStep === 'upload'"
          :label="$t('imports.analyzeFile')"
          icon="i-lucide-search"
          :loading="isAnalyzing"
          :disabled="!canAnalyze"
          @click="onAnalyze"
        />
        <UButton
          v-else-if="currentStep === 'analysis'"
          :label="$t('imports.next')"
          icon="i-lucide-arrow-right-circle"
          @click="currentStep = 'configure'"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { ImportAnalysis, ImportStatus, ImportMapping } from '~/types/domain'

const { t } = useI18n()
const route = useRoute()
const groupId = String(route.params.id)

const { currentGroup, fetchGroup } = useGroups()
const importExport = useImportExport(groupId)
const { analyzeFile, importWithMapping, fetchImports, isAnalyzing, isImporting, clearAnalysis } = importExport
const { showError } = useNotifications()

// Unwrap readonly refs for template binding (readonly arrays can't bind to mutable props)
const analysisResults = computed(() => {
  const val = importExport.analysisResults.value
  if (!val) return null
  return {
    fileHash: val.fileHash,
    members: val.members ? [...val.members] : [],
    categories: val.categories ? [...val.categories] : [],
    paymentModes: val.paymentModes ? [...val.paymentModes] : [],
    aliases: val.aliases ? [...val.aliases] : [],
  } as ImportAnalysis
})
const currentImport = computed(() => importExport.currentImport.value ? { ...importExport.currentImport.value } as ImportStatus : null)

const group = computed(() => currentGroup.value)
const selectedImportType = ref(1)
const selectedFile = ref<File | null>(null)
type Step = 'upload' | 'analysis' | 'configure' | 'importing'
const currentStep = ref<Step>('upload')

// Template-safe computed (avoids `|` filter syntax in template)
const analysisResultsForTemplate = computed(() => analysisResults.value as ImportAnalysis | null)

const isAliasMode = computed(() => !!group.value?.useAliases)

const steps = computed(() => [
  { id: 'upload', label: t('imports.upload'), active: currentStep.value === 'upload', done: currentStep.value !== 'upload' },
  { id: 'configure', label: t('imports.configureStep'), active: currentStep.value === 'configure' || currentStep.value === 'analysis', done: currentStep.value === 'importing' },
  { id: 'importing', label: t('imports.importStep'), active: currentStep.value === 'importing', done: false },
])

const importTypeOptions = [
  { value: 1, label: 'Cospend' },
  { value: 2, label: 'SplitDuo' },
  { value: 3, label: 'Splitwise' },
  { value: 4, label: 'SplitDuo Alias' },
]

const acceptedFileTypes = computed(() => {
  if (selectedImportType.value === 1) return '.csv'
  return '.csv'
})

const fileFormatDescription = computed(() => {
  if (selectedImportType.value === 1) {
    return t('imports.cospendFormat')
  }
  if (selectedImportType.value === 2) {
    return t('imports.splitDuoFormat')
  }
  if (selectedImportType.value === 3) {
    return t('imports.splitwiseFormat')
  }
  if (selectedImportType.value === 4) {
    return t('imports.splitDuoAliasFormat')
  }
  return t('imports.genericFormat')
})

const canAnalyze = computed(() => {
  return selectedFile.value && selectedImportType.value && !isAnalyzing.value
})

const clearFile = () => {
  selectedFile.value = null
}

const validateFile = (file: File) => {
  // Validate file size (10MB limit)
  if (file.size > 10 * 1024 * 1024) {
    showError(t('imports.fileSizeError'))
    clearFile()
    return false
  }

  // Validate file type
  const extension = file.name.split('.').pop()?.toLowerCase()
  const expectedExtension = 'csv' // Both Cospend and SplitDuo use CSV format

  if (extension !== expectedExtension) {
    showError(t('imports.fileTypeError', { type: expectedExtension.toUpperCase(), importType: getImportTypeLabel() }))
    clearFile()
    return false
  }

  return true
}

// Watch for file changes and validate
watch(selectedFile, (newFile) => {
  if (newFile) {
    validateFile(newFile)
  }
}, { immediate: true })

const onAnalyze = async () => {
  if (!canAnalyze.value) return

  if (!selectedFile.value) {
    showError(t('imports.selectFileFirst'))
    return
  }

  try {
    const result = await analyzeFile(selectedFile.value, selectedImportType.value)
    if (result) {
      currentStep.value = 'analysis'
    }
  }
  catch (error: unknown) {
    console.error('Analysis failed:', error)
  }
}

const onMappingSubmit = async (mappingConfig: {
  userMappings: Record<string, string | undefined>
  aliasMappings: Record<string, string | undefined>
  categoryMappings: Record<number, number | undefined>
  paymentModeMappings: Record<number, number | undefined>
}) => {
  try {
    currentStep.value = 'importing'
    const result = await importWithMapping(mappingConfig as unknown as ImportMapping)
    if (result) {
      // Navigate back to imports list
      navigateTo(`/groups/${groupId}/imports`)
    }
  }
  catch (error: unknown) {
    console.error('Import failed:', error)
    // Stay on configure step on error
    currentStep.value = 'configure'
  }
}

const onBack = () => {
  if (currentStep.value === 'analysis') {
    currentStep.value = 'upload'
  }
  else if (currentStep.value === 'configure') {
    currentStep.value = 'analysis'
  }
}

const onCancel = () => {
  if (isAnalyzing.value || isImporting.value) return

  clearAnalysis()
  navigateTo(`/groups/${groupId}/imports`)
}

const getPageTitle = () => {
  switch (currentStep.value) {
    case 'upload': return t('imports.addImportTitle')
    case 'analysis': return t('imports.analysisResults')
    case 'configure': return t('imports.configureMappings')
    case 'importing': return t('imports.importInProgress')
    default: return t('imports.addImportTitle')
  }
}

const getPageDescription = () => {
  switch (currentStep.value) {
    case 'upload': return t('imports.importExpensesFrom', { name: group.value?.name })
    case 'analysis': return t('imports.reviewWhatWasFound')
    case 'configure': return t('imports.configureHowToMap')
    case 'importing': return t('imports.yourImportIsProcessing')
    default: return t('imports.importExpensesFrom', { name: group.value?.name })
  }
}

const getImportTypeLabel = () => {
  return importTypeOptions.find(opt => opt.value === selectedImportType.value)?.label || t('imports.unknown')
}

// Watch alias mode and force SplitDuoAlias import type
watch(isAliasMode, (newValue) => {
  if (newValue) {
    selectedImportType.value = 4
  }
})

// Watch import type changes and clear file if format doesn't match
watch(selectedImportType, () => {
  if (selectedFile.value) {
    const extension = selectedFile.value.name.split('.').pop()?.toLowerCase()
    const expectedExtension = 'csv' // All import types use CSV format

    if (extension !== expectedExtension) {
      clearFile()
    }
  }
})

// Clear state when leaving page
onBeforeUnmount(() => {
  clearAnalysis()
})

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)

    if (isAliasMode.value) {
      selectedImportType.value = 4
    }

    // Check if we're continuing an existing import
    const continueImportId = typeof route.query.continue === 'string' ? route.query.continue : undefined
    if (continueImportId) {
      // Load the existing import and set appropriate step
      await loadExistingImport(continueImportId)
    }
  }
})

const loadExistingImport = async (importGuid: string) => {
  try {
    // Fetch the specific import to get its analysis results
    // For now, we'll need to fetch from the imports list
    // In a real implementation, you might have a separate endpoint
    const { imports } = useImportExport(groupId)
    await fetchImports({ page: 1 }) // This should load recent imports

    const existingImport = imports.value.find((imp: ImportStatus) => imp.id === importGuid)
    if (existingImport && existingImport.importStatusId === 5) {
      // Set the current import and analysis results
      // analysisResults is already parsed by fetchImports
      ;(currentImport as { value: ImportStatus | null }).value = existingImport
      ;(analysisResults as { value: ImportAnalysis | null }).value = existingImport.analysisResults as ImportAnalysis | null
      currentStep.value = 'configure' // Skip to configuration step
    }
    else {
      showError(t('imports.importNotReady'))
    }
  }
  catch (error: unknown) {
    console.error('Failed to load existing import:', error)
    showError(t('imports.failedToLoadImport'))
  }
}

useHead({
  title: computed(() => `${t('imports.addImportTitle')} - ${group.value?.name || ''}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
