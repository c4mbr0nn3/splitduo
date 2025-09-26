<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard
      class="w-full max-w-4xl"
      variant="soft"
    >
      <template #header>
        <div class="flex flex-col">
          <h2 class="text-xl font-semibold text-primary">
            {{ getPageTitle() }}
          </h2>
          <p class="text-sm text-muted">
            {{ getPageDescription() }}
          </p>

          <!-- Step Indicator -->
          <div class="flex items-center gap-2 mt-4">
            <UBadge
              :label="'1. Upload'"
              :color="currentStep === 'upload' ? 'primary' : (analysisResults ? 'success' : 'neutral')"
              :variant="currentStep === 'upload' ? 'solid' : 'soft'"
            />
            <UIcon
              name="i-lucide-chevron-right"
              class="text-gray-400"
              size="16"
            />
            <UBadge
              :label="'2. Configure'"
              :color="currentStep === 'configure' ? 'primary' : 'neutral'"
              :variant="currentStep === 'configure' ? 'solid' : 'soft'"
            />
            <UIcon
              name="i-lucide-chevron-right"
              class="text-gray-400"
              size="16"
            />
            <UBadge
              :label="'3. Import'"
              :color="currentStep === 'importing' ? 'primary' : 'neutral'"
              :variant="currentStep === 'importing' ? 'solid' : 'soft'"
            />
          </div>
        </div>
      </template>

      <!-- Step 1: File Upload -->
      <div
        v-if="currentStep === 'upload'"
        class="space-y-6"
      >
        <USelect
          v-model="selectedImportType"
          :items="importTypeOptions"
          option-attribute="label"
          value-attribute="value"
          placeholder="Select import type"
          class="w-full"
        />
        <UFileUpload
          v-model="selectedFile"
          :accept="acceptedFileTypes"
          :multiple="false"
          label="Drag & drop a file here or click to select"
          :description="fileFormatDescription"
          layout="list"
        />
      </div>

      <!-- Step 2: Analysis Results -->
      <div
        v-else-if="currentStep === 'analysis'"
        class="space-y-6"
      >
        <ImportAnalysisResults :analysis-results="analysisResults" />
      </div>

      <!-- Step 3: Mapping Configuration -->
      <div
        v-else-if="currentStep === 'configure'"
        class="space-y-6"
      >
        <ImportAnalysisResults :analysis-results="analysisResults" />
        <ImportMappingForm
          :analysis-results="analysisResults"
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
            Import in Progress
          </h3>
          <p class="text-muted">
            Your data is being imported. This may take a few moments...
          </p>
        </div>
      </div>

      <template #footer>
        <div class="flex items-center justify-between">
          <!-- Back Button -->
          <UButton
            v-if="currentStep !== 'upload'"
            label="Back"
            variant="ghost"
            icon="i-lucide-arrow-left"
            @click="onBack"
          />
          <div v-else />

          <!-- Action Buttons -->
          <div class="flex items-center gap-3">
            <UButton
              label="Cancel"
              variant="ghost"
              :disabled="isAnalyzing || isImporting"
              @click="onCancel"
            />
            <UButton
              v-if="currentStep === 'upload'"
              label="Analyze File"
              icon="i-lucide-search"
              :loading="isAnalyzing"
              :disabled="!canAnalyze"
              @click="onAnalyze"
            />
            <UButton
              v-else-if="currentStep === 'analysis'"
              label="Next"
              icon="i-lucide-arrow-right-circle"
              @click="currentStep = 'configure'"
            />
          </div>
        </div>
      </template>
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup } = useGroups()
const { analyzeFile, importWithMapping, fetchImports, isAnalyzing, isImporting, analysisResults, currentImport, clearAnalysis } = useImportExport(groupId)
const { showError } = useNotifications()

const group = computed(() => currentGroup.value)
const selectedImportType = ref(1)
const selectedFile = ref(null)
const currentStep = ref('upload') // 'upload', 'analysis', 'configure', 'importing'

