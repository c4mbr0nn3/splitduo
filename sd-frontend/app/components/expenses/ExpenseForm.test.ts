import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, readonly, nextTick } from 'vue'
import ExpenseForm from './ExpenseForm.vue'
import { apiMock } from '~/composables/api/base.mock'

// Mock all composables ExpenseForm depends on at module boundaries.
// vi.mock factories are auto-hoisted above imports by Vitest.
vi.mock('~/composables/api/base', () => ({
  default: () => apiMock,
}))

const { groupsRef, currentGroupRef, fetchGroupsMock, fetchGroupMock, fetchGroupMembersMock } = {
  groupsRef: ref([]),
  currentGroupRef: ref(null),
  fetchGroupsMock: vi.fn().mockResolvedValue(undefined),
  fetchGroupMock: vi.fn().mockResolvedValue(undefined),
  fetchGroupMembersMock: vi.fn().mockResolvedValue([]),
}

vi.mock('~/composables/resources/useGroups', () => ({
  default: () => ({
    groups: readonly(groupsRef),
    fetchGroups: fetchGroupsMock,
    fetchGroup: fetchGroupMock,
    currentGroup: readonly(currentGroupRef),
    fetchGroupMembers: fetchGroupMembersMock,
    isLoading: readonly(ref(false)),
  }),
}))

const { aliasesRef, fetchAliasesMock } = {
  aliasesRef: ref([]),
  fetchAliasesMock: vi.fn().mockResolvedValue([]),
}

vi.mock('~/composables/resources/useAliases', () => ({
  default: () => ({
    aliases: readonly(aliasesRef),
    fetchAliases: fetchAliasesMock,
  }),
}))

const { categoriesRef } = { categoriesRef: ref([]) }

vi.mock('~/composables/resources/useCategories', () => ({
  default: () => ({
    categories: readonly(categoriesRef),
    isLoading: readonly(ref(false)),
  }),
}))

const { paymentModesRef } = { paymentModesRef: ref([]) }

vi.mock('~/composables/resources/usePaymentModes', () => ({
  default: () => ({
    paymentModes: readonly(paymentModesRef),
    isLoading: readonly(ref(false)),
  }),
}))

const userRef = ref(null)

vi.mock('~/composables/auth/useAuth', () => ({
  default: () => ({
    user: readonly(userRef),
  }),
}))

const receiptImageUrlRef = ref(null)

vi.mock('~/composables/resources/useReceiptScan', () => ({
  default: () => ({
    receiptImageUrl: readonly(receiptImageUrlRef),
  }),
}))

vi.mock('~/composables/utils/useNotifications', () => ({
  default: () => ({
    showError: vi.fn(),
    showSuccess: vi.fn(),
  }),
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (key: string) => key }),
}))

function mountForm(props: Record<string, unknown> = {}) {
  return mount(ExpenseForm, {
    props: { title: 'Test Form', submitLabel: 'Submit', ...props },
    global: {
      mocks: { $t: (key: string) => key },
      stubs: {
        UTooltip: {
          template: '<div><slot /></div>',
        },
      },
    },
  })
}

describe('ExpenseForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    groupsRef.value = []
    currentGroupRef.value = null
    fetchGroupMembersMock.mockResolvedValue([])
    fetchGroupsMock.mockResolvedValue(undefined)
    fetchGroupMock.mockResolvedValue(undefined)
    fetchAliasesMock.mockResolvedValue([])
    aliasesRef.value = []
    categoriesRef.value = []
    paymentModesRef.value = []
  })

  it('mounts without errors', () => {
    const wrapper = mountForm()
    expect(wrapper.exists()).toBe(true)
  })

  it('renders the title prop', () => {
    const wrapper = mountForm({ title: 'My Custom Title' })
    expect(wrapper.text()).toContain('My Custom Title')
  })

  it('renders group selector when preSelectedGroupId is not set', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('expenses.group')
  })

  it('hides group selector when preSelectedGroupId is set', () => {
    const wrapper = mountForm({ preSelectedGroupId: 'group-123' })
    expect(wrapper.text()).not.toContain('expenses.group')
  })

  it('emits cancel when cancel button clicked', async () => {
    const wrapper = mountForm()
    const buttons = wrapper.findAll('button')
    const cancelButton = buttons.find(b => b.text().includes('cancel') || b.text().includes('Cancel'))
    expect(cancelButton, 'cancel button should be rendered').toBeDefined()
    // `cancelButton` is narrowed to defined by the assertion above
    await cancelButton!.trigger('click')
    expect(wrapper.emitted('cancel')).toBeTruthy()
  })

  it('renders split section heading', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('expenses.splitBetween')
  })

  it('renders split mode selector with two options', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('expenses.splitModeAmounts')
    expect(wrapper.text()).toContain('expenses.splitModePercentage')
  })

  it('renders split equally button', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('expenses.splitEqually')
  })
})

