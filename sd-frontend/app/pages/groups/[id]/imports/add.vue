<template>
  <div class="min-h-screen p-4 flex flex-col items-center">
    <UCard
      class="w-full max-w-2xl"
      variant="soft"
    >
      <template #header>
        <div class="flex flex-col">
          <h2 class="text-xl font-semibold text-primary">
            Add Import
          </h2>
          <p class="text-sm text-muted">
            Import expenses from a file to {{ group?.name }}
          </p>
        </div>
      </template>

      <div class="space-y-6">
        <!-- Import Type Selection -->

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
      <template #footer>
        <div class="flex items-center justify-end gap-3">
          <UButton
            label="Cancel"
            variant="ghost"
            @click="navigateTo(`/groups/${groupId}/imports`)"
          />
          <UButton
            label="Import Data"
            icon="i-lucide-upload"
            :loading="isImporting"
            :disabled="!canSubmit"
            @click="onSubmit"
          />
        </div>
      </template>
    </UCard>
  </div>
</template>

<script setup>
const route = useRoute()
const groupId = route.params.id

const { currentGroup, fetchGroup } = useGroups()
const { importData, isImporting } = useImportExport(groupId)
const { showError } = useNotifications()

const group = computed(() => currentGroup.value)
const selectedImportType = ref(1)
const selectedFile = ref(null)

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

const canSubmit = computed(() => {
  return selectedFile.value && selectedImportType.value && !isImporting.value
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

const onSubmit = async () => {
  if (!canSubmit.value) return

  if (!selectedFile.value) {
    showError('Please select a file first')
    return
  }

  try {
    await importData(selectedFile.value, selectedImportType.value)
    navigateTo(`/groups/${groupId}/imports`)
  }
  catch (error) {
    console.error('Import failed:', error)
  }
}

const getImportTypeLabel = () => {
  return importTypeOptions.find(opt => opt.value === selectedImportType.value)?.label || 'Unknown'
}

// Watch import type changes and clear file if format doesn't match
watch(selectedImportType, () => {
  if (selectedFile.value) {
    const extension = selectedFile.value.name.split('.').pop()?.toLowerCase()
    const expectedExtension = selectedImportType.value === 1 ? 'json' : 'csv'

    if (extension !== expectedExtension) {
      clearFile()
    }
  }
})

onMounted(async () => {
  if (groupId) {
    await fetchGroup(groupId)
  }
})

useHead({
  title: computed(() => `Add Import - ${group.value?.name || 'Group'}`),
})

definePageMeta({
  middleware: 'auth',
})
</script>
