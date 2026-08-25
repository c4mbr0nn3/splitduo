<template>
  <UCard v-if="categoryBreakdown.length > 0">
    <template #header>
      <p class="font-semibold">
        {{ t('stats.spendingByCategory') }}
      </p>
    </template>
    <div class="p-1">
      <apexchart
        type="donut"
        :options="chartOptions"
        :series="series"
        height="320"
      />
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { CategoryStat } from '~/types/domain'
import { formatCurrency } from '~/utils/currency'

interface Props {
  categoryBreakdown: CategoryStat[]
}
const props = defineProps<Props>()

const { t } = useI18n()
const { palette, themeMode } = useChartTheme()

const series = computed(() => props.categoryBreakdown.map(c => Number(c.amount)))

const chartOptions = computed(() => ({
  labels: props.categoryBreakdown.map(c => c.categoryName),
  colors: palette.value,
  theme: { mode: themeMode.value },
  chart: { background: 'transparent' },
  dataLabels: { enabled: false },
  tooltip: {
    y: { formatter: (val: number) => formatCurrency(val) },
  },
  legend: { position: 'bottom' },
}))
</script>
