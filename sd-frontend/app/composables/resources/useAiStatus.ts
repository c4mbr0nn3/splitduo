import type { AiStatus } from '~/types/domain'

const globalIsAiEnabled = ref(false)
const globalIsInitialized = ref(false)
let fetchPromise: Promise<void> | null = null

export default function useAiStatus() {
  const api = useApi()

  const fetchAiStatus = async () => {
    if (globalIsInitialized.value) return
    if (fetchPromise) return fetchPromise

    fetchPromise = (async () => {
      try {
        const response = await api.get<AiStatus>('/ai/status')
        if (response.success && response.data) globalIsAiEnabled.value = response.data.enabled
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
