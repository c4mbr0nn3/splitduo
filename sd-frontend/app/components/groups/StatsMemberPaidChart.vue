<template>
  <UCard v-if="balances.length > 0">
    <template #header>
      <p class="font-semibold">
        Paid by Member
      </p>
    </template>
    <apexchart
      type="bar"
      :options="chartOptions"
      :series="series"
      height="250"
    />
  </UCard>
</template>

<script setup>
const props = defineProps({
  balances: { type: Array, required: true },
})

const colorMode = useColorMode()

const series = computed(() => [{
  name: 'Paid',
  data: props.balances.map(b => Number(b.totalPaid)),
}])

const chartOptions = computed(() => ({
  xaxis: {
    categories: props.balances.map(b => b.user.firstName),
  },
  plotOptions: { bar: { horizontal: true } },
  theme: { mode: colorMode.value === 'dark' ? 'dark' : 'light' },
  chart: { background: 'transparent', toolbar: { show: false } },
  tooltip: {
    y: { formatter: val => `€ ${val.toFixed(2)}` },
  },
}))
</script>
