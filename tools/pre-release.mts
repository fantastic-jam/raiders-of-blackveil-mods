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
    all: { type: 'boolean' },
    'dry-run': { type: 'boolean' },
  },
})

const { mod: modName, lib: libName, all } = values
const allMods = listMods()
const allLibs = listLibs()

if (!modName && !libName && !all) {
  console.error(
    `Usage: pre-release --mod <name> | --lib <name> | --all [--dry-run]
  Mods: ${allMods.join(', ')}
  Libs: ${allLibs.join(', ')}`,
  )
  process.exit(1)
}
if ([modName, libName, all].filter(Boolean).length > 1) {
  console.error('--mod, --lib, and --all are mutually exclusive.')
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

const projects: Array<[string, ProjectKind]> = []
if (all) {
  for (const m of allMods) projects.push([m, 'mod'])
  for (const l of allLibs) projects.push([l, 'lib'])
} else if (modName) {
  projects.push([modName, 'mod'])
} else if (libName) {
  projects.push([libName, 'lib'])
}

function runPreRelease(name: string, kind: ProjectKind): void {
  let version: string
  try {
    version = execSync(`frelease --pkg ${name}${dryRun ? ' --dry-run' : ''}`, {
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

  if (dryRun) {
    console.log(`  version  : ${currentVersion} → ${version}  (dry-run, no writes)`)
    return
  }

  writeProjectVersion(name, kind, version)
  console.log(`  version  : ${currentVersion} → ${version}  (${projectVersionFile(name, kind)})`)
}

for (const [name, kind] of projects) {
  console.log(`\n[${name}]`)
  runPreRelease(name, kind)
}

if (!dryRun) {
  const releaseCmd = modName
    ? `pnpm run release -- --mod ${modName}`
    : libName
      ? `pnpm run release -- --lib ${libName}`
      : `pnpm run release -- --all`

  console.log(`\nReview the changes above, then run:\n  ${releaseCmd}`)
}
