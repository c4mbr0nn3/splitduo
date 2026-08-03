// Tree-shake apexcharts: import the slim core + only the chart types/features used.
// The app uses donut + bar charts with legend and toolbar (disabled). tooltip and
// dataLabels are built into the core/chart-type modules, no separate import needed.
// See https://apexcharts.com/docs/tree-shaking/ for the full feature entry-point list.
import VueApexCharts from 'vue3-apexcharts/core'
import 'apexcharts/bar'
import 'apexcharts/donut'
import 'apexcharts/features/legend'
import 'apexcharts/features/toolbar'

export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.use(VueApexCharts)
})