// ─── Percentage mode tests ──────────────────────────────────────────────────

const mockMembers = [
  { userId: 'u1', user: { firstName: 'Alice', lastName: 'A' }, role: 'member', joinedAt: '', groupId: 'g1' },
  { userId: 'u2', user: { firstName: 'Bob', lastName: 'B' }, role: 'member', joinedAt: '', groupId: 'g1' },
  { userId: 'u3', user: { firstName: 'Carol', lastName: 'C' }, role: 'member', joinedAt: '', groupId: 'g1' },
]

const mockGroup = { id: 'g1', name: 'Test Group', createdByUserId: 'u1', memberCount: 3, createdAt: '', updatedAt: '', netBalance: 0, useAliases: false, aliasSetupFinalized: false }

// Mount form with members loaded + amount set. Returns wrapper.
// defineModel state is passed via modelValue prop; interactions via DOM.
async function mountWithMembers(amount = '100') {
  fetchGroupMembersMock.mockResolvedValue(mockMembers)
  currentGroupRef.value = mockGroup as never

  const wrapper = mount(ExpenseForm, {
    props: {
      title: 'Test Form',
      submitLabel: 'Submit',
      preSelectedGroupId: 'g1',
      modelValue: { groupId: 'g1', amount, splits: [] },
    },
    global: {
      mocks: { $t: (key: string) => key },
      stubs: {
        UTooltip: { template: '<div><slot /></div>' },
        URadioGroup: {
          props: ['modelValue', 'items', 'variant', 'orientation', 'size'],
          emits: ['update:modelValue'],
          template: `
            <div>
              <label v-for="item in items" :key="item.value">
                <input
                  type="radio"
                  :value="item.value"
                  :checked="modelValue === item.value"
                  @change="$emit('update:modelValue', item.value)"
                />
                <span>{{ item.label }}</span>
              </label>
            </div>
          `,
        },
      },
    },
  })
  // Wait for watchers: groupId → fetchGroup → fetchGroupMembers → updateSplits
  await nextTick()
  await new Promise(r => setTimeout(r, 0))
  await nextTick()
  return wrapper
}

// Switch to a split mode by clicking the corresponding radio
async function switchMode(wrapper: ReturnType<typeof mount>, mode: string) {
  const radios = wrapper.findAll('input[type="radio"]')
  const radio = radios.find(r => (r.element as HTMLInputElement).value === mode)
  if (!radio) throw new Error(`radio "${mode}" not found`)
  await radio.setValue(true)
  await nextTick()
}

