import fs from 'node:fs'
import path from 'node:path'

const msgFile = process.argv[2]
if (!msgFile) {
  console.error('Usage: validate-commit-msg.mts <commit-msg-file>')
  process.exit(1)
}

const msg = fs.readFileSync(msgFile, 'utf8').trim()

// Skip merge, revert, fixup, squash commits
if (/^(Merge |Revert |fixup! |squash! )/.test(msg)) process.exit(0)

if (!/^(fix|chore|new|tidy)\([^)]+\): .+/.test(msg)) {
  console.error(`
ERROR: Bad commit message format.
  Expected : fix|chore|new|tidy(scope): message
  Got      : ${msg}

  Valid scopes: Repo, All, <ModName>, <LibName>
`)
  process.exit(1)
}

const scopeMatch = msg.match(/^[^(]+\(([^)]+)\):/)
if (!scopeMatch) throw new Error(`Unexpected: scope regex did not match validated message: ${msg}`)
const scope = scopeMatch[1]

if (scope === 'Repo' || scope === 'All') process.exit(0)

const repoRoot = path.resolve(import.meta.dirname, '..')

const validScopes = [
  ...fs
    .readdirSync(path.join(repoRoot, 'mods'))
    .filter((f) => fs.statSync(path.join(repoRoot, 'mods', f)).isDirectory())
    .filter((f) => !f.startsWith('.')),
  ...fs
    .readdirSync(path.join(repoRoot, 'libs'))
    .filter((f) => fs.statSync(path.join(repoRoot, 'libs', f)).isDirectory())
    .filter((f) => !f.startsWith('.')),
]

if (!validScopes.includes(scope)) {
  console.error(`
ERROR: Unknown scope "${scope}".
  Valid scopes: ${validScopes.join(', ')}, Repo, All
`)
  process.exit(1)
}

process.exit(0)