const importTypeOptions = [
  { value: 1, label: 'Cospend' },
]

const acceptedFileTypes = computed(() => {
  if (selectedImportType.value === 1) return '.csv'
  return '.csv'
})

const fileFormatDescription = computed(() => {
  if (selectedImportType.value === 1) {
    return 'Upload a Cospend .csv file (max 10MB)'
  }
  return 'Upload a file (max 10MB)'
})

const canAnalyze = computed(() => {
  return selectedFile.value && selectedImportType.value && !isAnalyzing.value
})

const clearFile = () => {
  selectedFile.value = null
}

const validateFile = (file) => {
  // Validate file size (10MB limit)
  if (file.size > 10 * 1024 * 1024) {
    showError('File size must be less than 10MB')
    clearFile()
    return false
  }

  // Validate file type
  const extension = file.name.split('.').pop()?.toLowerCase()
  const expectedExtension = selectedImportType.value === 1 ? 'csv' : 'csv'

  if (extension !== expectedExtension) {
    showError(`Please select a ${expectedExtension.toUpperCase()} file for ${getImportTypeLabel()} import`)
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
    showError('Please select a file first')
    return
  }

  try {
    const result = await analyzeFile(selectedFile.value, selectedImportType.value)
    if (result) {
      currentStep.value = 'analysis'
    }
  }
  catch (error) {
    console.error('Analysis failed:', error)
  }
}

const onMappingSubmit = async (mappingConfig) => {
  try {
    currentStep.value = 'importing'
    const result = await importWithMapping(mappingConfig)
    if (result) {
      // Navigate back to imports list
      navigateTo(`/groups/${groupId}/imports`)
    }
  }
  catch (error) {
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
    case 'upload': return 'Add Import'
    case 'analysis': return 'Analysis Results'
    case 'configure': return 'Configure Mappings'
    case 'importing': return 'Import in Progress'
    default: return 'Add Import'
  }
}

const getPageDescription = () => {
  switch (currentStep.value) {
    case 'upload': return `Import expenses from a file to ${group.value?.name}`
    case 'analysis': return 'Review what was found in your import file'
    case 'configure': return 'Configure how to map import data to your group'
    case 'importing': return 'Your import is being processed...'
    default: return `Import expenses from a file to ${group.value?.name}`
  }
}

const getImportTypeLabel = () => {
  return importTypeOptions.find(opt => opt.value === selectedImportType.value)?.label || 'Unknown'
}

// Watch import type changes and clear file if format doesn't match
watch(selectedImportType, () => {
  if (selectedFile.value) {
    const extension = selectedFile.value.name.split('.').pop()?.toLowerCase()
    const expectedExtension = selectedImportType.value === 1 ? 'csv' : 'csv'

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

    // Check if we're continuing an existing import
    const continueImportId = route.query.continue
    if (continueImportId) {
      // Load the existing import and set appropriate step
      await loadExistingImport(continueImportId)
    }
  }
})

const loadExistingImport = async (importGuid) => {
  try {
    // Fetch the specific import to get its analysis results
    // For now, we'll need to fetch from the imports list
    // In a real implementation, you might have a separate endpoint
    const { imports } = useImportExport(groupId)
    await fetchImports({ page: 1 }) // This should load recent imports

    const existingImport = imports.value.find(imp => imp.guid === importGuid)
    if (existingImport && existingImport.importStatusId === 5) {
      // Set the current import and analysis results
      // analysisResults is already parsed by fetchImports
      currentImport.value = existingImport
      analysisResults.value = existingImport.analysisResults
      currentStep.value = 'configure' // Skip to configuration step
    }
    else {
      showError('Import not found or not ready for configuration')
    }
  }
  catch (error) {
    console.error('Failed to load existing import:', error)
    showError('Failed to load import')
  }
}

useHead({
  title: computed(() => `Add Import - ${group.value?.name || 'Group'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