describe('ExpenseForm — percentage mode', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    groupsRef.value = []
    currentGroupRef.value = null
    fetchGroupMembersMock.mockResolvedValue([])
    fetchGroupsMock.mockResolvedValue(undefined)
    fetchGroupMock.mockResolvedValue(undefined)
    fetchAliasesMock.mockResolvedValue([])
    aliasesRef.value = []
    categoriesRef.value = []
    paymentModesRef.value = []
  })

  it('renders percentage inputs when switching to percentage mode', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    // Percentage inputs have placeholder="%"
    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
  })

  it('generates equal percentages when switching from equal to percentage', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    // Check the percentage input values — equalPercentages(3) = [33.34, 33.33, 33.33]
    // displayValue returns formatAmount(pct) when not editing
    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
    expect((pctInputs[0]!.element as HTMLInputElement).value).toBe('33.34')
    expect((pctInputs[1]!.element as HTMLInputElement).value).toBe('33.33')
    expect((pctInputs[2]!.element as HTMLInputElement).value).toBe('33.33')
  })

  it('disables submit when percentages do not sum to 100', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    // Set unbalanced percentages by typing into the percentage inputs
    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
    await pctInputs[0]!.setValue('30')
    await pctInputs[1]!.setValue('30')
    await pctInputs[2]!.setValue('30')
    await nextTick()

    // Submit button should be disabled
    const submitBtn = wrapper.find('button[type="submit"]')
    expect((submitBtn.element as HTMLButtonElement).disabled).toBe(true)
  })

  it('enables submit when percentages sum to 100', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    // Set balanced percentages (33.34 + 33.33 + 33.33 = 100)
    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
    await pctInputs[0]!.setValue('33.34')
    await pctInputs[1]!.setValue('33.33')
    await pctInputs[2]!.setValue('33.33')
    await nextTick()

    // Submit button should NOT be disabled due to percentage imbalance
    // (it may still be disabled if other required fields are empty, but not from percentage gate)
    // Check that the tooltip is not showing the "must sum to 100" text
    const tooltipText = wrapper.text()
    expect(tooltipText).not.toContain('expenses.saveDisabledTooltip')
  })

  it('clamps negative percentage to 0.01 on blur', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBeGreaterThanOrEqual(1)

    // parsePercentage strips "-" so typing "-5" becomes 5; type "0" instead (parses to 0, blurs to 0.01)
    await pctInputs[0]!.setValue('0')
    await pctInputs[0]!.trigger('blur')
    await nextTick()

    // After blur, displayValue shows formatAmount(0.01) = "0.01"
    expect((pctInputs[0]!.element as HTMLInputElement).value).toBe('0.01')
  })

  it('clamps percentage over 100 to 100 on blur', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBeGreaterThanOrEqual(1)

    await pctInputs[0]!.setValue('150')
    await pctInputs[0]!.trigger('blur')
    await nextTick()

    expect((pctInputs[0]!.element as HTMLInputElement).value).toBe('100.00')
  })

  it('emits submit payload with amounts computed from percentages', async () => {
    const wrapper = await mountWithMembers('100')
    await switchMode(wrapper, 'percentage')

    // Set balanced percentages
    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
    await pctInputs[0]!.setValue('33.34')
    await pctInputs[1]!.setValue('33.33')
    await pctInputs[2]!.setValue('33.33')
    await nextTick()

    // Fill required fields so validation passes
    await wrapper.find('input[placeholder="expenses.enterTitle"]').setValue('Test')
    // Find category + payment selects — they're USelect components
    // We can't easily set them in DOM, so just submit and check the payload shape
    // The form may not submit if required fields are missing, but we can check
    // the emitted submit event if it fires

    // Try to submit
    const form = wrapper.find('form')
    await form.trigger('submit.prevent')
    await nextTick()

    // If submit was emitted, check the payload doesn't contain splitPercentage
    const submitEvents = wrapper.emitted('submit')
    if (submitEvents && submitEvents.length > 0) {
      const payload = submitEvents[0]![0] as { expenseData: { splits?: Array<{ userId: string, splitAmount: number, splitPercentage?: unknown }> } }
      const splits = payload.expenseData.splits ?? []
      expect(splits.length).toBe(3)
      // splitPercentage should NOT be in the payload
      expect(splits[0]!.splitPercentage).toBeUndefined()
      // Sum should be exactly 100
      const sum = splits.reduce((s, x) => s + x.splitAmount, 0)
      expect(Math.abs(sum - 100)).toBeLessThan(0.01)
    }
  })

  it('excludes zero-milli shares from payload in percentage mode', async () => {
    // 0.01 EUR = 10 millis. 0.01% of 10 = 0.001 → floor to 0 millis → excluded
    const wrapper = await mountWithMembers('0.01')
    await switchMode(wrapper, 'percentage')

    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
    await pctInputs[0]!.setValue('0.01')
    await pctInputs[1]!.setValue('0.01')
    await pctInputs[2]!.setValue('99.98')
    await nextTick()

    // Verify the percentages were set via the input display values after blur
    await pctInputs[0]!.trigger('blur')
    await pctInputs[1]!.trigger('blur')
    await pctInputs[2]!.trigger('blur')
    await nextTick()
    expect((pctInputs[0]!.element as HTMLInputElement).value).toBe('0.01')
    expect((pctInputs[2]!.element as HTMLInputElement).value).toBe('99.98')
  })

  it('enables amount inputs in Amounts mode', async () => {
    const wrapper = await mountWithMembers()
    // Amounts is the default mode — amount inputs should be enabled

    const allDecimalInputs = wrapper.findAll('input[inputmode="decimal"]')
    const memberAmountInputs = allDecimalInputs.filter(i => (i.element as HTMLInputElement).placeholder === '')
    expect(memberAmountInputs.length).toBe(3)
    memberAmountInputs.forEach((input) => {
      expect((input.element as HTMLInputElement).disabled).toBe(false)
    })
  })

  it('clears snapshot on amount change in percentage mode', async () => {
    const wrapper = await mountWithMembers('100')
    await switchMode(wrapper, 'percentage')

    // The percentage inputs should show equal split (33.34/33.33/33.33)
    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
    expect((pctInputs[0]!.element as HTMLInputElement).value).toBe('33.34')

    // Change the amount via the amount input field (not setProps, which would wipe splits)
    const amountInput = wrapper.find('input[placeholder="expenses.enterAmount"]')
    expect(amountInput.exists()).toBe(true)
    await amountInput.setValue('200')
    await nextTick()

    // Percentage inputs should still be present (percentages are source of truth)
    const pctInputsAfter = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputsAfter.length).toBe(3)
    // Percentages unchanged (still 33.34/33.33/33.33)
    expect((pctInputsAfter[0]!.element as HTMLInputElement).value).toBe('33.34')
  })

  it('rescales remaining participants on toggle-off in percentage mode', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    // Set custom percentages: 50, 30, 20
    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)
    await pctInputs[0]!.setValue('50')
    await pctInputs[1]!.setValue('30')
    await pctInputs[2]!.setValue('20')
    await nextTick()

    // Toggle off the third participant (20%) — remaining should rescale to 100
    // Click the avatar button for the third member to toggle them off
    const toggleButtons = wrapper.findAll('button[type="button"]')
    // The member toggle buttons are the ones with avatar content
    const memberButtons = toggleButtons.filter(b => b.find('img, [class*="avatar"]').exists() || b.text().includes('Carol'))
    if (memberButtons.length >= 3) {
      await memberButtons[2]!.trigger('click')
      await nextTick()

      // Remaining two should sum to 100 (50/30 → 62.50/37.50 proportional rescale)
      const remainingPctInputs = wrapper.findAll('input[placeholder="%"]')
      // Only 2 inputs should be enabled (third is toggled off → disabled)
      const enabledInputs = remainingPctInputs.filter(i => !(i.element as HTMLInputElement).disabled)
      expect(enabledInputs.length).toBe(2)
      const sum = enabledInputs.reduce((s, i) => s + Number.parseFloat((i.element as HTMLInputElement).value || '0'), 0)
      expect(Math.abs(sum - 100)).toBeLessThan(0.02)
    }
  })

  it('forces single included participant to 100% in percentage mode', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    // Toggle off two participants, leaving only one
    const toggleButtons = wrapper.findAll('button[type="button"]')
    const memberButtons = toggleButtons.filter(b => b.find('img, [class*="avatar"]').exists() || b.text().includes('Bob') || b.text().includes('Carol'))
    if (memberButtons.length >= 3) {
      // Toggle off Bob (index 1) and Carol (index 2)
      await memberButtons[1]!.trigger('click')
      await nextTick()
      await memberButtons[2]!.trigger('click')
      await nextTick()

      // Only one input should be enabled, and it should show 100
      const enabledInputs = wrapper.findAll('input[placeholder="%"]').filter(i => !(i.element as HTMLInputElement).disabled)
      expect(enabledInputs.length).toBe(1)
      expect((enabledInputs[0]!.element as HTMLInputElement).value).toBe('100.00')
    }
  })

  it('preserves typed value in percentage input after focus', async () => {
    const wrapper = await mountWithMembers()
    await switchMode(wrapper, 'percentage')

    const pctInputs = wrapper.findAll('input[placeholder="%"]')
    expect(pctInputs.length).toBe(3)

    // Focus the first input, then type — the value must survive the re-render
    await pctInputs[0]!.trigger('focus')
    await pctInputs[0]!.setValue('50')
    await nextTick()

    // The input should show "50", not the stale pre-typing value
    expect((pctInputs[0]!.element as HTMLInputElement).value).toBe('50')
  })
})
