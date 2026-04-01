import {
  CommandLineFlagParameter,
  CommandLineParser,
  CommandLineStringParameter,
} from '@rushstack/ts-command-line'
import { execSync } from 'node:child_process'
import semver from 'semver'
import {
  REPO_ROOT,
  listMods,
  modSourceFile,
  readModVersion,
  writeModVersion,
} from './lib/mod.mts'
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

class ReleaseAction extends CommandLineParser {
  private _modName!: CommandLineStringParameter
  private _version!: CommandLineStringParameter
  private _bump!: CommandLineStringParameter
  private _skipPush!: CommandLineFlagParameter
  private _skipRelease!: CommandLineFlagParameter
  private _dryRun!: CommandLineFlagParameter

  constructor() {
    super({ toolFilename: 'release', toolDescription: 'Full release pipeline for a mod.' })
  }

  protected onDefineParameters(): void {
    this._modName = this.defineStringParameter({
      parameterLongName: '--mod',
      parameterShortName: '-m',
      description: `Mod to release. Valid values: ${listMods().join(', ')}`,
      argumentName: 'MOD_NAME',
      required: true,
    })
    this._version = this.defineStringParameter({
      parameterLongName: '--version',
      description: 'Explicit version to release (e.g. 1.2.3)',
      argumentName: 'VERSION',
    })
    this._bump = this.defineStringParameter({
      parameterLongName: '--bump',
      description: 'Auto-increment: major | minor | patch',
      argumentName: 'BUMP',
    })
    this._skipPush = this.defineFlagParameter({
      parameterLongName: '--skip-push',
      description: 'Do not push to origin',
    })
    this._skipRelease = this.defineFlagParameter({
      parameterLongName: '--skip-release',
      description: 'Do not create GitHub release',
    })
    this._dryRun = this.defineFlagParameter({
      parameterLongName: '--dry-run',
      description: 'Preview only — do not modify anything',
    })
  }

  protected async onExecute(): Promise<void> {
    const modName = this._modName.value!
    const validMods = listMods()
    if (!validMods.includes(modName)) {
      console.error(`Unknown mod "${modName}". Valid: ${validMods.join(', ')}`)
      process.exit(1)
    }

    const versionArg = this._version.value
    const bumpArg = this._bump.value
    const dryRun = this._dryRun.value
    let skipPush = this._skipPush.value
    const skipRelease = this._skipRelease.value

    if (versionArg && bumpArg) {
      console.error('Cannot use both --version and --bump.')
      process.exit(1)
    }
    if (!versionArg && !bumpArg) {
      console.error('Either --version or --bump must be specified.')
      process.exit(1)
    }
    if (dryRun) skipPush = true

    if (skipPush && !skipRelease) {
      console.error('Cannot create a GitHub release when --skip-push is set.')
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

    const currentVersion = readModVersion(modName)
    let version: string

    if (bumpArg) {
      const bumped = semver.inc(currentVersion, bumpArg as semver.ReleaseType)
      if (!bumped) {
        console.error(`Could not bump version "${currentVersion}" by "${bumpArg}"`)
        process.exit(1)
      }
      version = bumped
    } else {
      version = versionArg!
    }

    if (!semver.valid(version)) {
      console.error(`Invalid version "${version}". Expected SemVer (e.g. 1.2.0).`)
      process.exit(1)
    }

    const tag = `${modName}-v${version}`
    const assetPath = `dist/${modName}-${version}.zip`
    const prevTag = latestModTag(modName)

    if (tagExists(tag)) {
      console.error(`Tag already exists locally: ${tag}`)
      process.exit(1)
    }
    if (remoteTagExists(tag)) {
      console.error(`Tag already exists on origin: ${tag}`)
      process.exit(1)
    }

    const notes = changelog(modName, prevTag)

    if (dryRun) {
      console.log(`
=== DRY RUN - nothing will be modified ===

  Mod:        ${modName}
  Version:    ${version}
  Tag:        ${tag}
  Branch:     ${branch}
  Prev tag:   ${prevTag ?? '(none - first release)'}
  Asset:      ${assetPath}

  Steps that would run:
    1. Bump Version in ${modSourceFile(modName)}
    2. Build and package -> ${assetPath}
    3. git commit -m "chore(${modName}): release v${version}"
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

    writeModVersion(modName, version)

    try {
      console.log(`Packaging ${modName} ${version}...`)
      execSync(
        `node --experimental-strip-types tools/package.mts --mod ${modName}`,
        { cwd: REPO_ROOT, stdio: 'inherit' },
      )
    } catch (err) {
      writeModVersion(modName, currentVersion)
      throw err
    }

    stageFile(modSourceFile(modName))
    commit(`chore(${modName}): release v${version}`)
    createTag(tag)

    if (!skipPush) {
      push(branch, tag)
    }

    if (!skipRelease) {
      await createRelease(tag, `${modName} v${version}`, notes, assetPath)
    }

    console.log(`Release completed for ${tag}`)
  }
}

await new ReleaseAction().executeAsync()
