import 'dotenv/config'
import { execSync } from 'node:child_process'
import path from 'node:path'
import { parseArgs } from 'node:util'
import semver from 'semver'
import {
  changelog,
  commit,
  createTag,
  currentBranch,
  isWorkingTreeClean,
  latestModTag,
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
    version: { type: 'string', short: 'v' },
    bump: { type: 'string', short: 'b' },
    'skip-push': { type: 'boolean' },
    'skip-release': { type: 'boolean' },
    'dry-run': { type: 'boolean' },
  },
})

const { mod: modName, lib: libName, all } = values

const allMods = listMods()
const allLibs = listLibs()

if (!modName && !libName && !all) {
  console.error(
    `Usage: release --mod <name> | --lib <name> | --all [--version 1.2.3 | --bump major|minor|patch] [--dry-run] [--skip-push] [--skip-release]
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

const versionArg = values.version
const bumpArg = values.bump
const dryRun = values['dry-run'] ?? false
let skipPush = values['skip-push'] ?? false
let skipRelease = values['skip-release'] ?? false

if (versionArg && bumpArg) {
  console.error('Cannot use both --version and --bump.')
  process.exit(1)
}
if (!versionArg && !bumpArg) {
  console.error('Either --version or --bump must be specified.')
  process.exit(1)
}
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
if (!dryRun && !isWorkingTreeClean()) {
  console.error('Working tree is not clean. Commit or stash changes first.')
  process.exit(1)
}

// Build the list of projects to release: [name, kind]
const projects: Array<[string, ProjectKind]> = []
if (all) {
  for (const m of allMods) projects.push([m, 'mod'])
  for (const l of allLibs) projects.push([l, 'lib'])
} else if (modName) {
  projects.push([modName, 'mod'])
} else if (libName) {
  projects.push([libName, 'lib'])
}

async function runRelease(name: string, kind: ProjectKind): Promise<void> {
  const currentVersion = readProjectVersion(name, kind)

  let version: string
  if (bumpArg) {
    const bumped = semver.inc(currentVersion, bumpArg as semver.ReleaseType)
    if (!bumped) {
      console.error(`Could not bump version "${currentVersion}" by "${bumpArg}" for ${name}`)
      process.exit(1)
    }
    version = bumped
  } else if (versionArg) {
    version = versionArg
  } else {
    throw new Error('--version or --bump is required') // unreachable
  }

  if (!semver.valid(version)) {
    console.error(`Invalid version "${version}". Expected SemVer (e.g. 1.2.0).`)
    process.exit(1)
  }

  const tag = `${name}-v${version}`
  const assetPath = path.join(REPO_ROOT, 'dist', `${name}-${version}.zip`)
  const prevTag = latestModTag(name)
  const versionFile = projectVersionFile(name, kind)

  if (tagExists(tag)) {
    console.error(`Tag already exists locally: ${tag}`)
    process.exit(1)
  }
  if (remoteTagExists(tag)) {
    console.error(`Tag already exists on origin: ${tag}`)
    process.exit(1)
  }

  const notes = changelog(name, prevTag)
  const packageFlag = kind === 'lib' ? `--lib ${name}` : `--mod ${name}`

  if (dryRun) {
    console.log(`
=== DRY RUN - nothing will be modified ===

  ${kind === 'lib' ? 'Lib' : 'Mod'}:      ${name}
  Version:    ${version}
  Tag:        ${tag}
  Branch:     ${branch}
  Prev tag:   ${prevTag ?? '(none - first release)'}
  Asset:      ${assetPath}

  Steps that would run:
    1. Bump version in ${versionFile}
    2. Build and package -> ${assetPath}
    3. git commit -m "chore(${name}): release v${version}"
    4. git tag -a ${tag}
    5. git push origin ${branch}
    6. git push origin ${tag}
    7. Create GitHub release ${tag}
    8. Upload ${assetPath}

  Release notes:
${notes}
`)
    return
  }

  writeProjectVersion(name, kind, version)

  try {
    console.log(`Packaging ${name} ${version}...`)
    execSync(`node --experimental-strip-types tools/package.mts ${packageFlag}`, {
      cwd: REPO_ROOT,
      stdio: 'inherit',
    })
  } catch (err) {
    writeProjectVersion(name, kind, currentVersion)
    throw err
  }

  stageFile(versionFile)
  commit(`chore(${name}): release v${version}`)
  createTag(tag)

  if (!skipPush) push(branch, tag)

  if (!skipRelease) {
    await createRelease(tag, `${name} v${version}`, notes, assetPath)
  }

  console.log(`Release completed for ${tag}`)
}

for (const [name, kind] of projects) {
  await runRelease(name, kind)
}
