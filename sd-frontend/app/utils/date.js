/**
 * Format a Unix timestamp to a human-readable date string
 * @param {number|string} timestamp - Unix timestamp (seconds since epoch)
 * @returns {string} Formatted date string (e.g., "Jan 15, 2024")
 */
export function formatDate(timestamp) {
  if (!timestamp) return 'Unknown'
  const date = new Date(timestamp * 1000) // Convert Unix timestamp to Date
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

/**
 * Format a date string or Date object to a human-readable date string
 * @param {string|Date} dateInput - Date string or Date object
 * @returns {string} Formatted date string (e.g., "Jan 15, 2024")
 */
export function formatDateString(dateInput) {
  if (!dateInput) return 'Unknown'
  const date = new Date(dateInput)
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

/**
 * Formats a duration given in milliseconds into a string representing the duration in seconds.
 *
 * @param {number} durationMs - The duration in milliseconds.
 * @returns {string} The formatted duration in seconds followed by 's', or 'N/A' if input is falsy.
 */
export function formatDuration(durationMs) {
  if (!durationMs) return 'N/A'
  const seconds = Math.round(durationMs / 1000)
  return `${seconds}s`
}
