import 'dotenv/config'
import { execSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { parseArgs } from 'node:util'
import {
  commit,
  createTag,
  currentBranch,
  dirtyFiles,
  push,
  remoteTagExists,
  stageFile,
  tagExists,
} from './lib/git.mts'
import { createRelease } from './lib/github.mts'
import type { ProjectKind } from './lib/mod.mts'
import {
  REPO_ROOT,
  listLibs,
  listMods,
  projectChangelogFile,
  projectVersionFile,
  readProjectVersion,
} from './lib/mod.mts'

const { values } = parseArgs({
  args: process.argv.slice(2).filter((a) => a !== '--'),
  options: {
    mod: { type: 'string', short: 'm' },
    lib: { type: 'string', short: 'l' },
    'skip-push': { type: 'boolean' },
    'skip-release': { type: 'boolean' },
    'dry-run': { type: 'boolean' },
  },
})

const { mod: modName, lib: libName } = values

const allMods = listMods()
const allLibs = listLibs()

if (!modName && !libName) {
  console.error(
    `Usage: release --mod <name> | --lib <name> [--dry-run] [--skip-push] [--skip-release]
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
let skipPush = values['skip-push'] ?? false
let skipRelease = values['skip-release'] ?? false

if (dryRun) {
  skipPush = true
  skipRelease = true
}
if (skipPush && !skipRelease) {
  console.error('Cannot create a GitHub release when --skip-push is set.')
  process.exit(1)
}

if (!skipRelease && !process.env['GITHUB_TOKEN']) {
  console.error('GITHUB_TOKEN is not set. Copy .env.template to .env and fill in your token.')
  process.exit(1)
}

const branch = currentBranch()
if (branch !== 'main') {
  console.error(`Releases must be from main. Current branch: ${branch}`)
  process.exit(1)
}

// Build the project to release
const projects: Array<[string, ProjectKind]> = []
if (modName) {
  projects.push([modName, 'mod'])
} else if (libName) {
  projects.push([libName, 'lib'])
}

// Build the set of files that are allowed to be dirty (pre-release artifacts)
function toRelative(absPath: string): string {
  return path.relative(REPO_ROOT, absPath).replace(/\\/g, '/')
}

const allowedDirty = new Set<string>()
for (const [name, kind] of projects) {
  allowedDirty.add(toRelative(projectVersionFile(name, kind)))
  allowedDirty.add(toRelative(projectChangelogFile(name, kind)))
}

// Validate dirty files
const dirty = dirtyFiles()
const unexpected = dirty.filter((f) => !allowedDirty.has(f))
if (unexpected.length > 0) {
  console.error(
    `Unexpected dirty files — commit or stash these before releasing:\n${unexpected.map((f) => `  ${f}`).join('\n')}`,
  )
  process.exit(1)
}

async function runRelease(name: string, kind: ProjectKind): Promise<void> {
  const version = readProjectVersion(name, kind)

  if (!version.match(/^\d+\.\d+\.\d+/)) {
    console.error(
      `Version "${version}" in ${projectVersionFile(name, kind)} does not look like a valid version. Did you run pre-release?`,
    )
    process.exit(1)
  }

  const tag = `${name}-v${version}`
  const assetPath = path.join(REPO_ROOT, 'dist', `${name}-${version}.zip`)
  const versionFile = projectVersionFile(name, kind)
  const clFile = projectChangelogFile(name, kind)

  if (tagExists(tag)) {
    console.error(`Tag already exists locally: ${tag}`)
    process.exit(1)
  }
  if (remoteTagExists(tag)) {
    console.error(`Tag already exists on origin: ${tag}`)
    process.exit(1)
  }

  if (!fs.existsSync(clFile)) {
    console.error(`No CHANGELOG.md for ${name}. Use fchange to record entries before releasing.`)
    process.exit(1)
  }
  const notes = execSync(`frelease --pkg ${name} changelog`, {
    cwd: REPO_ROOT,
    encoding: 'utf8',
  }).trim()

  const packageFlag = kind === 'lib' ? `--lib ${name}` : `--mod ${name}`

  if (dryRun) {
    console.log(`
=== DRY RUN - nothing will be modified ===

  ${kind === 'lib' ? 'Lib' : 'Mod'}:      ${name}
  Version:    ${version}  (read from ${versionFile})
  Tag:        ${tag}
  Branch:     ${branch}
  Asset:      ${assetPath}
  Notes from: ${clFile}

  Steps that would run:
    1. Build and package → ${assetPath}
    2. git add ${toRelative(versionFile)} ${toRelative(clFile)}
    3. git commit -m "chore(${name}): release v${version}"
    4. git tag -a ${tag}
    5. git push origin ${branch}
    6. git push origin ${tag}
    7. Create GitHub release ${tag}
    8. Upload ${path.basename(assetPath)}

  Release notes:
${notes}
`)
    return
  }

  console.log(`Packaging ${name} ${version}...`)
  execSync(`node --experimental-strip-types tools/package.mts ${packageFlag}`, {
    cwd: REPO_ROOT,
    stdio: 'inherit',
  })

  stageFile(versionFile)
  stageFile(clFile)
  commit(`chore(${name}): release v${version}`)
  createTag(tag)

  if (!skipPush) push(branch, tag)

  if (!skipRelease) {
    await createRelease(tag, `${name} v${version}`, notes, assetPath)
  }

  console.log(`Released ${tag}`)
}

for (const [name, kind] of projects) {
  await runRelease(name, kind)
}
