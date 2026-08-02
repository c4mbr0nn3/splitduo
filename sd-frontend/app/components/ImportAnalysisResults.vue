<template>
  <UCard v-if="analysisResults">
    <template #header>
      <div class="flex items-center gap-2">
        <UIcon
          name="i-lucide-search"
          class="text-primary"
        />
        <h3 class="text-lg font-semibold text-primary">
          {{ $t('imports.analysisResults') }}
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
          {{ $t('imports.fileAnalysisComplete') }}
        </template>
        <template #description>
          {{ $t('imports.foundItems', {
            total: getTotalItems(),
            aliases: analysisResults.aliases?.length || 0,
            members: analysisResults.members?.length || 0,
            categories: analysisResults.categories?.length || 0,
            paymentModes: analysisResults.paymentModes?.length || 0,
          }) }}
        </template>
      </UAlert>

      <!-- Aliases Section -->
      <div v-if="analysisResults.aliases?.length">
        <h4 class="text-sm font-medium text-highlighted mb-3">
          {{ $t('imports.aliasesFound', { count: analysisResults.aliases.length }) }}
        </h4>
        <div class="grid gap-2">
          <UCard
            v-for="alias in analysisResults.aliases"
            :key="alias.key"
            variant="outline"
            class="p-3"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UAvatar
                  :alt="alias.key"
                  size="sm"
                  class="bg-primary/10"
                >
                  <UIcon
                    name="i-lucide-users"
                    size="16"
                  />
                </UAvatar>
                <span class="font-medium">{{ alias.key }}</span>
              </div>
              <UBadge
                :label="alias.value"
                color="neutral"
                variant="soft"
              />
            </div>
          </UCard>
        </div>
      </div>

      <!-- Users/Members Section -->
      <div v-if="analysisResults.members?.length">
        <h4 class="text-sm font-medium text-highlighted mb-3">
          {{ $t('imports.usersFound', { count: analysisResults.members.length }) }}
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
        <h4 class="text-sm font-medium text-highlighted mb-3">
          {{ $t('imports.categoriesFound', { count: analysisResults.categories.length }) }}
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
                  class="text-muted"
                  size="16"
                />
                <span class="font-medium">{{ $t('imports.category', { key: category.key }) }}</span>
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
        <h4 class="text-sm font-medium text-highlighted mb-3">
          {{ $t('imports.paymentModesFound', { count: analysisResults.paymentModes.length }) }}
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
                  class="text-muted"
                  size="16"
                />
                <span class="font-medium">{{ $t('imports.mode', { key: paymentMode.key }) }}</span>
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
          {{ $t('imports.noDataFound') }}
        </template>
        <template #description>
          {{ $t('imports.noDataDescription') }}
        </template>
      </UAlert>
    </div>

    <template #footer>
      <div class="text-xs text-muted">
        {{ $t('imports.nextStep') }}
      </div>
    </template>
  </UCard>
</template>

<script setup lang="ts">
import type { ImportAnalysis } from '~/types/domain'

const props = defineProps<{
  analysisResults: ImportAnalysis | null
}>()

const getTotalItems = (): number => {
  const { aliases = [], members = [], categories = [], paymentModes = [] } = props.analysisResults || {}
  return aliases.length + members.length + categories.length + paymentModes.length
}

const getInitials = (name: string): string => {
  return name
    .split(' ')
    .map(word => word.charAt(0))
    .join('')
    .toUpperCase()
    .slice(0, 2)
}
</script>
