import { changelog, latestModTag } from './lib/git.mts'
import { listLibs, listMods, readLibVersion, readModVersion } from './lib/mod.mts'

const projects = [
  ...listMods().map((name) => ({ name, kind: 'mod' as const })),
  ...listLibs().map((name) => ({ name, kind: 'lib' as const })),
]
let found = false

for (const { name, kind } of projects) {
  const lastTag = latestModTag(name)
  const log = changelog(name, lastTag)

  if (log === 'No changes recorded.') continue

  found = true
  const version = kind === 'lib' ? readLibVersion(name) : readModVersion(name)
  const tagLabel = lastTag ?? '(no tag yet)'
  console.log(`\n── ${name} v${version}  [since ${tagLabel}] ──`)
  console.log(log.replace(/^## Changelog\n\n/, ''))
}

if (!found) {
  console.log('All mods are up to date.')
}
