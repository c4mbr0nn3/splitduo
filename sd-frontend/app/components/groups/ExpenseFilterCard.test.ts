import { describe, it, expect } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'

import ExpenseFilterCard from './ExpenseFilterCard.vue'

// Nuxt UI components (UCard, UButton, UInput, USelect, UiInputDate) render
// fully in happy-dom, so we mount them for real and query the rendered DOM.
// `$t` is stubbed to return the message key.

const categoryOptions = [
  { value: 'food', label: 'Food' },
  { value: 'transport', label: 'Transport' },
]

const memberOptions = [
  { value: 'u1', label: 'Alice' },
  { value: 'u2', label: 'Bob' },
]

function mountCard(filters: Record<string, string | undefined>, activeFilterCount = 0) {
  return mount(ExpenseFilterCard, {
    props: {
      filters,
      categoryOptions,
      memberOptions,
      activeFilterCount,
    },
    global: {
      mocks: { $t: (key: string) => key },
    },
  })
}

describe('ExpenseFilterCard', () => {
  it('mounts without errors', () => {
    const wrapper = mountCard({ search: '' })
    expect(wrapper.exists()).toBe(true)
  })

  it('renders search input, date range inputs and category select', () => {
    const wrapper = mountCard({ search: 'groceries' })

    const search = wrapper.find('input[type="text"]')
    expect(search.exists()).toBe(true)
    expect(search.attributes('placeholder')).toBe('expenses.filterSearchPlaceholder')
    expect((search.element as HTMLInputElement).value).toBe('groceries')

    // UiInputDate renders a hidden native date input per bound date
    const dateInputs = wrapper.findAll('input[type="date"]')
    expect(dateInputs).toHaveLength(2)

    // USelect renders a combobox button per select
    const comboboxes = wrapper.findAll('[role="combobox"]')
    expect(comboboxes).toHaveLength(2)

    // Labels come from $t keys
    const labels = wrapper.findAll('label').map(l => l.text())
    expect(labels).toEqual([
      'expenses.filterSearch',
      'expenses.filterFrom',
      'expenses.filterTo',
      'expenses.filterCategory',
      'expenses.filterPaidBy',
    ])
  })

  it('emits apply when the apply button is clicked', async () => {
    const wrapper = mountCard({ search: 'groceries' })

    const apply = wrapper.findAll('button').find(b => b.text() === 'expenses.filterApply')
    expect(apply).toBeDefined()
    await apply!.trigger('click')

    expect(wrapper.emitted('apply')).toHaveLength(1)
  })

  it('emits clear when the clear button is clicked', async () => {
    const wrapper = mountCard({ search: 'groceries' }, 1)

    const clear = wrapper.findAll('button').find(b => b.text() === 'expenses.filterClear')
    expect(clear).toBeDefined()
    await clear!.trigger('click')

    expect(wrapper.emitted('clear')).toHaveLength(1)
  })

  it('disables the clear button when no filters are active', () => {
    const wrapper = mountCard({ search: '' }, 0)

    const clear = wrapper.findAll('button').find(b => b.text() === 'expenses.filterClear')
    expect(clear!.attributes('disabled')).toBeDefined()
  })

  it('enables the clear button when filters are active', () => {
    const wrapper = mountCard({ search: 'groceries' }, 1)

    const clear = wrapper.findAll('button').find(b => b.text() === 'expenses.filterClear')
    expect(clear!.attributes('disabled')).toBeUndefined()
  })

  it('disables the apply button when no filter value is pending', () => {
    const wrapper = mountCard({ search: '' })

    const apply = wrapper.findAll('button').find(b => b.text() === 'expenses.filterApply')
    expect(apply!.attributes('disabled')).toBeDefined()
  })

  it('enables the apply button when a filter value is pending', () => {
    const wrapper = mountCard({ search: 'groceries' })

    const apply = wrapper.findAll('button').find(b => b.text() === 'expenses.filterApply')
    expect(apply!.attributes('disabled')).toBeUndefined()
  })

  it('updates the filters model when the search input changes', async () => {
    const filters = ref<Record<string, string | undefined>>({ search: '' })
    const wrapper = mountCard(filters.value)

    await wrapper.find('input[type="text"]').setValue('groceries')

    // defineModel mutates the passed object in place (no update:filters emit)
    expect(filters.value.search).toBe('groceries')
  })
})
