import { execSync } from 'node:child_process'
import { parseArgs } from 'node:util'
import { loadDotEnv } from './lib/env.mts'
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

loadDotEnv()

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
const hasMod = modName !== undefined && modName !== ''
const hasLib = libName !== undefined && libName !== ''

if (!hasMod && !hasLib) {
  console.error(
    `Usage: pre-release --mod <name> | --lib <name> [--dry-run]
  Mods: ${allMods.join(', ')}
  Libs: ${allLibs.join(', ')}`,
  )
  process.exit(1)
}
if (hasMod && hasLib) {
  console.error('--mod and --lib are mutually exclusive.')
  process.exit(1)
}
if (hasMod && !allMods.includes(modName)) {
  console.error(`Unknown mod "${modName}". Valid: ${allMods.join(', ')}`)
  process.exit(1)
}
if (hasLib && !allLibs.includes(libName)) {
  console.error(`Unknown lib "${libName}". Valid: ${allLibs.join(', ')}`)
  process.exit(1)
}

const dryRun = values['dry-run'] ?? false

const name = modName ?? libName
if (name === undefined) throw new Error('unreachable: --mod or --lib is required')
const kind: ProjectKind = hasMod ? 'mod' : 'lib'

function runPreRelease(projectName: string, projectKind: ProjectKind): void {
  if (dryRun) {
    try {
      const out = execSync(`frelease --pkg ${projectName} --dry-run`, {
        cwd: REPO_ROOT,
        encoding: 'utf8',
      })
      process.stdout.write(out)
    } catch (e) {
      console.error(`  frelease failed: ${e instanceof Error ? e.message : String(e)}`)
      process.exit(1)
    }
    return
  }

  let version: string
  try {
    version = execSync(`frelease --pkg ${projectName}`, {
      cwd: REPO_ROOT,
      encoding: 'utf8',
    }).trim()
  } catch (e) {
    console.error(`  frelease failed: ${e instanceof Error ? e.message : String(e)}`)
    process.exit(1)
  }

  if (!/^\d+\.\d+\.\d+$/u.test(version)) {
    console.error(`  frelease returned unexpected output: "${version}"`)
    process.exit(1)
  }

  const tag = `${projectName}-v${version}`
  if (tagExists(tag)) {
    console.error(`  Tag already exists locally: ${tag}`)
    process.exit(1)
  }
  if (remoteTagExists(tag)) {
    console.error(`  Tag already exists on origin: ${tag}`)
    process.exit(1)
  }

  const currentVersion = readProjectVersion(projectName, projectKind)
  writeProjectVersion(projectName, projectKind, version)
  console.log(
    `  version  : ${currentVersion} → ${version}  (${projectVersionFile(projectName, projectKind)})`,
  )
}

console.log(`\n[${name}]`)
runPreRelease(name, kind)

if (!dryRun) {
  const releaseCmd = hasMod
    ? `pnpm run release -- --mod ${modName}`
    : `pnpm run release -- --lib ${name}`

  console.log(`\nReview the changes above, then run:\n  ${releaseCmd}`)
}
