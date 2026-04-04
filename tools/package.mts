import { execSync } from 'node:child_process'
import fs from 'node:fs'
import fsExtra from 'fs-extra'
import path from 'node:path'
import { parseArgs } from 'node:util'
import {
  REPO_ROOT,
  listMods,
  modDir,
  modDllPath,
  modOutputDir,
  readModVersion,
} from './lib/mod.mts'
import { createZip } from './lib/zip.mts'

const { values } = parseArgs({
  args: process.argv.slice(2).filter((a) => a !== '--'),
  options: {
    mod: { type: 'string', short: 'm' },
  },
})

const modName = values.mod
if (!modName) {
  console.error(`Usage: package --mod <name>\nValid mods: ${listMods().join(', ')}`)
  process.exit(1)
}
if (!listMods().includes(modName)) {
  console.error(`Unknown mod "${modName}". Valid: ${listMods().join(', ')}`)
  process.exit(1)
}

const version = readModVersion(modName)
const dllPath = modDllPath(modName)
const distRoot = path.join(REPO_ROOT, 'dist')
const stagingRoot = path.join(distRoot, `${modName}-staging`)
const pluginDir = path.join(stagingRoot, 'plugins', `fantastic-jam-${modName}`)

if (fs.existsSync(stagingRoot)) fs.rmSync(stagingRoot, { recursive: true })

const outputDir = modOutputDir(modName)
if (fs.existsSync(outputDir)) fs.rmSync(outputDir, { recursive: true })

console.log(`Building ${modName} v${version}...`)
const sln = path.join(REPO_ROOT, 'mods', 'raiders-of-blackveil-mods.sln')
execSync(`dotnet build "${sln}" -c Release`, { cwd: REPO_ROOT, stdio: 'inherit' })

if (!fs.existsSync(dllPath)) throw new Error(`Expected build output not found: ${dllPath}`)

fs.mkdirSync(pluginDir, { recursive: true })
fs.copyFileSync(dllPath, path.join(pluginDir, `${modName}.dll`))

const assetsDir = path.join(modOutputDir(modName), 'Assets')
if (fs.existsSync(assetsDir)) fsExtra.copySync(assetsDir, path.join(pluginDir, 'Assets'))

const readmePath = path.join(modDir(modName), 'README.md')
if (fs.existsSync(readmePath)) fs.copyFileSync(readmePath, path.join(pluginDir, 'README.md'))

fs.mkdirSync(distRoot, { recursive: true })

const assetName = `${modName}-${version}.zip`
const assetPath = path.join(distRoot, assetName)
if (fs.existsSync(assetPath)) fs.rmSync(assetPath)

await createZip(stagingRoot, assetPath)

const githubOutput = process.env['GITHUB_OUTPUT']
if (githubOutput) {
  fs.appendFileSync(githubOutput, `asset_path=${assetPath}\nasset_name=${assetName}\n`)
}

console.log(`Created release asset: ${assetPath}`)
