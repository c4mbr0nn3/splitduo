#!/usr/bin/env node
/**
 * Contract check: verifies that `app/types/api.d.ts` is up-to-date with the
 * OpenAPI spec (`docs/api/splitduoapi-v1.yaml`).
 *
 * Regenerates the types to a temp file and diffs them against the committed
 * file. Exits 0 when they match, 1 when stale (run `pnpm gen:api` to fix).
 * The temp file is always cleaned up, even on failure.
 */
import { spawnSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { createRequire } from 'node:module'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const SPEC_PATH = join(__dirname, '..', '..', 'docs', 'api', 'splitduoapi-v1.yaml')
const COMMITTED_PATH = join(__dirname, '..', 'app', 'types', 'api.d.ts')

// Resolve the CLI entry so the script works regardless of how it is invoked
// (pnpm script, direct `node`, or CI) — no reliance on PATH. The package's
// `exports` map rewrites `./*.js` → `./*.mjs`, so resolve its package.json
// (passes through the `./*` mapping) and read the `bin` field instead.
const require = createRequire(import.meta.url)
const pkgPath = require.resolve('openapi-typescript/package.json')
const pkg = JSON.parse(readFileSync(pkgPath, 'utf-8'))
const CLI_PATH = join(dirname(pkgPath), pkg.bin['openapi-typescript'])

const tmpDir = mkdtempSync(join(tmpdir(), 'splitduo-api-'))
const generatedPath = join(tmpDir, 'api.d.ts')

// Exit code is set instead of calling process.exit() so the finally block
// always runs — process.exit() would skip temp-file cleanup on failure.
let exitCode = 0

try {
  const result = spawnSync(process.execPath, [CLI_PATH, SPEC_PATH, '--output', generatedPath], {
    encoding: 'utf-8',
  })

  if (result.status !== 0) {
    console.error('openapi-typescript failed to generate types:')
    console.error(result.stderr || result.stdout || `exit code ${result.status}`)
    exitCode = 1
  }
  else {
    const generated = readFileSync(generatedPath, 'utf-8')
    const committed = readFileSync(COMMITTED_PATH, 'utf-8')

    if (generated === committed) {
      console.log('API types are up-to-date')
    }
    else {
      console.error('API types are stale — run `pnpm gen:api` to regenerate')
      exitCode = 1

      // Show the first few differing lines from both sides.
      const genLines = generated.split('\n')
      const comLines = committed.split('\n')
      const maxLen = Math.max(genLines.length, comLines.length)
      let firstDiff = -1
      for (let i = 0; i < maxLen; i++) {
        if (genLines[i] !== comLines[i]) {
          firstDiff = i
          break
        }
      }

      const start = Math.max(0, firstDiff - 2)
      const end = Math.min(maxLen, firstDiff + 3)
      console.error(`\nFirst difference at line ${firstDiff + 1}:`)
      for (let i = start; i < end; i++) {
        const marker = i === firstDiff ? '>' : ' '
        console.error(`${marker} generated: ${genLines[i] ?? ''}`)
        console.error(`${marker} committed: ${comLines[i] ?? ''}`)
      }
    }
  }
}
finally {
  rmSync(tmpDir, { recursive: true, force: true })
}

process.exit(exitCode)
