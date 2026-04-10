import { execSync } from 'node:child_process'
import fs from 'node:fs'
import fsExtra from 'fs-extra'
import path from 'node:path'
import { parseArgs } from 'node:util'
import {
  REPO_ROOT,
  libDllPath,
  libDir,
  libOutputDir,
  listLibs,
  listMods,
  modDir,
  modDllPath,
  modOutputDir,
  readLibVersion,
  readModMetadata,
  readModVersion,
} from './lib/mod.mts'
import { createZip } from './lib/zip.mts'

const { values } = parseArgs({
  args: process.argv.slice(2).filter((a) => a !== '--'),
  options: {
    mod: { type: 'string', short: 'm' },
    lib: { type: 'string', short: 'l' },
  },
})

const { mod: modName, lib: libName } = values

if (!modName && !libName) {
  console.error(
    `Usage: package --mod <name> | --lib <name>
  Mods: ${listMods().join(', ')}
  Libs: ${listLibs().join(', ')}`,
  )
  process.exit(1)
}
if (modName && libName) {
  console.error('--mod and --lib are mutually exclusive.')
  process.exit(1)
}
if (modName && !listMods().includes(modName)) {
  console.error(`Unknown mod "${modName}". Valid: ${listMods().join(', ')}`)
  process.exit(1)
}
if (libName && !listLibs().includes(libName)) {
  console.error(`Unknown lib "${libName}". Valid: ${listLibs().join(', ')}`)
  process.exit(1)
}

const sln = path.join(REPO_ROOT, 'mods', 'raiders-of-blackveil-mods.sln')
const distRoot = path.join(REPO_ROOT, 'dist')

if (modName) {
  await packageMod(modName)
} else if (libName) {
  await packageLib(libName)
}

async function packageMod(name: string): Promise<void> {
  const version = readModVersion(name)
  const dllPath = modDllPath(name)
  const stagingRoot = path.join(distRoot, `${name}-staging`)
  const pluginDir = path.join(stagingRoot, 'plugins', `fantastic-jam-${name}`)

  if (fs.existsSync(stagingRoot)) fs.rmSync(stagingRoot, { recursive: true })

  const outputDir = modOutputDir(name)
  if (fs.existsSync(outputDir)) fs.rmSync(outputDir, { recursive: true })

  console.log(`Building ${name} v${version}...`)
  execSync(`dotnet build "${sln}" -c Release`, { cwd: REPO_ROOT, stdio: 'inherit' })

  if (!fs.existsSync(dllPath)) throw new Error(`Expected build output not found: ${dllPath}`)

  fs.mkdirSync(pluginDir, { recursive: true })
  fs.copyFileSync(dllPath, path.join(pluginDir, `${name}.dll`))

  const assetsDir = path.join(modOutputDir(name), 'Assets')
  if (fs.existsSync(assetsDir)) fsExtra.copySync(assetsDir, path.join(pluginDir, 'Assets'))

  const readmePath = path.join(modDir(name), 'README.md')
  if (fs.existsSync(readmePath)) fs.copyFileSync(readmePath, path.join(pluginDir, 'README.md'))

  const changelogPath = path.join(modDir(name), 'CHANGELOG.md')
  if (fs.existsSync(changelogPath))
    fs.copyFileSync(changelogPath, path.join(pluginDir, 'CHANGELOG.md'))

  const meta = readModMetadata(name)
  if (meta.patchers?.length) {
    const patcherDir = path.join(stagingRoot, 'patchers', `fantastic-jam-${name}`)
    fs.mkdirSync(patcherDir, { recursive: true })
    for (const dll of meta.patchers) {
      const src = path.join(modOutputDir(name), dll)
      if (!fs.existsSync(src)) throw new Error(`Patcher DLL not found: ${src}`)
      fs.copyFileSync(src, path.join(patcherDir, dll))
    }
  }

  await writeZip(name, version, stagingRoot)
}

async function packageLib(name: string): Promise<void> {
  const version = readLibVersion(name)
  const dllPath = libDllPath(name)
  const stagingRoot = path.join(distRoot, `${name}-staging`)
  const pluginDir = path.join(stagingRoot, 'plugins', `fantastic-jam-${name}`)

  if (fs.existsSync(stagingRoot)) fs.rmSync(stagingRoot, { recursive: true })

  const outputDir = libOutputDir(name)
  if (fs.existsSync(outputDir)) fs.rmSync(outputDir, { recursive: true })

  console.log(`Building ${name} v${version}...`)
  execSync(`dotnet build "${sln}" -c Release`, { cwd: REPO_ROOT, stdio: 'inherit' })

  if (!fs.existsSync(dllPath)) throw new Error(`Expected build output not found: ${dllPath}`)

  fs.mkdirSync(pluginDir, { recursive: true })
  fs.copyFileSync(dllPath, path.join(pluginDir, `${name}.dll`))

  const readmePath = path.join(libDir(name), 'README.md')
  if (fs.existsSync(readmePath)) fs.copyFileSync(readmePath, path.join(pluginDir, 'README.md'))

  const changelogPath = path.join(libDir(name), 'CHANGELOG.md')
  if (fs.existsSync(changelogPath))
    fs.copyFileSync(changelogPath, path.join(pluginDir, 'CHANGELOG.md'))

  await writeZip(name, version, stagingRoot)
}

async function writeZip(name: string, version: string, stagingRoot: string): Promise<void> {
  fs.mkdirSync(distRoot, { recursive: true })

  const assetName = `${name}-${version}.zip`
  const assetPath = path.join(distRoot, assetName)
  if (fs.existsSync(assetPath)) fs.rmSync(assetPath)

  await createZip(stagingRoot, assetPath)

  const githubOutput = process.env['GITHUB_OUTPUT']
  if (githubOutput) {
    fs.appendFileSync(githubOutput, `asset_path=${assetPath}\nasset_name=${assetName}\n`)
  }

  console.log(`Created release asset: ${assetPath}`)
}
