const KEY = 'push-opt-in-pending'

/** Records that the user joined a group before they could be prompted. */
export function markPushOptInPending(groupName: string): void {
  if (!import.meta.client) return
  localStorage.setItem(KEY, groupName)
}

/** Returns the stored group name and clears the marker; null when absent. */
export function consumePushOptInPending(): string | null {
  if (!import.meta.client) return null
  const value = localStorage.getItem(KEY)
  if (value === null) return null
  localStorage.removeItem(KEY)
  return value
}
