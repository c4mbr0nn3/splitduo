<template>
  <UCard v-if="analysisResults">
    <template #header>
      <div class="flex items-center gap-2">
        <UIcon
          name="i-lucide-search"
          class="text-primary"
        />
        <h3 class="text-lg font-semibold text-primary">
          Analysis Results
        </h3>
      </div>
    </template>

    <div class="space-y-6">
      <!-- Summary Alert -->
      <UAlert
        color="info"
        variant="soft"
        icon="i-lucide-info"
      >
        <template #title>
          File Analysis Complete
        </template>
        <template #description>
          Found {{ getTotalItems() }} items to configure:
          {{ analysisResults.members?.length || 0 }} users,
          {{ analysisResults.categories?.length || 0 }} categories,
          {{ analysisResults.paymentModes?.length || 0 }} payment modes
        </template>
      </UAlert>

      <!-- Users/Members Section -->
      <div v-if="analysisResults.members?.length">
        <h4 class="text-sm font-medium text-gray-900 dark:text-gray-100 mb-3">
          Users Found ({{ analysisResults.members.length }})
        </h4>
        <div class="grid gap-2">
          <UCard
            v-for="member in analysisResults.members"
            :key="member.key"
            variant="outline"
            class="p-3"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UAvatar
                  :alt="member.key"
                  size="sm"
                  class="bg-primary/10"
                >
                  {{ getInitials(member.key) }}
                </UAvatar>
                <span class="font-medium">{{ member.key }}</span>
              </div>
              <UBadge
                :label="member.value"
                color="neutral"
                variant="soft"
              />
            </div>
          </UCard>
        </div>
      </div>

      <!-- Categories Section -->
      <div v-if="analysisResults.categories?.length">
        <h4 class="text-sm font-medium text-gray-900 dark:text-gray-100 mb-3">
          Categories Found ({{ analysisResults.categories.length }})
        </h4>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-2">
          <UCard
            v-for="category in analysisResults.categories"
            :key="category.key"
            variant="outline"
            class="p-3"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UIcon
                  name="i-lucide-tag"
                  class="text-neutral-500"
                  size="16"
                />
                <span class="font-medium">Category {{ category.key }}</span>
              </div>
              <UBadge
                :label="category.value"
                color="neutral"
                variant="soft"
              />
            </div>
          </UCard>
        </div>
      </div>

      <!-- Payment Modes Section -->
      <div v-if="analysisResults.paymentModes?.length">
        <h4 class="text-sm font-medium text-gray-900 dark:text-gray-100 mb-3">
          Payment Modes Found ({{ analysisResults.paymentModes.length }})
        </h4>
        <div class="grid grid-cols-1 md:grid-cols-2 gap-2">
          <UCard
            v-for="paymentMode in analysisResults.paymentModes"
            :key="paymentMode.key"
            variant="outline"
            class="p-3"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UIcon
                  name="i-lucide-credit-card"
                  class="text-neutral-500"
                  size="16"
                />
                <span class="font-medium">Mode {{ paymentMode.key }}</span>
              </div>
              <UBadge
                :label="paymentMode.value"
                color="neutral"
                variant="soft"
              />
            </div>
          </UCard>
        </div>
      </div>

      <!-- Empty State -->
      <UAlert
        v-if="!getTotalItems()"
        color="warning"
        variant="soft"
        icon="i-lucide-triangle-alert"
      >
        <template #title>
          No Data Found
        </template>
        <template #description>
          The file appears to be empty or in an unexpected format. Please check your file and try again.
        </template>
      </UAlert>
    </div>

    <template #footer>
      <div class="text-xs text-gray-500 dark:text-gray-400">
        Next: Configure how these items should be mapped to your SplitDuo group
      </div>
    </template>
  </UCard>
</template>

<script setup>
const props = defineProps({
  analysisResults: {
    type: Object,
    required: true,
  },
})

const getTotalItems = () => {
  const { members = [], categories = [], paymentModes = [] } = props.analysisResults || {}
  return members.length + categories.length + paymentModes.length
}

const getInitials = (name) => {
  return name
    .split(' ')
    .map(word => word.charAt(0))
    .join('')
    .toUpperCase()
    .slice(0, 2)
}
</script>
