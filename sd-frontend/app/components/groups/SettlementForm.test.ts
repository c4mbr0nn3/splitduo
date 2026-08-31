import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, readonly, nextTick } from 'vue'
import SettlementForm from './SettlementForm.vue'

// Mock all composables SettlementForm depends on at module boundaries.
// vi.mock factories are auto-hoisted above imports by Vitest.
const aliasesRef = ref([])

vi.mock('~/composables/resources/useAliases', () => ({
  default: () => ({
    aliases: readonly(aliasesRef),
    fetchAliases: vi.fn().mockResolvedValue([]),
  }),
}))

const paymentModesRef = ref([])

vi.mock('~/composables/resources/usePaymentModes', () => ({
  default: () => ({
    paymentModes: readonly(paymentModesRef),
    isLoading: readonly(ref(false)),
  }),
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (key: string) => key }),
}))

const mockMembers = [
  { userId: 'u1', user: { firstName: 'Alice', lastName: 'A' }, role: 'member', joinedAt: '', groupId: 'g1' },
  { userId: 'u2', user: { firstName: 'Bob', lastName: 'B' }, role: 'member', joinedAt: '', groupId: 'g1' },
]

const mockAliases = [
  { id: 'alias-1', name: 'Us', groupId: 'g1', isSingleton: false, createdAt: 0, updatedAt: 0, members: [{ id: 'u1', firstName: 'Alice', lastName: 'A' }] },
  { id: 'alias-2', name: 'Theirs', groupId: 'g1', isSingleton: false, createdAt: 0, updatedAt: 0, members: [{ id: 'u2', firstName: 'Bob', lastName: 'B' }] },
]

// Native-select stub for USelect (same idiom as the URadioGroup stub in
// ExpenseForm.test.ts) — lets tests set values and inspect items/modelValue
// without exercising Reka UI internals.
const USelectStub = {
  props: ['modelValue', 'items', 'placeholder', 'size', 'loading'],
  emits: ['update:modelValue'],
  template: `
    <select :value="modelValue" @change="$emit('update:modelValue', $event.target.value)">
      <option value="">{{ placeholder }}</option>
      <option v-for="item in items" :key="item.value" :value="item.value">{{ item.label }}</option>
    </select>
  `,
}

// UiInputDate stub — plain input bridging the string model.
const UiInputDateStub = {
  props: ['modelValue', 'size'],
  emits: ['update:modelValue'],
  template: `<input data-testid="input-date" :value="modelValue" @input="$emit('update:modelValue', $event.target.value)" />`,
}

interface MountOptions {
  isAliasMode?: boolean
  members?: typeof mockMembers
  aliases?: typeof mockAliases
  prefill?: Record<string, unknown>
}

function mountForm(props: MountOptions = {}) {
  return mount(SettlementForm, {
    props: {
      groupId: 'g1',
      isAliasMode: props.isAliasMode ?? false,
      members: props.members ?? mockMembers,
      aliases: props.aliases ?? [],
      currentUserId: 'u1',
      prefill: props.prefill,
    },
    global: {
      mocks: { $t: (key: string) => key },
      stubs: {
        USelect: USelectStub,
        UiInputDate: UiInputDateStub,
      },
    },
  })
}

const getFromSelect = (wrapper: ReturnType<typeof mount>) =>
  wrapper.findAll('select')[0]!
const getToSelect = (wrapper: ReturnType<typeof mount>) =>
  wrapper.findAll('select')[1]!

