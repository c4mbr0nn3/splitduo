const globalIsAiEnabled = ref(false)
const globalIsInitialized = ref(false)
let fetchPromise = null

export default function useAiStatus() {
  const api = useApi()

  const fetchAiStatus = async () => {
    if (globalIsInitialized.value) return
    if (fetchPromise) return fetchPromise

    fetchPromise = (async () => {
      try {
        const response = await api.get('/ai/status')
        if (response.success) globalIsAiEnabled.value = response.data.enabled
        globalIsInitialized.value = true
      }
      catch {
        // Silent — feature just stays hidden
      }
      finally {
        fetchPromise = null
      }
    })()

    return fetchPromise
  }

  return {
    isAiEnabled: readonly(globalIsAiEnabled),
    fetchAiStatus,
  }
}
