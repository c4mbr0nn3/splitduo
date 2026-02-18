<template>
  <UTabs
    v-model="activeTab"
    :content="false"
    :items="items"
    class="mb-4"
  />
</template>

<script setup>
defineProps({
  groupId: {
    type: String,
    required: true,
  },
})

const route = useRoute()
const router = useRouter()

const items = [
  { label: 'Expenses', icon: 'i-lucide-receipt', value: 'expenses' },
  { label: 'Stats', icon: 'i-lucide-bar-chart-3', value: 'stats' },
]

const activeTab = computed({
  get() {
    return route.query.tab === 'stats' ? 'stats' : 'expenses'
  },
  set(tab) {
    router.push({ path: route.path, query: tab === 'expenses' ? {} : { tab } })
  },
})
</script>
