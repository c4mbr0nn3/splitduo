// Proactive token refresh: schedule a refresh before the access token expires
// and on tab focus, so the user never hits a 401→refresh→retry cycle in the
// foreground. Three layers back this up: the 401-retry in useApi, the backend
// 30s grace window on refresh-token rotation, and this plugin.
//
// On refresh failure, refreshToken() clears tokens + nulls user; auth.client.js
// watches user→null and redirects to /. The timer is cleared on logout and
// rescheduled whenever the token changes (including cross-tab cookie sync).
const REFRESH_LEAD_S = 60 // refresh this many seconds before expiry
const MIN_DELAY_S = 10 // never schedule a refresh sooner than this

export default defineNuxtPlugin({
  name: 'auth-refresh',
  setup() {
    const { getToken, getRefreshToken } = useAuthToken()
    const { refreshToken } = useAuth()
    const token = useState('auth-token')

    let timer = null
    let inFlight = false

    const clearTimer = () => {
      if (timer) {
        clearTimeout(timer)
        timer = null
      }
    }

    // Refresh now. On success: reschedule with the new token's exp. On failure:
    // refreshToken() already cleared tokens + nulled user; auth.client.js watches
    // user→null and redirects to /. Nothing to do here on failure.
    const refreshNow = async () => {
      if (inFlight) return
      if (!getRefreshToken()) return
      inFlight = true
      try {
        const ok = await refreshToken()
        if (ok) {
          schedule()
        }
      }
      finally {
        inFlight = false
      }
    }

    // Schedule the next refresh based on the current token's exp. If the token
    // is already expired, skip scheduling — the 401-retry path in useApi handles
    // it; this plugin only adds value for future expiries.
    const schedule = () => {
      clearTimer()
      const exp = decodeJwtExp(getToken())
      if (!exp) return
      const now = Math.floor(Date.now() / 1000)
      if (exp - now <= 0) return
      const delayS = Math.max(exp - REFRESH_LEAD_S - now, MIN_DELAY_S)
      timer = setTimeout(refreshNow, delayS * 1000)
    }

    // On tab focus: if the token is expired or near-expiry, refresh before
    // requests fire. This is the real safety net for sleep/wake, where the
    // timer would have died.
    const onVisibility = () => {
      if (document.visibilityState !== 'visible') return
      const exp = decodeJwtExp(getToken())
      if (!exp) return
      const now = Math.floor(Date.now() / 1000)
      if (exp - now <= REFRESH_LEAD_S) {
        refreshNow()
      }
      else {
        schedule()
      }
    }

    // Reschedule whenever the token changes (login, refresh, cross-tab sync,
    // logout). On logout (token null) the timer is cleared and not rescheduled.
    watch(token, (val) => {
      if (!val) {
        clearTimer()
        return
      }
      schedule()
    })

    document.addEventListener('visibilitychange', onVisibility)

    // Initial schedule if already authenticated at plugin setup
    if (getToken()) {
      schedule()
    }

    // Clean up on app teardown
    const nuxtApp = useNuxtApp()
    nuxtApp.hook('app:deactivated', () => {
      clearTimer()
      document.removeEventListener('visibilitychange', onVisibility)
    })
  },
})
