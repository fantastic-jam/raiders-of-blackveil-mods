import fs from 'node:fs'
import { listLibs, listMods, projectChangelogFile, readProjectVersion } from './lib/mod.mts'

function readUnreleased(clFile: string): string {
  if (!fs.existsSync(clFile)) return ''
  const content = fs.readFileSync(clFile, 'utf8')
  const start = content.indexOf('## [Unreleased]')
  if (start === -1) return ''
  const afterHeading = content.indexOf('\n', start) + 1
  const nextSection = content.indexOf('\n## ', afterHeading)
  const section =
    nextSection === -1 ? content.slice(afterHeading) : content.slice(afterHeading, nextSection)
  return section.trim()
}

const projects = [
  ...listMods().map((name) => ({ name, kind: 'mod' as const })),
  ...listLibs().map((name) => ({ name, kind: 'lib' as const })),
]
let found = false

for (const { name, kind } of projects) {
  const unreleased = readUnreleased(projectChangelogFile(name, kind))

  if (!unreleased) continue

  found = true
  const version = readProjectVersion(name, kind)
  console.log(`\n── ${name} v${version} ──`)
  console.log(unreleased)
}

if (!found) {
  console.log('All mods are up to date.')
}
