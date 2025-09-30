<template>
  <UCard>
    <template #header>
      <div class="flex items-center gap-2">
        <UIcon
          name="i-lucide-settings"
          class="text-primary"
        />
        <h3 class="text-lg font-semibold text-primary">
          Configure Mappings
        </h3>
      </div>
    </template>

    <UForm
      ref="form"
      :state="mappingState"
      :validate="validate"
      class="space-y-6"
      @submit="onSubmit"
    >
      <!-- User Mappings Section -->
      <div v-if="analysisResults.members?.length">
        <UFormField
          label="User Mappings"
          description="Map each user from your import file to an existing group member"
          class="mb-4"
        >
          <div class="space-y-3">
            <div
              v-for="member in analysisResults.members"
              :key="member.key"
              class="grid grid-cols-1 md:grid-cols-2 gap-3 items-center p-3 border border-gray-200 dark:border-gray-700 rounded-lg"
            >
              <div class="flex items-center gap-2">
                <UAvatar
                  :alt="member.key"
                  size="sm"
                  class="bg-primary/10"
                >
                  {{ getInitials(member.key) }}
                </UAvatar>
                <div>
                  <div class="font-medium">
                    {{ member.value }}
                  </div>
                </div>
              </div>
              <UFormField :name="`userMappings.${member.key}`">
                <USelect
                  v-model="mappingState.userMappings[member.key]"
                  :items="groupMemberOptions"
                  placeholder="Select group member..."
                  :loading="loadingGroupData"
                  class="w-full"
                />
              </UFormField>
            </div>
          </div>
        </UFormField>
      </div>

      <!-- Category Mappings Section -->
      <div v-if="analysisResults.categories?.length">
        <UFormField
          label="Category Mappings"
          description="Map each category from your import file to a SplitDuo category"
          class="mb-4"
        >
          <div class="space-y-3">
            <div
              v-for="category in analysisResults.categories"
              :key="category.key"
              class="grid grid-cols-1 md:grid-cols-2 gap-3 items-center p-3 border border-gray-200 dark:border-gray-700 rounded-lg"
            >
              <div class="flex items-center gap-2">
                <UIcon
                  name="i-lucide-tag"
                  class="text-muted"
                  size="20"
                />
                <div>
                  <div class="font-medium">
                    {{ category.value }}
                  </div>
                </div>
              </div>
              <UFormField :name="`categoryMappings.${category.key}`">
                <USelect
                  v-model="mappingState.categoryMappings[category.key]"
                  :items="categories"
                  value-key="id"
                  label-key="name"
                  placeholder="Select category..."
                  :loading="loadingGroupData"
                  class="w-full"
                />
              </UFormField>
            </div>
          </div>
        </UFormField>
      </div>

      <!-- Payment Mode Mappings Section -->
      <div v-if="analysisResults.paymentModes?.length">
        <UFormField
          label="Payment Mode Mappings"
          description="Map each payment mode from your import file to a SplitDuo payment mode"
          class="mb-4"
        >
          <div class="space-y-3">
            <div
              v-for="paymentMode in analysisResults.paymentModes"
              :key="paymentMode.key"
              class="grid grid-cols-1 md:grid-cols-2 gap-3 items-center p-3 border border-gray-200 dark:border-gray-700 rounded-lg"
            >
              <div class="flex items-center gap-2">
                <UIcon
                  name="i-lucide-credit-card"
                  class="text-muted"
                  size="20"
                />
                <div>
                  <div class="font-medium">
                    {{ paymentMode.value }}
                  </div>
                </div>
              </div>
              <UFormField :name="`paymentModeMappings.${paymentMode.key}`">
                <USelect
                  v-model="mappingState.paymentModeMappings[paymentMode.key]"
                  :items="paymentModes"
                  value-key="id"
                  label-key="name"
                  placeholder="Select payment mode..."
                  :loading="loadingGroupData"
                  class="w-full"
                />
              </UFormField>
            </div>
          </div>
        </UFormField>
      </div>

      <!-- Validation Summary -->
      <UAlert
        v-if="hasValidationErrors"
        color="error"
        variant="soft"
        icon="i-lucide-triangle-alert"
      >
        <template #title>
          Missing Required Mappings
        </template>
        <template #description>
          Please configure all mappings before proceeding with the import.
        </template>
      </UAlert>

      <!-- Submit Button -->
      <div class="flex justify-end">
        <UButton
          type="submit"
          icon="i-lucide-upload"
          :loading="isImporting"
          :disabled="hasValidationErrors || isImporting"
        >
          Start Import
        </UButton>
      </div>
    </UForm>
  </UCard>
