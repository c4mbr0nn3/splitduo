import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, readonly, nextTick } from 'vue'
import RecurringExpenseForm from './RecurringExpenseForm.vue'
import type { RecurringFormModel } from './RecurringExpenseForm.vue'
import { apiMock } from '~/composables/api/base.mock'

// Mock all composables RecurringExpenseForm depends on at module boundaries.
// vi.mock factories are auto-hoisted above imports by Vitest.
vi.mock('~/composables/api/base', () => ({
  default: () => apiMock,
}))

const { currentGroupRef, fetchGroupMock, fetchGroupMembersMock } = {
  currentGroupRef: ref(null),
  fetchGroupMock: vi.fn().mockResolvedValue(undefined),
  fetchGroupMembersMock: vi.fn().mockResolvedValue([]),
}

vi.mock('~/composables/resources/useGroups', () => ({
  default: () => ({
    groups: readonly(ref([])),
    fetchGroups: vi.fn().mockResolvedValue(undefined),
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

vi.mock('~/composables/utils/useNotifications', () => ({
  default: () => ({
    showError: vi.fn(),
    showSuccess: vi.fn(),
  }),
}))

// RecurrenceBuilder calls useRecurringExpenses(groupId) for schedule previews —
// stub the composable so no network machinery is pulled in.
vi.mock('~/composables/resources/useRecurringExpenses', () => ({
  default: () => ({
    currentTemplate: readonly(ref(null)),
    fetchTemplate: vi.fn().mockResolvedValue(undefined),
    fetchTemplates: vi.fn().mockResolvedValue(undefined),
    previewOccurrences: vi.fn().mockResolvedValue(undefined),
  }),
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (key: string) => key }),
}))

const mockMembers = [
  { userId: 'u1', user: { firstName: 'Alice', lastName: 'A' }, role: 'member', joinedAt: '', groupId: 'g1' },
  { userId: 'u2', user: { firstName: 'Bob', lastName: 'B' }, role: 'member', joinedAt: '', groupId: 'g1' },
]

const mockGroup = { id: 'g1', name: 'Test Group', createdByUserId: 'u1', memberCount: 2, createdAt: '', updatedAt: '', netBalance: 0, useAliases: false, aliasSetupFinalized: false }

function makeModel(overrides: Partial<RecurringFormModel> = {}): RecurringFormModel {
  return {
    templateId: 't1',
    title: 'Weekly dinner',
    description: '',
    amount: '100',
    paidByUserId: 'u1',
    categoryId: 1,
    paymentModeId: 1,
    requiresApproval: false,
    splits: [],
    aliasSplits: [],
    recurrence: {
      recurrenceMode: 1,
      weekdays: 0,
      dayOfMonth: null,
      interval: null,
      anchorDate: '2026-01-01',
      endDate: null,
    },
    ...overrides,
  }
}

// Mount the form with members loaded and a valid model. The model is passed
// as the modelValue prop; orphaned splits come in as a prop.
async function mountWithMembers(model: RecurringFormModel, props: Record<string, unknown> = {}) {
  fetchGroupMembersMock.mockResolvedValue(mockMembers)
  fetchGroupMock.mockResolvedValue(mockGroup)
  currentGroupRef.value = mockGroup as never

  const wrapper = mount(RecurringExpenseForm, {
    props: {
      title: 'Edit Recurring Expense',
      submitLabel: 'Update Template',
      groupId: 'g1',
      modelValue: model,
      ...props,
    },
    global: {
      mocks: { $t: (key: string) => key },
      stubs: {
        UiCardHeader: { template: '<div />' },
        UserAvatar: { template: '<div />' },
        RecurringRecurrenceBuilder: { template: '<div />' },
      },
    },
  })
  // Wait for onMounted → loadGroupData → members → updateSplits
  await nextTick()
  await new Promise(r => setTimeout(r, 0))
  await nextTick()
  return wrapper
}

describe('RecurringExpenseForm — orphaned splits', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    currentGroupRef.value = null
    fetchGroupMembersMock.mockResolvedValue([])
    fetchGroupMock.mockResolvedValue(undefined)
    fetchAliasesMock.mockResolvedValue([])
    aliasesRef.value = []
    categoriesRef.value = []
    paymentModesRef.value = []
  })

  it('renders orphan rows with badge and disabled amount input', async () => {
    const orphaned = [{ userId: 'gone-1', included: true, splitAmount: 40 }]
    const wrapper = await mountWithMembers(makeModel(), { orphanedSplits: orphaned })

    // Badge label + disabled input are rendered
    expect(wrapper.text()).toContain('recurring.leftGroup')
    const disabledInputs = wrapper
      .findAll('input[inputmode="decimal"]')
      .filter(i => (i.element as HTMLInputElement).disabled)
    expect(disabledInputs.length).toBe(1)
    expect((disabledInputs[0]!.element as HTMLInputElement).value).toBe('40.00')
  })

  it('emitted submit payload excludes the orphaned userId', async () => {
    const orphaned = [{ userId: 'gone-1', included: true, splitAmount: 40 }]
    // Pre-seed model splits as if the watcher merged the saved template splits
    const model = makeModel({
      splits: [
        { userId: 'u1', included: true, splitAmount: 60 },
        { userId: 'u2', included: true, splitAmount: 40 },
      ],
    })
    const wrapper = await mountWithMembers(model, { orphanedSplits: orphaned })

    const form = wrapper.find('form')
    await form.trigger('submit.prevent')
    await nextTick()

    const submitEvents = wrapper.emitted('submit')
    expect(submitEvents, 'submit should be emitted').toBeTruthy()
    const payload = submitEvents![0]![0] as { splits?: Array<{ userId: string, splitAmount: number }> }
    const userIds = (payload.splits ?? []).map(s => s.userId)
    expect(userIds).toContain('u1')
    expect(userIds).toContain('u2')
    expect(userIds).not.toContain('gone-1')
  })

  it('shows no orphan UI when all template splits belong to current members', async () => {
    const wrapper = await mountWithMembers(makeModel(), { orphanedSplits: [] })

    expect(wrapper.text()).not.toContain('recurring.leftGroup')
    const disabledInputs = wrapper
      .findAll('input[inputmode="decimal"]')
      .filter(i => (i.element as HTMLInputElement).disabled)
    expect(disabledInputs.length).toBe(0)
  })

  it('shows the redistributed-amount warning in the totals block', async () => {
    const orphaned = [
      { userId: 'gone-1', included: true, splitAmount: 40 },
      { userId: 'gone-2', included: true, splitAmount: 10 },
    ]
    const wrapper = await mountWithMembers(makeModel(), { orphanedSplits: orphaned })

    expect(wrapper.text()).toContain('recurring.orphanedSplitsRedistributed')
  })

  it('keeps orphan amounts out of the form model splits', async () => {
    const orphaned = [{ userId: 'gone-1', included: true, splitAmount: 40 }]
    const model = makeModel({
      splits: [
        { userId: 'u1', included: true, splitAmount: 60 },
        { userId: 'u2', included: true, splitAmount: 40 },
      ],
    })
    await mountWithMembers(model, { orphanedSplits: orphaned })
    await nextTick()

    const userIds = model.splits.map(s => s.userId)
    expect(userIds).not.toContain('gone-1')
  })
})
