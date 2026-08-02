import { shallowRef, onMounted, onUnmounted } from 'vue'

/**
 * Smart back navigation.
 *
 * - If the user navigated forward to this page (history.state.back is set),
 *   go back in browser history.
 * - Otherwise (deep link, page refresh, external referrer), navigate to the
 *   given parentRoute as a fallback.
 */
export default function useSmartBack(parentRoute: string): { canGoBack: ReturnType<typeof shallowRef<boolean>>, goBack: () => void } {
  const router = useRouter()
  const canGoBack = shallowRef(false)

  function checkBack(): void {
    const state = router.options.history.state
    canGoBack.value = state !== null && state.back !== null
  }

  onMounted(checkBack)
  const removeAfterEach = router.afterEach(checkBack)
  onUnmounted(removeAfterEach)

  function goBack(): void {
    const state = router.options.history.state
    if (state !== null && state.back !== null) {
      router.back()
    }
    else {
      navigateTo(parentRoute, { replace: true })
    }
  }

  return { canGoBack, goBack }
}