</template>

<script setup>
const props = defineProps({
  analysisResults: {
    type: Object,
    required: true,
  },
  groupId: {
    type: String,
    required: true,
  },
  isImporting: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(['submit'])

// Composables
const { fetchGroupMembers } = useGroups()
const { categories } = useCategories()
const { paymentModes } = usePaymentModes()
const { showError } = useNotifications()

// State
const form = ref(null)
const loadingGroupData = ref(false)
const groupMemberOptions = ref([])

// Initialize mapping state
const mappingState = reactive({
  userMappings: {},
  categoryMappings: {},
  paymentModeMappings: {},
})

// Initialize mapping objects based on analysis results
const initializeMappings = () => {
  // Initialize user mappings
  props.analysisResults.members?.forEach((member) => {
    mappingState.userMappings[member.key] = null
  })

  // Initialize category mappings (convert keys to numbers)
  props.analysisResults.categories?.forEach((category) => {
    mappingState.categoryMappings[parseInt(category.key)] = null
  })

  // Initialize payment mode mappings (convert keys to numbers)
  props.analysisResults.paymentModes?.forEach((paymentMode) => {
    mappingState.paymentModeMappings[parseInt(paymentMode.key)] = null
  })
}

// Form validation
const validate = () => {
  const errors = []

  // User mapping validation - all required
  props.analysisResults.members?.forEach((member) => {
    if (!mappingState.userMappings[member.key]) {
      errors.push({ name: `userMappings.${member.key}`, message: 'User mapping is required' })
    }
  })

  // Category mapping validation - all required
  props.analysisResults.categories?.forEach((category) => {
    if (!mappingState.categoryMappings[parseInt(category.key)]) {
      errors.push({ name: `categoryMappings.${category.key}`, message: 'Category mapping is required' })
    }
  })

  // Payment mode mapping validation - all required
  props.analysisResults.paymentModes?.forEach((paymentMode) => {
    if (!mappingState.paymentModeMappings[parseInt(paymentMode.key)]) {
      errors.push({ name: `paymentModeMappings.${paymentMode.key}`, message: 'Payment mode mapping is required' })
    }
  })

  return errors
}

// Check if there are validation errors
const hasValidationErrors = computed(() => {
  // Check user mappings
  const missingUserMappings = props.analysisResults.members?.some(member =>
    !mappingState.userMappings[member.key],
  )

  // Check category mappings
  const missingCategoryMappings = props.analysisResults.categories?.some(category =>
    !mappingState.categoryMappings[parseInt(category.key)],
  )

  // Check payment mode mappings
  const missingPaymentModeMappings = props.analysisResults.paymentModes?.some(paymentMode =>
    !mappingState.paymentModeMappings[parseInt(paymentMode.key)],
  )

  return missingUserMappings || missingCategoryMappings || missingPaymentModeMappings
})

// Load group data for mapping options
const loadGroupData = async () => {
  if (!props.groupId) return

  loadingGroupData.value = true
  try {
    let gm
    if (props.groupId) {
      gm = await fetchGroupMembers(props.groupId)
    }

    if (gm && gm.length > 0) {
      groupMemberOptions.value = gm.map(member => ({
        value: member.user.id,
        label: `${member.user.firstName} (${member.user.email || 'No email'})`,
      }))
    }
    else {
      groupMemberOptions.value = []
    }
  }
  catch (error) {
    console.error('Failed to load group data:', error)
    showError('Failed to load group data')
  }
  finally {
    loadingGroupData.value = false
  }
}

const getInitials = (name) => {
  return name
    .split(' ')
    .map(word => word.charAt(0))
    .join('')
    .toUpperCase()
    .slice(0, 2)
}

const onSubmit = () => {
  if (hasValidationErrors.value) return

  emit('submit', {
    userMappings: mappingState.userMappings,
    categoryMappings: mappingState.categoryMappings,
    paymentModeMappings: mappingState.paymentModeMappings,
  })
}

// Initialize on mount
onMounted(async () => {
  initializeMappings()
  await loadGroupData()
})

// Watch for analysis results changes
watch(() => props.analysisResults, () => {
  initializeMappings()
}, { immediate: true })
</script>
