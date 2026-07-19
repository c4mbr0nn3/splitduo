import { shallowRef, onMounted, onUnmounted } from 'vue'

/**
 * Smart back navigation.
 *
 * - If the user navigated forward to this page (history.state.back is set),
 *   go back in browser history.
 * - Otherwise (deep link, page refresh, external referrer), navigate to the
 *   given parentRoute as a fallback.
 *
 * @param {string} parentRoute - Fallback route path when no in-app history exists
 * @returns {{ canGoBack: import('vue').ShallowRef<boolean>, goBack: () => void }}
 */
export default function useSmartBack(parentRoute) {
  const router = useRouter()
  const canGoBack = shallowRef(false)

  function checkBack() {
    canGoBack.value = router.options.history.state.back !== null
  }

  onMounted(checkBack)
  const removeAfterEach = router.afterEach(checkBack)
  onUnmounted(removeAfterEach)

  function goBack() {
    if (router.options.history.state.back) {
      router.back()
    }
    else {
      navigateTo(parentRoute, { replace: true })
    }
  }

  return { canGoBack, goBack }
}