describe('SettlementForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    aliasesRef.value = []
    paymentModesRef.value = []
  })

  it('renders field labels and select options in normal mode', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('settle.fromLabel')
    expect(wrapper.text()).toContain('settle.toLabel')
    expect(wrapper.text()).toContain('settle.amountLabel')

    const fromOptions = getFromSelect(wrapper).findAll('option')
    const labels = fromOptions.map(o => o.text())
    expect(labels).toContain('Alice A')
    expect(labels).toContain('Bob B')
    expect(labels).toContain('settle.selectFrom')
  })

  it('renders alias names as options in alias mode', () => {
    const wrapper = mountForm({ isAliasMode: true, aliases: mockAliases })
    const fromOptions = getFromSelect(wrapper).findAll('option')
    const labels = fromOptions.map(o => o.text())
    expect(labels).toContain('Us')
    expect(labels).toContain('Theirs')
  })

  it('normal mode: defaults from to current user, to stays empty', async () => {
    const wrapper = mountForm()
    await nextTick()
    expect((getFromSelect(wrapper).element as HTMLSelectElement).value).toBe('u1')
    expect((getToSelect(wrapper).element as HTMLSelectElement).value).toBe('')
  })

  it('alias mode: defaults from to the current user alias', async () => {
    const wrapper = mountForm({ isAliasMode: true, aliases: mockAliases })
    await nextTick()
    expect((getFromSelect(wrapper).element as HTMLSelectElement).value).toBe('alias-1')
    expect((getToSelect(wrapper).element as HTMLSelectElement).value).toBe('')
  })

  it('normal mode: prefill selects from/to values', async () => {
    const wrapper = mountForm({ prefill: { from: 'u2', to: 'u1', amount: 25.5 } })
    await nextTick()
    expect((getFromSelect(wrapper).element as HTMLSelectElement).value).toBe('u2')
    expect((getToSelect(wrapper).element as HTMLSelectElement).value).toBe('u1')
  })

  it('normal mode: drops unresolvable from-prefill silently, keeps to-prefill', async () => {
    const wrapper = mountForm({ prefill: { from: 'ghost-user', to: 'u2', amount: 10 } })
    await nextTick()
    expect((getFromSelect(wrapper).element as HTMLSelectElement).value).toBe('u1')
    expect((getToSelect(wrapper).element as HTMLSelectElement).value).toBe('u2')
  })

  it('normal mode: drops to-prefill equal to the payer (excluded from toOptions)', async () => {
    const wrapper = mountForm({ prefill: { from: 'u2', to: 'u2' } })
    await nextTick()
    expect((getFromSelect(wrapper).element as HTMLSelectElement).value).toBe('u2')
    expect((getToSelect(wrapper).element as HTMLSelectElement).value).toBe('')
  })

  it('emits validation errors keyed to fields on empty submit', async () => {
    const wrapper = mountForm()
    await nextTick()

    // Neutralize the default from = current user so from-validation is exercised.
    await getFromSelect(wrapper).setValue('')
    await wrapper.find('form').trigger('submit.prevent')
    await nextTick()

    const text = wrapper.text()
    expect(text).toContain('settle.fromRequired')
    expect(text).toContain('settle.toRequired')
    expect(text).toContain('settle.amountRequired')
    // date defaults to today, so only the three empty fields are flagged
    expect(wrapper.emitted('submit')).toBeFalsy()
  })

  it('emits submit with resolved alias fromUserId in alias mode', async () => {
    const wrapper = mountForm({ isAliasMode: true, aliases: mockAliases })
    await nextTick()

    // From defaults to the current user's alias ('Us', members: [u1]).
    await getToSelect(wrapper).setValue('alias-2')
    const amountInput = wrapper.find('input[inputmode="decimal"]')
    await amountInput.setValue('42.50')
    await amountInput.trigger('blur')

    await wrapper.find('form').trigger('submit.prevent')
    await nextTick()

    const submitEvents = wrapper.emitted('submit')
    expect(submitEvents).toBeTruthy()
    const payload = submitEvents![0]![0] as Record<string, unknown>
    expect(payload.fromUserId).toBe('u1') // current user is a member of 'Us'
    expect(payload.fromAliasId).toBe('alias-1')
    expect(payload.toAliasId).toBe('alias-2')
    expect(payload.amount).toBe(42.5)

    // description omitted when empty
    expect(payload.description).toBeNull()
  })

  it('amount formats to 2dp on blur', async () => {
    const wrapper = mountForm()
    await nextTick()

    const amountInput = wrapper.find('input[inputmode="decimal"]')
    await amountInput.setValue('7,5')
    await amountInput.trigger('blur')
    await nextTick()

    expect((amountInput.element as HTMLInputElement).value).toBe('7.50')
  })
})
