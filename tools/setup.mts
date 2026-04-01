import { createHash } from 'node:crypto'
import { execSync, spawnSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import readline from 'node:readline'
import { MODS_DIR, REPO_ROOT } from './lib/mod.mts'

const DEFAULT_GAME_ROOT = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Raiders of Blackveil'
const BEPINEX_BUILDS_URL = 'https://builds.bepinex.dev/projects/bepinex_be'

function tryRun(cmd: string): string | null {
  try {
    return execSync(cmd, { encoding: 'utf8' }).trim()
  } catch {
    return null
  }
}

function autoDetectGameRoot(): string | null {
  if (process.platform !== 'win32') return null
  const candidates = [
    'HKCU:\\Software\\Valve\\Steam',
    'HKLM:\\SOFTWARE\\WOW6432Node\\Valve\\Steam',
  ]
  for (const key of candidates) {
    const result = tryRun(
      `powershell -NoProfile -Command "(Get-ItemProperty '${key}' -ErrorAction SilentlyContinue).SteamPath"`,
    )
    if (result) {
      const gamePath = path.join(result, 'steamapps', 'common', 'Raiders of Blackveil')
      if (fs.existsSync(path.join(gamePath, 'RoB.exe'))) return gamePath
    }
  }
  return null
}

async function promptGameRoot(): Promise<string> {
  const autoDetected = autoDetectGameRoot()
  if (autoDetected) {
    console.log(`Auto-detected game path: ${autoDetected}`)
    return autoDetected
  }
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout })
  return new Promise((resolve) => {
    rl.question(
      `Enter game path (Enter for default: ${DEFAULT_GAME_ROOT}): `,
      (answer: string) => {
        rl.close()
        resolve(answer.trim() || DEFAULT_GAME_ROOT)
      },
    )
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

function installBepInEx(gameRoot: string): void {
  const coreDll = path.join(gameRoot, 'BepInEx', 'core', 'BepInEx.Core.dll')
  if (fs.existsSync(coreDll)) {
    console.log('BepInEx 6.x already present.')
    return
  }
  if (!fs.existsSync(path.join(gameRoot, 'RoB.exe'))) {
    console.warn(`WARNING: RoB.exe not found at ${gameRoot}. Skipping BepInEx install.`)
    return
  }
  console.log('Installing BepInEx 6.0.0 BE...')
  execSync(
    `powershell -NoProfile -ExecutionPolicy Bypass -Command "
      $html = (Invoke-WebRequest -UseBasicParsing -Uri '${BEPINEX_BUILDS_URL}').Content
      $m = [regex]::Match($html, 'href=""(/projects/bepinex_be/\\d+/BepInEx-Unity\\.Mono-win-x64-6\\.0\\.0-be\\.[^""/]+\\.zip)""')
      if (-not $m.Success) { throw 'Cannot find BepInEx artifact' }
      $url = 'https://builds.bepinex.dev' + $m.Groups[1].Value
      $zip = Join-Path $env:TEMP 'bepinex_rob.zip'
      Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $zip
      Expand-Archive -Path $zip -DestinationPath '${gameRoot.replace(/\\/g, '\\\\')}' -Force
      Remove-Item $zip -Force"`,
    { stdio: 'inherit' },
  )
  console.log('BepInEx installed.')
}

function decompileIfNeeded(): void {
  const assemblyPath = path.join(REPO_ROOT, 'lib', 'Assembly-CSharp.dll')
  if (!fs.existsSync(assemblyPath)) {
    console.warn('lib/Assembly-CSharp.dll not found. Skipping decompile.')
    return
  }

  const gameSrcDir = path.join(REPO_ROOT, 'game-src')
  const hashFile = path.join(REPO_ROOT, '.game-src-assembly.sha256')
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

  console.log('Decompiling Assembly-CSharp.dll into game-src/...')
  fs.mkdirSync(gameSrcDir, { recursive: true })
  execSync(`ilspycmd -p -o "${gameSrcDir}" "${assemblyPath}"`, { stdio: 'inherit' })
  fs.writeFileSync(hashFile, currentHash, 'ascii')
  console.log('Decompiled game code into game-src/')
}

console.log('=== Raiders of Blackveil Modding Setup ===')
const gameRoot = await promptGameRoot()
console.log(`Game: ${gameRoot}`)
writeUserPaths(gameRoot)
installBepInEx(gameRoot)
copyGameAssemblies(gameRoot)
decompileIfNeeded()
console.log('\nSetup complete. Next: pnpm run build')
