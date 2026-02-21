<template>
  <UCard v-if="categoryBreakdown.length > 0">
    <template #header>
      <p class="font-semibold">
        Spending by Category
      </p>
    </template>
    <apexchart
      type="donut"
      :options="chartOptions"
      :series="series"
      height="300"
    />
  </UCard>
</template>

<script setup>
const props = defineProps({
  categoryBreakdown: { type: Array, required: true },
})

const { palette, themeMode } = useChartTheme()

const series = computed(() => props.categoryBreakdown.map(c => Number(c.amount)))

const chartOptions = computed(() => ({
  labels: props.categoryBreakdown.map(c => c.categoryName),
  colors: palette.value,
  theme: { mode: themeMode.value },
  chart: { background: 'transparent' },
  dataLabels: { enabled: false },
  tooltip: {
    y: { formatter: val => `€ ${val.toFixed(2)}` },
  },
  legend: { position: 'bottom' },
}))
</script>
