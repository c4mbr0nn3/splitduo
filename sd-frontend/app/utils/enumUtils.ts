/**
 * Generic enum utility system for creating type-safe enums with labels and select options
 */

/** A single enum entry definition */
export interface EnumEntry {
  value: number
  label: string
}

/** The shape of an enum definition object: `{ KEY: { value, label } }` */
export type EnumDefinition = Record<string, EnumEntry>

/** The result of `createEnum`, combining value keys with helper methods */
export type EnumResult<T extends EnumDefinition> = {
  [K in keyof T]: T[K]['value']
} & {
  /** Map of numeric value → label string */
  Labels: Record<number, string>

  /** Array of all entries with key, value, and label */
  Entries: Array<{ key: string, value: number, label: string }>

  /** Get the label for a numeric value */
  getLabel: (value: number) => string

  /** Get options suitable for USelect / dropdown components */
  getSelectOptions: () => Array<{ label: string, value: number }>

  /** Get all numeric values as an array */
  getValues: () => number[]

  /** Check if a numeric value exists in the enum */
  isValid: (value: number) => boolean

  /** Get an entry by its numeric value */
  getEntry: (value: number) => { key: string, value: number, label: string } | undefined

  /** Get an entry by its string key */
  getEntryByKey: (key: string) => { key: string, value: number, label: string } | undefined
}

/**
 * Creates a new enum with values, labels, and utility functions
 * @param enumDefinition - Object with `{ KEY: { value, label } }`
 * @returns Enum object with values, labels, and utility functions
 */
export function createEnum<T extends EnumDefinition>(enumDefinition: T): EnumResult<T> {
  const values: Record<string, number> = {}
  const labels: Record<number, string> = {}
  const entries: Array<{ key: string, value: number, label: string }> = []

  // Build values, labels, and entries from definition
  Object.entries(enumDefinition as Record<string, EnumEntry>).forEach(([key, { value, label }]) => {
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
    getLabel: (value: number) => labels[value] || 'Unknown',

    // Get USelect options
    getSelectOptions: () => entries.map(({ value, label }) => ({
      label,
      value,
    })),

    // Get all values as array
    getValues: () => Object.values(values),

    // Check if value exists in enum
    isValid: (value: number) => Object.values(values).includes(value),

    // Get enum entry by value
    getEntry: (value: number) => entries.find(entry => entry.value === value),

    // Get enum entry by key
    getEntryByKey: (key: string) => entries.find(entry => entry.key === key),
  } as unknown as EnumResult<T>
}

/**
 * Creates a simple enum from an array of strings
 * @param keys - Array of enum keys
 * @param labelFormatter - Optional function to format labels (defaults to capitalizing first letter)
 * @returns Enum object
 */
export function createSimpleEnum(
  keys: string[],
  labelFormatter: (key: string) => string = key => key.charAt(0).toUpperCase() + key.slice(1).toLowerCase(),
): EnumResult<Record<string, EnumEntry>> {
  const enumDefinition: Record<string, EnumEntry> = {}

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
 * @param keyValuePairs - Object with `{ KEY: value }`
 * @param labelFormatter - Function to format labels from keys
 * @returns Enum object
 */
export function createEnumFromValues(
  keyValuePairs: Record<string, number>,
  labelFormatter: (key: string) => string = key => key.replace(/_/g, ' ').toLowerCase().replace(/\b\w/g, l => l.toUpperCase()),
): EnumResult<Record<string, EnumEntry>> {
  const enumDefinition: Record<string, EnumEntry> = {}

  Object.entries(keyValuePairs).forEach(([key, value]) => {
    enumDefinition[key] = {
      value,
      label: labelFormatter(key),
    }
  })

  return createEnum(enumDefinition)
}
