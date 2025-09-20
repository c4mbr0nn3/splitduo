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
 * Formats a duration given in milliseconds into a human-readable string.
 * Handles ms, seconds (with decimals), minutes, and hours.
 *
 * @param {number} durationMs - The duration in milliseconds.
 * @returns {string} The formatted duration (e.g., '347ms', '1.2s', '1m 5s', '2h 3m').
 */
export function formatDuration(durationMs) {
  if (durationMs == null || isNaN(durationMs)) return 'N/A'
  if (durationMs < 1000) {
    return `${durationMs}ms`
  }
  const seconds = durationMs / 1000
  if (seconds < 60) {
    // Show up to 1 decimal for sub-minute durations
    return `${parseFloat(seconds.toFixed(seconds < 10 ? 2 : 1))}s`
  }
  const mins = Math.floor(seconds / 60)
  const secs = Math.floor(seconds % 60)
  if (mins < 60) {
    return secs > 0 ? `${mins}m ${secs}s` : `${mins}m`
  }
  const hours = Math.floor(mins / 60)
  const minsLeft = mins % 60
  let result = `${hours}h`
  if (minsLeft > 0) result += ` ${minsLeft}m`
  if (secs > 0) result += ` ${secs}s`
  return result
}
