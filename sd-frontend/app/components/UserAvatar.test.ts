import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import UserAvatar from './UserAvatar.vue'

// Mock the composable at the module boundary (auto-import resolves to the
// direct module, so the mock path matches the source file).
const { getAvatarUrlMock } = { getAvatarUrlMock: vi.fn() }

vi.mock('~/composables/resources/useUserAvatar', () => ({
  default: () => ({
    getAvatarUrl: getAvatarUrlMock,
    uploadAvatar: vi.fn(),
    deleteAvatar: vi.fn(),
    isLoading: ref(false),
  }),
}))

// The a11y attrs (role="img", aria-label) are forwarded by UAvatar onto
// whichever element renders the avatar: the fallback span when no image is
// shown, the <img> when a blob URL is set.
const fallback = (wrapper: ReturnType<typeof mountAvatar>) => wrapper.find('[role="img"]')

function mountAvatar(user: { id: string } & Record<string, unknown>, size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl' | '2xl' | '3xl') {
  return mount(UserAvatar, {
    props: {
      user,
      ...(size ? { size } : {}),
    },
    global: {
      mocks: { $t: (key: string) => key },
    },
  })
}

describe('UserAvatar', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    getAvatarUrlMock.mockResolvedValue(null)
  })

  it('renders initials fallback when hasAvatar is false', () => {
    const wrapper = mountAvatar({ id: 'abc', firstName: 'John', lastName: 'Doe' })

    expect(fallback(wrapper).exists()).toBe(true)
    expect(fallback(wrapper).text()).toBe('JD')
    expect(fallback(wrapper).attributes('style')).toContain('background-color:')
  })

  it('renders initials from firstName and lastName', () => {
    const wrapper = mountAvatar({ id: 'abc', firstName: 'John', lastName: 'Doe' })

    expect(fallback(wrapper).text()).toBe('JD')
  })

  it('renders initials from fullName when firstName and lastName are absent', () => {
    const wrapper = mountAvatar({ id: 'abc', fullName: 'Jane Smith' })

    expect(fallback(wrapper).text()).toBe('JS')
  })

  it('renders the first email character when no name is present', () => {
    const wrapper = mountAvatar({ id: 'abc', email: 'test@example.com' })

    expect(fallback(wrapper).text()).toBe('T')
  })

  it('renders a question mark when no name or email is present', () => {
    const wrapper = mountAvatar({ id: 'abc' })

    expect(fallback(wrapper).text()).toBe('?')
  })

  it('derives the fallback color deterministically from the user id', () => {
    const first = mountAvatar({ id: 'user-1', firstName: 'John', lastName: 'Doe' })
    const second = mountAvatar({ id: 'user-1', firstName: 'Jane', lastName: 'Smith' })

    const firstStyle = fallback(first).attributes('style')
    const secondStyle = fallback(second).attributes('style')
    expect(firstStyle).toBe(secondStyle)
    expect(firstStyle).toContain('background-color:')
  })

  it('derives different fallback colors for different user ids', () => {
    const first = mountAvatar({ id: 'user-1', firstName: 'John', lastName: 'Doe' })
    const second = mountAvatar({ id: 'user-2', firstName: 'Jane', lastName: 'Smith' })

    const firstStyle = fallback(first).attributes('style')
    const secondStyle = fallback(second).attributes('style')
    expect(firstStyle).not.toBe(secondStyle)
  })

  it('fetches the avatar blob URL when hasAvatar is true', async () => {
    getAvatarUrlMock.mockResolvedValue('blob:avatar-1')

    mountAvatar({ id: 'user-1', firstName: 'John', lastName: 'Doe', hasAvatar: true })

    await vi.waitFor(() => {
      expect(getAvatarUrlMock).toHaveBeenCalledWith('user-1')
    })
  })

  it('renders the avatar image with the fetched blob URL', async () => {
    getAvatarUrlMock.mockResolvedValue('blob:avatar-1')

    const wrapper = mountAvatar({ id: 'user-1', firstName: 'John', lastName: 'Doe', hasAvatar: true })

    await vi.waitFor(() => {
      expect(wrapper.find('img').attributes('src')).toBe('blob:avatar-1')
    })
  })

  it('does not fetch an avatar when hasAvatar is false', () => {
    mountAvatar({ id: 'user-1', firstName: 'John', lastName: 'Doe' })

    expect(getAvatarUrlMock).not.toHaveBeenCalled()
  })

  // Skipped: happy-dom does not fire `error` events for blob URLs, so the
  // image-error → initials fallback path cannot be exercised in this
  // environment. UAvatar's own error handling is upstream-tested.
  it.skip('falls back to initials when the avatar image fails to load', () => {
    expect(true).toBe(true)
  })
})
