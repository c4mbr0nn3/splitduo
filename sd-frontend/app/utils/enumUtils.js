/**
 * Generic enum utility system for creating type-safe enums with labels and select options
 */

/**
 * Creates a new enum with values, labels, and utility functions
 * @param {Object} enumDefinition - Object with { KEY: { value, label } }
 * @returns {Object} - Enum object with values, labels, and utility functions
 */
export function createEnum(enumDefinition) {
  const values = {}
  const labels = {}
  const entries = []

  // Build values, labels, and entries from definition
  Object.entries(enumDefinition).forEach(([key, { value, label }]) => {
    values[key] = value
    labels[value] = label
    entries.push({ key, value, label })
  })

  return {
    // Enum values (e.g., UserRole.BASE_USER)
    ...values,

    // Labels object (e.g., UserRoleLabels[1] = 'User')
    Labels: labels,

    // All entries with metadata
    Entries: entries,

    // Get label for a value
    getLabel: value => labels[value] || 'Unknown',

    // Get USelect options
    getSelectOptions: () => entries.map(({ value, label }) => ({
      label,
      value,
    })),

    // Get all values as array
    getValues: () => Object.values(values),

    // Check if value exists in enum
    isValid: value => Object.values(values).includes(value),

    // Get enum entry by value
    getEntry: value => entries.find(entry => entry.value === value),

    // Get enum entry by key
    getEntryByKey: key => entries.find(entry => entry.key === key),
  }
}

/**
 * Creates a simple enum from an array of strings
 * @param {Array} keys - Array of enum keys
 * @param {Function} labelFormatter - Optional function to format labels (defaults to capitalizing first letter)
 * @returns {Object} - Enum object
 */
export function createSimpleEnum(keys, labelFormatter = key => key.charAt(0).toUpperCase() + key.slice(1).toLowerCase()) {
  const enumDefinition = {}

  keys.forEach((key, index) => {
    enumDefinition[key.toUpperCase()] = {
      value: index + 1,
      label: labelFormatter(key),
    }
  })

  return createEnum(enumDefinition)
}

/**
 * Helper to create enum from key-value pairs
 * @param {Object} keyValuePairs - Object with { KEY: value }
 * @param {Function} labelFormatter - Function to format labels from keys
 * @returns {Object} - Enum object
 */
export function createEnumFromValues(keyValuePairs, labelFormatter = key => key.replace(/_/g, ' ').toLowerCase().replace(/\b\w/g, l => l.toUpperCase())) {
  const enumDefinition = {}

  Object.entries(keyValuePairs).forEach(([key, value]) => {
    enumDefinition[key] = {
      value,
      label: labelFormatter(key),
    }
  })

  return createEnum(enumDefinition)
}
