import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref } from 'vue'
import { mount } from '@vue/test-utils'

import DatePicker from './DatePicker.vue'

// DatePicker calls useI18n() in its script; mock the module so `t` returns
// the message key and `locale` is a stable ref.
const tMock = vi.hoisted(() => vi.fn((key: string) => key))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: tMock, locale: ref('en') }),
}))

function mountPicker(modelValue: string | null = null) {
  return mount(DatePicker, {
    props: { modelValue },
    global: {
      mocks: { $t: (key: string) => key },
    },
  })
}

// The calendar popover is teleported to document.body and is not removed
// between tests; clear it so querySelector only sees the current popover.
beforeEach(() => {
  document.body.innerHTML = ''
})

describe('DatePicker', () => {
  it('mounts without errors', () => {
    const wrapper = mountPicker('2026-01-15')
    expect(wrapper.exists()).toBe(true)
  })

  it('renders the trigger button with the formatted date label', () => {
    const wrapper = mountPicker('2026-01-15')

    const trigger = wrapper.find('button')
    expect(trigger.exists()).toBe(true)
    expect(trigger.text()).toContain('Jan 15, 2026')
  })

  it('shows the placeholder label when no date is selected', () => {
    const wrapper = mountPicker(null)

    expect(wrapper.find('button').text()).toBe('common.selectDate')
    expect(tMock).toHaveBeenCalledWith('common.selectDate')
  })

  it('opens the calendar popover when the trigger is clicked', async () => {
    const wrapper = mountPicker('2026-01-15')

    await wrapper.find('button').trigger('click')

    const calendar = document.querySelector('[data-slot="grid"]')
    expect(calendar).not.toBeNull()
  })

  it('emits update:modelValue when a calendar day is selected', async () => {
    const wrapper = mountPicker('2026-01-15')

    await wrapper.find('button').trigger('click')
    const cell = document.querySelector('[data-value="2026-01-20"]') as HTMLElement
    expect(cell).not.toBeNull()
    cell.click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:modelValue')).toEqual([['2026-01-20']])
  })

  it('emits update:modelValue with null when the selected day is deselected', async () => {
    const wrapper = mountPicker('2026-01-15')

    await wrapper.find('button').trigger('click')
    const cell = document.querySelector('[data-value="2026-01-15"]') as HTMLElement
    expect(cell).not.toBeNull()
    cell.click()
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
  })

  it('throws on an invalid date string model value', () => {
    expect(() => mountPicker('not-a-date')).toThrow('Invalid ISO 8601 date string')
  })
})
