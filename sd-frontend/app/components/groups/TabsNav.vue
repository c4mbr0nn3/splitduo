<template>
  <UTabs
    v-model="activeTab"
    :content="false"
    :items="items"
    class="mb-4"
  />
</template>

<script setup lang="ts">
const { t } = useI18n()

interface Props {
  groupId: string
}
const props = defineProps<Props>()

const route = useRoute()
const router = useRouter()

const items = [
  { label: t('expenses.title'), icon: 'i-lucide-receipt', value: 'expenses' },
  { label: t('stats.title'), icon: 'i-lucide-bar-chart-3', value: 'stats' },
]

const activeTab = computed({
  get() {
    return route.query.tab === 'stats' ? 'stats' : 'expenses'
  },
  set(tab) {
    router.push({ path: `/groups/${props.groupId}`, query: { tab, page: 1 } })
  },
})
</script>
