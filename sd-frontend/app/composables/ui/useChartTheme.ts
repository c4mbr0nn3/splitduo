const getCssColor = (name: string): string => {
  if (!import.meta.client) return ''
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim()
}

export default function useChartTheme() {
  const colorMode = useColorMode()

  // Interleave primary and secondary shades for distinct adjacent slices in multi-series charts
  const palette: ComputedRef<string[]> = computed(() => [
    getCssColor('--ui-color-primary-500'),
    getCssColor('--ui-color-secondary-500'),
    getCssColor('--ui-color-primary-300'),
    getCssColor('--ui-color-secondary-300'),
    getCssColor('--ui-color-primary-700'),
    getCssColor('--ui-color-secondary-700'),
    getCssColor('--ui-color-primary-400'),
    getCssColor('--ui-color-secondary-400'),
    getCssColor('--ui-color-primary-600'),
    getCssColor('--ui-color-secondary-600'),
    getCssColor('--ui-color-primary-200'),
  ])

  const primaryColor: ComputedRef<string> = computed(() => getCssColor('--ui-color-primary-500'))

  const themeMode: ComputedRef<'dark' | 'light'> = computed(() => colorMode.value === 'dark' ? 'dark' : 'light')

  return { palette, primaryColor, themeMode }
}
