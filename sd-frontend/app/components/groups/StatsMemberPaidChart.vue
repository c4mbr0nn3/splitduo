<template>
  <UCard v-if="balances.length > 0">
    <template #header>
      <p class="font-semibold">
        {{ t('stats.paidByMember') }}
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

<script setup lang="ts">
import type { NormalBalance } from '~/types/domain'
import { formatCurrency } from '~/utils/currency'

interface Props {
  balances: NormalBalance[]
}
const props = defineProps<Props>()

const { t } = useI18n()
const { primaryColor, themeMode } = useChartTheme()

const series = computed(() => [{
  name: 'Paid',
  data: props.balances.map(b => Number(b.totalPaid)),
}])

const chartOptions = computed(() => ({
  xaxis: {
    categories: props.balances.map(b => b.user.firstName),
  },
  plotOptions: { bar: { horizontal: true } },
  colors: [primaryColor.value],
  theme: { mode: themeMode.value },
  chart: { background: 'transparent', toolbar: { show: false } },
  tooltip: {
    y: { formatter: (val: number) => formatCurrency(val) },
  },
}))
</script>
