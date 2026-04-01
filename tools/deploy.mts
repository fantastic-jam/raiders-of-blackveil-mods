import { CommandLineParser, CommandLineStringParameter } from '@rushstack/ts-command-line'
import { execSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { REPO_ROOT, listMods, modDir, modDllPath, readUserPaths } from './lib/mod.mts'

class DeployAction extends CommandLineParser {
  private _modName!: CommandLineStringParameter

  constructor() {
    super({ toolFilename: 'deploy', toolDescription: 'Build and deploy a mod to the local game.' })
  }

  protected onDefineParameters(): void {
    this._modName = this.defineStringParameter({
      parameterLongName: '--mod',
      parameterShortName: '-m',
      description: `Mod to deploy. Valid values: ${listMods().join(', ')}`,
      argumentName: 'MOD_NAME',
      required: true,
    })
  }

  protected async onExecute(): Promise<void> {
    const modName = this._modName.value!
    const validMods = listMods()
    if (!validMods.includes(modName)) {
      console.error(`Unknown mod "${modName}". Valid: ${validMods.join(', ')}`)
      process.exit(1)
    }

    const gameRoot = readUserPaths()
    const pluginDir = path.join(gameRoot, 'BepInEx', 'plugins', `fantastic-jam-${modName}`)
    const configDir = path.join(gameRoot, 'BepInEx', 'config')

    console.log('Building solution...')
    const sln = path.join(REPO_ROOT, 'mods', 'raiders-of-blackveil-mods.sln')
    execSync(`dotnet build "${sln}" -c Release`, { cwd: REPO_ROOT, stdio: 'inherit' })

    const dllPath = modDllPath(modName)
    if (!fs.existsSync(dllPath)) {
      throw new Error(`Built DLL not found: ${dllPath}`)
    }

    fs.mkdirSync(pluginDir, { recursive: true })
    fs.copyFileSync(dllPath, path.join(pluginDir, `${modName}.dll`))
    console.log(`Deployed ${modName} to: ${pluginDir}`)

    const pdbPath = dllPath.replace(/\.dll$/, '.pdb')
    if (fs.existsSync(pdbPath)) {
      fs.copyFileSync(pdbPath, path.join(pluginDir, `${modName}.pdb`))
    }

    const locDir = path.join(modDir(modName), 'bin', 'Release', 'Assets', 'Localization')
    if (fs.existsSync(locDir)) {
      const jsonFiles = fs.readdirSync(locDir).filter((f) => f.endsWith('.json'))
      if (jsonFiles.length > 0) {
        const locOut = path.join(pluginDir, 'Assets', 'Localization')
        fs.mkdirSync(locOut, { recursive: true })
        for (const f of jsonFiles) {
          fs.copyFileSync(path.join(locDir, f), path.join(locOut, f))
        }
      }
    }

    const cfgSrcDir = path.join(modDir(modName), 'Config')
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

    console.log('\nDeploy complete.')
  }
}

await new DeployAction().executeAsync()
