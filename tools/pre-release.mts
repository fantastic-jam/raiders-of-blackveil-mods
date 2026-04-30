import 'dotenv/config'
import { execSync } from 'node:child_process'
import { parseArgs } from 'node:util'
import { remoteTagExists, tagExists } from './lib/git.mts'
import type { ProjectKind } from './lib/mod.mts'
import {
  REPO_ROOT,
  listLibs,
  listMods,
  projectVersionFile,
  readProjectVersion,
  writeProjectVersion,
} from './lib/mod.mts'

const { values } = parseArgs({
  args: process.argv.slice(2).filter((a) => a !== '--'),
  options: {
    mod: { type: 'string', short: 'm' },
    lib: { type: 'string', short: 'l' },
    'dry-run': { type: 'boolean' },
  },
})

const { mod: modName, lib: libName } = values
const allMods = listMods()
const allLibs = listLibs()

if (!modName && !libName) {
  console.error(
    `Usage: pre-release --mod <name> | --lib <name> [--dry-run]
  Mods: ${allMods.join(', ')}
  Libs: ${allLibs.join(', ')}`,
  )
  process.exit(1)
}
if (modName && libName) {
  console.error('--mod and --lib are mutually exclusive.')
  process.exit(1)
}
if (modName && !allMods.includes(modName)) {
  console.error(`Unknown mod "${modName}". Valid: ${allMods.join(', ')}`)
  process.exit(1)
}
if (libName && !allLibs.includes(libName)) {
  console.error(`Unknown lib "${libName}". Valid: ${allLibs.join(', ')}`)
  process.exit(1)
}

const dryRun = values['dry-run'] ?? false

const name = (modName ?? libName)!
const kind: ProjectKind = modName ? 'mod' : 'lib'

function runPreRelease(name: string, kind: ProjectKind): void {
  if (dryRun) {
    try {
      const out = execSync(`frelease --pkg ${name} --dry-run`, { cwd: REPO_ROOT, encoding: 'utf8' })
      process.stdout.write(out)
    } catch (e) {
      console.error(`  frelease failed: ${(e as Error).message}`)
      process.exit(1)
    }
    return
  }

  let version: string
  try {
    version = execSync(`frelease --pkg ${name}`, {
      cwd: REPO_ROOT,
      encoding: 'utf8',
    }).trim()
  } catch (e) {
    console.error(`  frelease failed: ${(e as Error).message}`)
    process.exit(1)
  }

  if (!version.match(/^\d+\.\d+\.\d+$/)) {
    console.error(`  frelease returned unexpected output: "${version}"`)
    process.exit(1)
  }

  const tag = `${name}-v${version}`
  if (tagExists(tag)) {
    console.error(`  Tag already exists locally: ${tag}`)
    process.exit(1)
  }
  if (remoteTagExists(tag)) {
    console.error(`  Tag already exists on origin: ${tag}`)
    process.exit(1)
  }

  const currentVersion = readProjectVersion(name, kind)
  writeProjectVersion(name, kind, version)
  console.log(`  version  : ${currentVersion} → ${version}  (${projectVersionFile(name, kind)})`)
}

console.log(`\n[${name}]`)
runPreRelease(name, kind)

if (!dryRun) {
  const releaseCmd = modName
    ? `pnpm run release -- --mod ${modName}`
    : `pnpm run release -- --lib ${libName}`

  console.log(`\nReview the changes above, then run:\n  ${releaseCmd}`)
}
