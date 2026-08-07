import { describe, it, expect } from 'vitest'
import { createEnum, createSimpleEnum, createEnumFromValues } from './enumUtils'
import type { EnumDefinition } from './enumUtils'

describe('createEnum', () => {
  const statusDefinition = {
    DRAFT: { value: 0, label: 'Draft' },
    IN_PROGRESS: { value: 1, label: 'In Progress' },
    DONE: { value: 2, label: 'Done' },
  } as const satisfies EnumDefinition

  it('exposes each key as its numeric value', () => {
    const status = createEnum(statusDefinition)

    expect(status.DRAFT).toBe(0)
    expect(status.IN_PROGRESS).toBe(1)
    expect(status.DONE).toBe(2)
  })

  it('builds a Labels map from value to label', () => {
    const status = createEnum(statusDefinition)

    expect(status.Labels).toEqual({ 0: 'Draft', 1: 'In Progress', 2: 'Done' })
  })

  it('returns the label for a known value via getLabel', () => {
    const status = createEnum(statusDefinition)

    expect(status.getLabel(1)).toBe('In Progress')
  })

  it('returns "Unknown" for an unknown value via getLabel', () => {
    const status = createEnum(statusDefinition)

    expect(status.getLabel(99)).toBe('Unknown')
  })

  it('generates select options as {label, value} pairs in definition order', () => {
    const status = createEnum(statusDefinition)

    expect(status.getSelectOptions()).toEqual([
      { label: 'Draft', value: 0 },
      { label: 'In Progress', value: 1 },
      { label: 'Done', value: 2 },
    ])
  })

  it('returns all numeric values via getValues', () => {
    const status = createEnum(statusDefinition)

    expect(status.getValues()).toEqual([0, 1, 2])
  })

  it('validates known values as valid', () => {
    const status = createEnum(statusDefinition)

    expect(status.isValid(0)).toBe(true)
    expect(status.isValid(2)).toBe(true)
  })

  it('rejects unknown values as invalid', () => {
    const status = createEnum(statusDefinition)

    expect(status.isValid(3)).toBe(false)
    expect(status.isValid(-1)).toBe(false)
  })

  it('returns the entry for a known value via getEntry', () => {
    const status = createEnum(statusDefinition)

    expect(status.getEntry(1)).toEqual({ key: 'IN_PROGRESS', value: 1, label: 'In Progress' })
  })

  it('returns undefined for an unknown value via getEntry', () => {
    const status = createEnum(statusDefinition)

    expect(status.getEntry(99)).toBeUndefined()
  })

  it('returns the entry for a known key via getEntryByKey', () => {
    const status = createEnum(statusDefinition)

    expect(status.getEntryByKey('DONE')).toEqual({ key: 'DONE', value: 2, label: 'Done' })
  })

  it('returns undefined for an unknown key via getEntryByKey', () => {
    const status = createEnum(statusDefinition)

    expect(status.getEntryByKey('NOPE')).toBeUndefined()
  })

  it('handles an empty definition', () => {
    const empty = createEnum({})

    expect(empty.Entries).toEqual([])
    expect(empty.getValues()).toEqual([])
    expect(empty.getSelectOptions()).toEqual([])
    expect(empty.isValid(1)).toBe(false)
    expect(empty.getLabel(1)).toBe('Unknown')
  })

  it('allows duplicate labels across different values', () => {
    const duplicated = createEnum({
      FIRST: { value: 1, label: 'Same' },
      SECOND: { value: 2, label: 'Same' },
    } as const satisfies EnumDefinition)

    expect(duplicated.getLabel(1)).toBe('Same')
    expect(duplicated.getLabel(2)).toBe('Same')
    expect(duplicated.Entries).toHaveLength(2)
    expect(duplicated.getSelectOptions()).toEqual([
      { label: 'Same', value: 1 },
      { label: 'Same', value: 2 },
    ])
  })
})

describe('createSimpleEnum', () => {
  it('creates enum values starting at 1 in key order', () => {
    const status = createSimpleEnum(['active', 'inactive'])

    expect(status.ACTIVE).toBe(1)
    expect(status.INACTIVE).toBe(2)
  })

  it('defaults labels to capitalized keys', () => {
    const status = createSimpleEnum(['active', 'inactive'])

    expect(status.Labels).toEqual({ 1: 'Active', 2: 'Inactive' })
  })

  it('generates select options from the default labels', () => {
    const status = createSimpleEnum(['active', 'inactive'])

    expect(status.getSelectOptions()).toEqual([
      { label: 'Active', value: 1 },
      { label: 'Inactive', value: 2 },
    ])
  })

  it('validates generated values and rejects others', () => {
    const status = createSimpleEnum(['active', 'inactive'])

    expect(status.isValid(1)).toBe(true)
    expect(status.isValid(2)).toBe(true)
    expect(status.isValid(3)).toBe(false)
  })

  it('applies a custom label formatter', () => {
    const status = createSimpleEnum(['active', 'inactive'], key => key.toUpperCase())

    expect(status.getLabel(1)).toBe('ACTIVE')
    expect(status.getLabel(2)).toBe('INACTIVE')
  })

  it('handles an empty keys array', () => {
    const empty = createSimpleEnum([])

    expect(empty.Entries).toEqual([])
    expect(empty.getValues()).toEqual([])
    expect(empty.isValid(1)).toBe(false)
  })
})

describe('createEnumFromValues', () => {
  it('creates enum values from the given key-value pairs', () => {
    const status = createEnumFromValues({ DRAFT: 0, IN_PROGRESS: 1, DONE: 2 })

    expect(status.DRAFT).toBe(0)
    expect(status.IN_PROGRESS).toBe(1)
    expect(status.DONE).toBe(2)
  })

  it('defaults labels to title-cased keys', () => {
    const status = createEnumFromValues({ DRAFT: 0, IN_PROGRESS: 1 })

    expect(status.Labels).toEqual({ 0: 'Draft', 1: 'In Progress' })
  })

  it('generates select options from the default labels', () => {
    const status = createEnumFromValues({ DRAFT: 0, IN_PROGRESS: 1 })

    expect(status.getSelectOptions()).toEqual([
      { label: 'Draft', value: 0 },
      { label: 'In Progress', value: 1 },
    ])
  })

  it('validates values including zero', () => {
    const status = createEnumFromValues({ DRAFT: 0, DONE: 2 })

    expect(status.isValid(0)).toBe(true)
    expect(status.isValid(2)).toBe(true)
    expect(status.isValid(1)).toBe(false)
  })

  it('applies a custom label formatter', () => {
    const status = createEnumFromValues({ DRAFT: 0 }, key => `label-${key}`)

    expect(status.getLabel(0)).toBe('label-DRAFT')
  })

  it('handles an empty key-value map', () => {
    const empty = createEnumFromValues({})

    expect(empty.Entries).toEqual([])
    expect(empty.getValues()).toEqual([])
    expect(empty.isValid(0)).toBe(false)
  })

  it('handles a single value', () => {
    const single = createEnumFromValues({ DRAFT: 0 })

    expect(single.Entries).toEqual([{ key: 'DRAFT', value: 0, label: 'Draft' }])
    expect(single.getValues()).toEqual([0])
    expect(single.getSelectOptions()).toEqual([{ label: 'Draft', value: 0 }])
  })
})
