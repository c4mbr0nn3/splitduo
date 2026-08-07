import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'

import InputDate from './InputDate.vue'

// InputDate renders Nuxt UI's UInputDate (reka date-field segments) for real
// in happy-dom. The date is edited segment by segment: focus a segment, then
// type digits via keydown events.

function mountInputDate(modelValue: string | null = null) {
  return mount(InputDate, {
    props: { modelValue },
    global: {
      mocks: { $t: (key: string) => key },
    },
  })
}

async function typeIntoSegment(wrapper: ReturnType<typeof mountInputDate>, index: number, digits: string) {
  const segment = wrapper.findAll('[role="spinbutton"]')[index]
  if (!segment) throw new Error(`Expected a date segment at index ${index}`)
  await segment.trigger('focus')
  await segment.trigger('keydown', { key: 'a', ctrlKey: true }) // select all
  for (const digit of digits) {
    await segment.trigger('keydown', { key: digit })
  }
  await wrapper.vm.$nextTick()
}

describe('InputDate', () => {
  it('mounts without errors', () => {
    const wrapper = mountInputDate('2026-01-15')
    expect(wrapper.exists()).toBe(true)
  })

  it('renders the date input with month, day and year segments', () => {
    const wrapper = mountInputDate('2026-01-15')

    const segments = wrapper.findAll('[role="spinbutton"]')
    expect(segments).toHaveLength(3)
    expect(wrapper.find('[data-segment="month"]').text()).toBe('1')
    expect(wrapper.find('[data-segment="day"]').text()).toBe('15')
    expect(wrapper.find('[data-segment="year"]').text()).toBe('2026')
  })

  it('emits update:modelValue when a segment is edited', async () => {
    const wrapper = mountInputDate('2026-01-15')

    await typeIntoSegment(wrapper, 0, '02')

    expect(wrapper.emitted('update:modelValue')).toEqual([['2026-02-15']])
  })

  it('emits update:modelValue with null when a segment is cleared', async () => {
    const wrapper = mountInputDate('2026-01-15')

    // Clearing a segment leaves the date incomplete, which resolves to null
    const segment = wrapper.findAll('[role="spinbutton"]')[0]
    if (!segment) throw new Error('Expected a month segment')
    await segment.trigger('focus')
    await segment.trigger('keydown', { key: 'a', ctrlKey: true })
    await segment.trigger('keydown', { key: 'Backspace' })
    await wrapper.vm.$nextTick()

    expect(wrapper.emitted('update:modelValue')).toEqual([[null]])
  })

  it('renders empty segments when the model value is null', () => {
    const wrapper = mountInputDate(null)

    expect(wrapper.find('[data-segment="month"]').text()).toBe('mm')
    expect(wrapper.find('[data-segment="day"]').text()).toBe('dd')
    expect(wrapper.find('[data-segment="year"]').text()).toBe('yyyy')
  })

  it('throws on an invalid date string model value', () => {
    expect(() => mountInputDate('not-a-date')).toThrow('Invalid ISO 8601 date string')
  })
})
