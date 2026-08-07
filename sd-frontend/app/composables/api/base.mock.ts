import { vi } from 'vitest'

// Shared mock instance for mocking useApi() in tests — mirrors the methods
// returned by base.ts. Colocated next to the source it mocks.
//
// This is a pre-built instance (not a factory) because vi.mock factories are
// hoisted above imports: a `vi.hoisted(() => createApiMock())` call cannot
// reference an imported function, and a plain `const apiMock = createApiMock()`
// is still in the temporal dead zone when the mocked module is first imported.
// Importing this instance and referencing it from the vi.mock factory works
// because the factory's `default: () => apiMock` closure only dereferences
// `apiMock` when useApi() is actually called — by then the test module body
// has run and the import is initialized.
//
// Vitest isolates module graphs per test file, so every file gets its own
// instance; `vi.clearAllMocks()` in beforeEach resets it between tests.
export const apiMock = {
  get: vi.fn(),
  getPaginated: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  getBlob: vi.fn(),
}
