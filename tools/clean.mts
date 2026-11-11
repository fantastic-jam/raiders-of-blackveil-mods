import { globSync, rmSync } from 'node:fs'
import path from 'node:path'
import { REPO_ROOT } from './lib/mod.mts'

const toRemove = ['mods/**/bin', 'mods/**/obj', 'dist/*-staging'].flatMap((pattern) =>
  globSync(pattern, { cwd: REPO_ROOT }).map((rel) => path.join(REPO_ROOT, rel)),
)

for (const dir of toRemove) {
  console.log(`Removing ${path.relative(REPO_ROOT, dir)}`)
  rmSync(dir, { recursive: true, force: true })
}

console.log('Clean done.')
