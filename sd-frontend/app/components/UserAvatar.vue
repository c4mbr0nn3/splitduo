<template>
  <UAvatar
    :src="src ?? undefined"
    :alt="displayName"
    :size="size"
    :ui="ui"
    role="img"
    :aria-label="displayName"
  >
    <template #default>
      <span
        class="h-full w-full rounded-full flex items-center justify-center"
        :style="{ backgroundColor: color.bg, color: color.fg }"
      >{{ initials }}</span>
    </template>
  </UAvatar>
</template>

<script setup lang="ts">
// 12 color pairs, all verified ≥4.5:1 contrast against white (AD7 — inline
// styles, not Tailwind tokens).
const AVATAR_PALETTE = [
  { bg: '#1e40af', fg: '#ffffff' },
  { bg: '#075985', fg: '#ffffff' },
  { bg: '#155e75', fg: '#ffffff' },
  { bg: '#065f46', fg: '#ffffff' },
  { bg: '#166534', fg: '#ffffff' },
  { bg: '#3f6212', fg: '#ffffff' },
  { bg: '#854d0e', fg: '#ffffff' },
  { bg: '#9a3412', fg: '#ffffff' },
  { bg: '#9f1239', fg: '#ffffff' },
  { bg: '#86198f', fg: '#ffffff' },
  { bg: '#6b21a8', fg: '#ffffff' },
  { bg: '#3730a3', fg: '#ffffff' },
] as const satisfies { bg: string, fg: string }[]

interface UserLike {
  id: string
  firstName?: string | null
  lastName?: string | null
  email?: string | null
  fullName?: string | null
  hasAvatar?: boolean
}

interface Props {
  user: UserLike
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl' | '2xl' | '3xl'
}

// djb2 hash → deterministic palette index for a user id
function getAvatarColor(userId: string): { bg: string, fg: string } {
  let hash = 5381
  for (let i = 0; i < userId.length; i++) {
    hash = ((hash << 5) + hash) + userId.charCodeAt(i)
  }
  const index = (hash >>> 0) % AVATAR_PALETTE.length
  // Index is always in range; fallback only satisfies noUncheckedIndexedAccess
  return AVATAR_PALETTE[index] ?? { bg: '#1e40af', fg: '#ffffff' }
}

function getInitials(user: UserLike): string {
  const first = user.firstName?.trim()
  const last = user.lastName?.trim()
  if (first && last) return (first[0]?.toUpperCase() ?? '') + (last[0]?.toUpperCase() ?? '')
  const displayName = user.fullName?.trim() || `${first || ''} ${last || ''}`.trim()
  if (displayName) {
    return displayName.split(/\s+/).filter(Boolean).slice(0, 2)
      .map(part => part[0]?.toUpperCase() ?? '').join('')
  }
  if (user.email?.trim()) return user.email.trim()[0]?.toUpperCase() ?? '?'
  return '?'
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
})

const { getAvatarUrl } = useUserAvatar()

// Blob URL of the user's avatar image (caller must revoke)
const src = ref<string | null>(null)

const ui = {
  root: 'ring-1 ring-neutral-200 dark:ring-neutral-700',
  fallback: 'font-semibold uppercase tracking-wide',
}

const displayName = computed(() =>
  props.user.fullName?.trim()
  || `${props.user.firstName || ''} ${props.user.lastName || ''}`.trim()
  || props.user.email?.trim()
  || '?',
)

const initials = computed(() => getInitials(props.user))

const color = computed(() => getAvatarColor(props.user.id))

function revokeSrc() {
  if (src.value) {
    window.URL.revokeObjectURL(src.value)
    src.value = null
  }
}

async function loadAvatar() {
  if (!props.user.hasAvatar) {
    revokeSrc()
    return
  }
  const url = await getAvatarUrl(props.user.id)
  revokeSrc()
  src.value = url
}

watch(
  [() => props.user.id, () => props.user.hasAvatar],
  () => { void loadAvatar() },
  { immediate: true },
)

onUnmounted(() => {
  revokeSrc()
})
</script>
