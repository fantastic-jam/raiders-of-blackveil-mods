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

if (!/^(fix|chore|new)\([^)]+\): .+/.test(msg)) {
  console.error(`
ERROR: Bad commit message format.
  Expected : fix|chore|new(scope): message
  Got      : ${msg}

  Valid scopes: Repo, All, <ModName>  (any subfolder of mods/)
`)
  process.exit(1)
}

const scopeMatch = msg.match(/^[^(]+\(([^)]+)\):/)
const scope = scopeMatch![1]

if (scope === 'Repo' || scope === 'All') process.exit(0)

const modsDir = path.resolve(import.meta.dirname, '../mods')
const validMods = fs
  .readdirSync(modsDir)
  .filter((f) => fs.statSync(path.join(modsDir, f)).isDirectory())
  .filter((f) => !f.startsWith('.'))

if (!validMods.includes(scope)) {
  console.error(`
ERROR: Unknown scope "${scope}".
  Valid mod scopes: ${validMods.join(', ')}
  Valid fixed scopes: Repo, All
`)
  process.exit(1)
}

process.exit(0)
