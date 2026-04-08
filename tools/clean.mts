import { rmSync } from 'node:fs'
import path from 'node:path'
import { glob } from 'glob'
import { REPO_ROOT } from './lib/mod.mts'

const toRemove = [
  ...glob.sync('mods/**/bin', { cwd: REPO_ROOT, absolute: true }),
  ...glob.sync('mods/**/obj', { cwd: REPO_ROOT, absolute: true }),
  ...glob.sync('dist/*-staging', { cwd: REPO_ROOT, absolute: true }),
]

for (const dir of toRemove) {
  console.log(`Removing ${path.relative(REPO_ROOT, dir)}`)
  rmSync(dir, { recursive: true, force: true })
}

console.log('Clean done.')
