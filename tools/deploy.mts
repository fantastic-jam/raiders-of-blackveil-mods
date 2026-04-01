import { execSync } from 'node:child_process'
import fs from 'node:fs'
import fsExtra from 'fs-extra'
import path from 'node:path'
import { parseArgs } from 'node:util'
import { REPO_ROOT, listMods, modDir, modDllPath, modOutputDir, readUserPaths } from './lib/mod.mts'

const { values } = parseArgs({
  args: process.argv.slice(2).filter((a) => a !== '--'),
  options: {
    mod: { type: 'string', short: 'm' },
    all: { type: 'boolean' },
  },
})

const { mod: modName, all } = values

if (!modName && !all) {
  console.error(`Usage: deploy --mod <name> | --all\nValid mods: ${listMods().join(', ')}`)
  process.exit(1)
}
if (modName && all) {
  console.error('--mod and --all are mutually exclusive.')
  process.exit(1)
}
if (modName && !listMods().includes(modName)) {
  console.error(`Unknown mod "${modName}". Valid: ${listMods().join(', ')}`)
  process.exit(1)
}

const gameRoot = readUserPaths()
const mods: string[] = all ? listMods() : []
if (!all && modName) mods.push(modName)

for (const mod of mods) {
  const outputDir = modOutputDir(mod)
  if (fs.existsSync(outputDir)) fs.rmSync(outputDir, { recursive: true })
}

console.log('Building solution...')
const sln = path.join(REPO_ROOT, 'mods', 'raiders-of-blackveil-mods.sln')
execSync(`dotnet build "${sln}" -c Release`, { cwd: REPO_ROOT, stdio: 'inherit' })

for (const mod of mods) {
  const pluginDir = path.join(gameRoot, 'BepInEx', 'plugins', `fantastic-jam-${mod}`)
  const configDir = path.join(gameRoot, 'BepInEx', 'config')

  const dllPath = modDllPath(mod)
  if (!fs.existsSync(dllPath)) throw new Error(`Built DLL not found: ${dllPath}`)

  fs.mkdirSync(pluginDir, { recursive: true })
  fs.copyFileSync(dllPath, path.join(pluginDir, `${mod}.dll`))
  console.log(`Deployed ${mod} to: ${pluginDir}`)

  const pdbPath = dllPath.replace(/\.dll$/, '.pdb')
  if (fs.existsSync(pdbPath)) fs.copyFileSync(pdbPath, path.join(pluginDir, `${mod}.pdb`))

  const assetsDir = path.join(modOutputDir(mod), 'Assets')
  if (fs.existsSync(assetsDir)) fsExtra.copySync(assetsDir, path.join(pluginDir, 'Assets'))

  const cfgSrcDir = path.join(modDir(mod), 'Config')
  if (fs.existsSync(cfgSrcDir)) {
    const cfgFiles = fs.readdirSync(cfgSrcDir).filter((f) => f.endsWith('.cfg'))
    if (cfgFiles.length > 0) {
      fs.mkdirSync(configDir, { recursive: true })
      for (const f of cfgFiles) {
        const dest = path.join(configDir, f)
        if (fs.existsSync(dest)) {
          console.log(`Config preserved: ${dest}`)
        } else {
          fs.copyFileSync(path.join(cfgSrcDir, f), dest)
          console.log(`Config deployed: ${dest}`)
        }
      }
    }
  }
}

console.log('\nDeploy complete.')
