import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, readonly } from 'vue'
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

  it('renders splitEqually and adjustSplits buttons', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('expenses.splitEqually')
    expect(wrapper.text()).toContain('expenses.adjustSplits')
  })
})
