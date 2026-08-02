export default defineNuxtRouteMiddleware(() => {
  const { user, isGlobalAdmin } = useAuth()

  if (!user.value || !isGlobalAdmin.value) {
    return navigateTo('/dashboard')
  }
})
