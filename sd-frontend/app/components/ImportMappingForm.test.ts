import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import ImportMappingForm from './ImportMappingForm.vue'

// Mock all composables ImportMappingForm depends on at module boundaries.
// vi.mock factories are auto-hoisted above imports by Vitest.

const { fetchGroupMembersMock } = { fetchGroupMembersMock: vi.fn().mockResolvedValue([]) }

vi.mock('~/composables/resources/useGroups', () => ({
  default: () => ({
    fetchGroupMembers: fetchGroupMembersMock,
  }),
}))

const { fetchAliasesMock } = { fetchAliasesMock: vi.fn().mockResolvedValue([]) }

vi.mock('~/composables/resources/useAliases', () => ({
  default: () => ({
    fetchAliases: fetchAliasesMock,
  }),
}))

const { categoriesRef } = { categoriesRef: ref([]) }

vi.mock('~/composables/resources/useCategories', () => ({
  default: () => ({
    categories: categoriesRef,
  }),
}))

const { paymentModesRef } = { paymentModesRef: ref([]) }

vi.mock('~/composables/resources/usePaymentModes', () => ({
  default: () => ({
    paymentModes: paymentModesRef,
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

const sampleAnalysis = {
  fileHash: 'abc123',
  members: [{ key: 'user1', value: 'Alice' }],
  aliases: [{ key: 'alias1', value: 'Group A' }],
  categories: [{ key: '1', value: 'Food' }],
  paymentModes: [{ key: '1', value: 'Cash' }],
}

function mountForm(props: Record<string, unknown> = {}) {
  return mount(ImportMappingForm, {
    props: {
      analysisResults: sampleAnalysis,
      groupId: 'group-123',
      ...props,
    },
    global: {
      mocks: { $t: (key: string) => key },
    },
  })
}

describe('ImportMappingForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    fetchGroupMembersMock.mockResolvedValue([])
    fetchAliasesMock.mockResolvedValue([])
  })

  it('mounts without errors', () => {
    const wrapper = mountForm()
    expect(wrapper.exists()).toBe(true)
  })

  it('renders the configure mappings header', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('imports.configureMappings')
  })

  it('renders user mappings section when analysisResults has members', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('imports.userMappings')
    expect(wrapper.text()).toContain('Alice')
  })

  it('renders alias mappings section when analysisResults has aliases', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('imports.aliasMappings')
    expect(wrapper.text()).toContain('Group A')
  })

  it('renders category mappings section when analysisResults has categories', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('imports.categoryMappings')
    expect(wrapper.text()).toContain('Food')
  })

  it('renders payment mode mappings section when analysisResults has paymentModes', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('imports.paymentModeMappings')
    expect(wrapper.text()).toContain('Cash')
  })

  it('renders the submit button', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('imports.startImport')
  })

  it('renders validation alert when mappings are missing', () => {
    const wrapper = mountForm()
    expect(wrapper.text()).toContain('imports.missingRequiredMappings')
  })

  it('does not render user mappings section when analysisResults has no members', () => {
    const wrapper = mountForm({
      analysisResults: { members: [], aliases: [], categories: [], paymentModes: [] },
    })
    expect(wrapper.text()).not.toContain('imports.userMappings')
  })
})
