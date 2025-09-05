import { useAuth } from '~/composables/auth/useAuth'

export default defineNuxtRouteMiddleware(() => {
  const { user } = useAuth()
  if (!user.value) {
    return navigateTo('/')
  }
})
