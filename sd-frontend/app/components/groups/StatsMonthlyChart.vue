<template>
  <UCard v-if="monthlyBreakdown.length > 0">
    <template #header>
      <p class="font-semibold">
        {{ $t('stats.monthlySpending') }}
      </p>
    </template>
    <div class="p-1">
      <apexchart
        type="bar"
        :options="chartOptions"
        :series="series"
        height="320"
      />
    </div>
  </UCard>
</template>

<script setup>
const { t, locale } = useI18n()

const props = defineProps({
  monthlyBreakdown: { type: Array, required: true },
})

const { primaryColor, themeMode } = useChartTheme()

const series = computed(() => {
  // Access locale to re-evaluate on language switch
  void locale.value
  return [{
    name: t('stats.spending'),
    data: props.monthlyBreakdown.map(m => Number(m.amount)),
  }]
})

const chartOptions = computed(() => ({
  xaxis: {
    categories: props.monthlyBreakdown.map(m =>
      new Date(m.year, m.month - 1).toLocaleDateString(locale.value, { month: 'short', year: '2-digit' }),
    ),
    tickAmount: Math.min(props.monthlyBreakdown.length, 12),
    labels: { rotate: -45, hideOverlappingLabels: true, trim: true },
  },
  colors: [primaryColor.value],
  theme: { mode: themeMode.value },
  chart: { background: 'transparent', toolbar: { show: false } },
  dataLabels: { enabled: false },
  tooltip: {
    y: { formatter: val => `€ ${val.toFixed(2)}` },
  },
}))
</script>
