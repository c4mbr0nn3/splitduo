import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import ResumeRecurringDialog from './ResumeRecurringDialog.vue'
import type { RecurringExpenseTemplate } from '~/types/domain'

const template = (overrides: Partial<RecurringExpenseTemplate> = {}): RecurringExpenseTemplate => ({
  id: 'template-1',
  groupId: 'group-1',
  ownerId: 'user-1',
  title: 'Gym',
  amount: 50,
  paidByUserId: 'user-1',
  recurrenceMode: 1,
  weekdays: 0,
  anchorDate: '2026-01-01',
  isActive: false,
  missedCount: 3,
  skippedPeriodDates: ['2026-01-08', '2026-01-15', '2026-01-22'],
  ...overrides,
})

const mountDialog = (tpl: RecurringExpenseTemplate, open = true) => mount(ResumeRecurringDialog, {
  props: {
    template: tpl,
    open,
  },
  global: {
    mocks: { $t: (key: string) => key },
    stubs: {
      // UModal teleports its slots — render them inline for assertions
      UModal: {
        template: '<div><slot name="header" /><slot name="body" /><slot name="footer" /></div>',
      },
    },
  },
})

describe('ResumeRecurringDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders the missed count from the template prop', async () => {
    const wrapper = mountDialog(template({ missedCount: 3 }))
    await nextTick()

    expect(wrapper.text()).toContain('recurring.resume.missedTitle')
    expect(wrapper.text()).toContain('recurring.resume.missedContent')
  })

  it('renders the skipped dates list when provided', async () => {
    const wrapper = mountDialog(template())
    await nextTick()

    // Dates render through formatDateString — assert the list container exists
    expect(wrapper.find('ul').exists()).toBe(true)
    expect(wrapper.findAll('ul li').length).toBe(3)
  })

  it('emits resume with "skip" when the skip button is clicked', async () => {
    const wrapper = mountDialog(template())
    await nextTick()

    const skipButton = wrapper.findAll('button').find(btn => btn.text().includes('recurring.resume.skipButton'))
    expect(skipButton).toBeDefined()
    await skipButton!.trigger('click')

    expect(wrapper.emitted('resume')).toEqual([['skip']])
  })

  it('emits resume with "backfill" when the backfill button is clicked', async () => {
    const wrapper = mountDialog(template())
    await nextTick()

    const backfillButton = wrapper.findAll('button')
      .find(btn => btn.text().includes('recurring.resume.backfillButton'))
    expect(backfillButton).toBeDefined()
    await backfillButton!.trigger('click')

    expect(wrapper.emitted('resume')).toEqual([['backfill']])
  })

  it('emits cancel when the dialog is closed without choosing', async () => {
    const wrapper = mountDialog(template(), true)
    await nextTick()

    await wrapper.setProps({ open: false })
    await nextTick()

    expect(wrapper.emitted('cancel')).toBeTruthy()
  })

  it('emits cancel when the explicit cancel button is clicked', async () => {
    const wrapper = mountDialog(template())
    await nextTick()

    const cancelButton = wrapper.findAll('button')
      .find(btn => btn.text().includes('recurring.resume.cancel'))
    expect(cancelButton).toBeDefined()
    await cancelButton!.trigger('click')
    await nextTick()

    expect(wrapper.emitted('cancel')).toBeTruthy()
    expect(wrapper.emitted('resume')).toBeUndefined()
  })

  it('renders without a resume choice when there are no missed dates', async () => {
    const wrapper = mountDialog(template({ missedCount: 0, skippedPeriodDates: [] }))
    await nextTick()

    expect(wrapper.emitted('resume')).toBeUndefined()
    expect(wrapper.emitted('cancel')).toBeUndefined()
  })
})
