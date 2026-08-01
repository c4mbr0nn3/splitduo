import { readdirSync, readFileSync, existsSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const LOCALES_DIR = join(__dirname, '..', 'i18n', 'locales')

/**
 * Recursively collect all dotted key paths from a parsed JSON object.
 * Returns an array of strings like ['common.save', 'auth.login', ...].
 */
function collectKeys(obj, prefix = '') {
  const keys = []
  for (const key of Object.keys(obj)) {
    const fullKey = prefix ? `${prefix}.${key}` : key
    if (obj[key] !== null && typeof obj[key] === 'object' && !Array.isArray(obj[key])) {
      keys.push(...collectKeys(obj[key], fullKey))
    }
    else {
      keys.push(fullKey)
    }
  }
  return keys
}

/**
 * Read a locale JSON file and return its key set.
 * Returns null if the file doesn't exist or can't be parsed.
 */
function readLocaleKeys(filePath) {
  if (!existsSync(filePath)) {
    return null
  }
  const raw = readFileSync(filePath, 'utf-8')
  const parsed = JSON.parse(raw)
  return new Set(collectKeys(parsed))
}

/**
 * Check that all locale files in i18n/locales/ have identical key sets
 * compared to en.json (the reference).
 *
 * Returns { ok: true } on parity, or
 * { ok: false, errors: [{ file, missing: [...], extra: [...] }] } on mismatch.
 */
export function checkLocaleParity() {
  const files = readdirSync(LOCALES_DIR).filter(f => f.endsWith('.json'))

  const enFile = files.find(f => f === 'en.json')
  if (!enFile) {
    return {
      ok: false,
      errors: [{ file: '(system)', missing: [], extra: [], error: 'en.json not found in locales directory' }],
    }
  }

  const enPath = join(LOCALES_DIR, enFile)
  const enKeys = readLocaleKeys(enPath)
  if (!enKeys) {
    return {
      ok: false,
      errors: [{ file: 'en.json', missing: [], extra: [], error: 'Could not read en.json' }],
    }
  }

  const errors = []

  for (const file of files) {
    if (file === 'en.json') continue

    const filePath = join(LOCALES_DIR, file)
    const localeKeys = readLocaleKeys(filePath)

    if (localeKeys === null) {
      // File doesn't exist or can't be parsed — report all en keys as missing
      errors.push({
        file,
        missing: [...enKeys].sort(),
        extra: [],
        error: 'File not found or unreadable',
      })
      continue
    }

    const missing = [...enKeys].filter(k => !localeKeys.has(k)).sort()
    const extra = [...localeKeys].filter(k => !enKeys.has(k)).sort()

    if (missing.length > 0 || extra.length > 0) {
      errors.push({ file, missing, extra })
    }
  }

  return errors.length > 0 ? { ok: false, errors } : { ok: true }
}
