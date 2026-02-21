<template>
  <UCard v-if="monthlyBreakdown.length > 0">
    <template #header>
      <p class="font-semibold">
        Monthly Spending
      </p>
    </template>
    <apexchart
      type="bar"
      :options="chartOptions"
      :series="series"
      height="300"
    />
  </UCard>
</template>

<script setup>
const props = defineProps({
  monthlyBreakdown: { type: Array, required: true },
})

const colorMode = useColorMode()

const series = computed(() => [{
  name: 'Spending',
  data: props.monthlyBreakdown.map(m => Number(m.amount)),
}])

const chartOptions = computed(() => ({
  xaxis: {
    categories: props.monthlyBreakdown.map(m =>
      new Date(m.year, m.month - 1).toLocaleDateString('en', { month: 'short', year: '2-digit' }),
    ),
    tickAmount: Math.min(props.monthlyBreakdown.length, 12),
    labels: { rotate: -45, hideOverlappingLabels: true, trim: true },
  },
  theme: { mode: colorMode.value === 'dark' ? 'dark' : 'light' },
  chart: { background: 'transparent', toolbar: { show: false } },
  dataLabels: { enabled: false },
  tooltip: {
    y: { formatter: val => `€ ${val.toFixed(2)}` },
  },
}))
</script>
