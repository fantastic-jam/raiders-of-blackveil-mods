import { createHash } from 'node:crypto'
import { execSync, spawnSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import readline from 'node:readline'
import { MODS_DIR, REPO_ROOT } from './lib/mod.mts'
import { extractZip } from './lib/zip.mts'

const DEFAULT_GAME_ROOT = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Raiders of Blackveil'

function readStoredGameRoot(): string | null {
  const propsPath = path.join(MODS_DIR, 'UserPaths.props')
  if (!fs.existsSync(propsPath)) return null
  const m = fs
    .readFileSync(propsPath, 'utf8')
    .match(/<RaidersOfBlackveilRootPath>([^<]+)<\/RaidersOfBlackveilRootPath>/)
  return m ? m[1].trim() : null
}

async function promptGameRoot(): Promise<string> {
  const stored = readStoredGameRoot()
  const defaultPath = stored ?? DEFAULT_GAME_ROOT
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout })
  return new Promise((resolve) => {
    const label = stored ? `Enter to keep: ${stored}` : `Enter for default: ${DEFAULT_GAME_ROOT}`
    rl.question(`Enter game path (${label}): `, (answer: string) => {
      rl.close()
      resolve(answer.trim() || defaultPath)
    })
  })
}

function writeUserPaths(gameRoot: string): void {
  const propsPath = path.join(MODS_DIR, 'UserPaths.props')
  const xml = `<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <RaidersOfBlackveilRootPath>${gameRoot}</RaidersOfBlackveilRootPath>
  </PropertyGroup>
</Project>
`
  fs.writeFileSync(propsPath, xml, 'utf8')
  console.log(`Wrote ${propsPath}`)
}

function copyGameAssemblies(gameRoot: string): void {
  const src = path.join(gameRoot, 'RoB_Data', 'Managed', 'Assembly-CSharp.dll')
  if (!fs.existsSync(src)) {
    console.warn(`WARNING: ${src} not found. Skipping lib copy.`)
    return
  }
  const libDir = path.join(REPO_ROOT, 'lib')
  fs.mkdirSync(libDir, { recursive: true })
  fs.copyFileSync(src, path.join(libDir, 'Assembly-CSharp.dll'))
  console.log('Copied Assembly-CSharp.dll to lib/')
}

async function installBepInEx(): Promise<void> {
  const bepinexDir = path.join(REPO_ROOT, 'bepinex')
  const coreDll = path.join(bepinexDir, 'BepInEx', 'core', 'BepInEx.dll')
  if (fs.existsSync(coreDll)) {
    console.log('BepInEx 5.x already present.')
    return
  }
  fs.mkdirSync(bepinexDir, { recursive: true })
  console.log('Downloading BepInEx 5 (latest stable)...')

  const releasesRes = await fetch('https://api.github.com/repos/BepInEx/BepInEx/releases', {
    headers: { 'User-Agent': 'setup-script' },
  })
  const releases = (await releasesRes.json()) as {
    tag_name: string
    prerelease: boolean
    assets: { name: string; browser_download_url: string }[]
  }[]
  const rel = releases.find((r) => r.tag_name.startsWith('v5.') && !r.prerelease)
  if (!rel) throw new Error('Cannot find BepInEx 5 release')
  const asset = rel.assets.find((a) => /^BepInEx_win_x64_.*\.zip$/.test(a.name))
  if (!asset) throw new Error('Cannot find BepInEx 5 win-x64 asset')

  const zipPath = path.join(os.tmpdir(), 'bepinex_rob.zip')
  const res = await fetch(asset.browser_download_url)
  fs.writeFileSync(zipPath, Buffer.from(await res.arrayBuffer()))
  await extractZip(zipPath, bepinexDir)
  fs.unlinkSync(zipPath)
  console.log('BepInEx 5 downloaded to bepinex/.')
}

function decompileIfNeeded(gameRoot: string): void {
  const assemblyPath = path.join(REPO_ROOT, 'lib', 'Assembly-CSharp.dll')
  if (!fs.existsSync(assemblyPath)) {
    console.warn('lib/Assembly-CSharp.dll not found. Skipping decompile.')
    return
  }

  const gameSrcDir = path.join(REPO_ROOT, 'game-src')
  const hashFile = path.join(gameSrcDir, '.assembly.sha256')
  const currentHash = createHash('sha256').update(fs.readFileSync(assemblyPath)).digest('hex')
  const storedHash = fs.existsSync(hashFile) ? fs.readFileSync(hashFile, 'utf8').trim() : null

  if (fs.existsSync(gameSrcDir) && storedHash === currentHash) {
    console.log('game-src is up to date. Skipping decompile.')
    return
  }

  if (fs.existsSync(gameSrcDir)) {
    console.log('Assembly hash changed. Removing game-src/ for fresh decompile.')
    fs.rmSync(gameSrcDir, { recursive: true })
  }

  console.log('Installing ilspycmd...')
  spawnSync('dotnet', ['tool', 'install', '--global', 'ilspycmd', '--verbosity', 'minimal'], {
    stdio: 'inherit',
  })

  const managedDir = path.join(gameRoot, 'RoB_Data', 'Managed')
  console.log('Decompiling Assembly-CSharp.dll into game-src/...')
  fs.mkdirSync(gameSrcDir, { recursive: true })
  execSync(`ilspycmd -p -o "${gameSrcDir}" -r "${managedDir}" "${assemblyPath}"`, {
    stdio: 'inherit',
  })
  fs.writeFileSync(hashFile, currentHash, 'ascii')
  console.log('Decompiled game code into game-src/')
}

console.log('=== Raiders of Blackveil Modding Setup ===')
const gameRoot = await promptGameRoot()
console.log(`Game: ${gameRoot}`)
writeUserPaths(gameRoot)
await installBepInEx()
copyGameAssemblies(gameRoot)
decompileIfNeeded(gameRoot)
console.log('\nSetup complete. Next: pnpm run build')
