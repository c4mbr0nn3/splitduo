import { useAuth } from '../composables'

export default defineNuxtRouteMiddleware(() => {
  const { user } = useAuth()
  if (!user.value) {
    return navigateTo('/')
  }
})
