import { createHash } from 'node:crypto'
import { execFileSync, spawnSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import readline from 'node:readline'
import { MODS_DIR, REPO_ROOT } from './lib/mod.mts'
import {
  BEPINEX_BUILD,
  BEPINEX_URL,
  CPP2IL_ARTIFACT_NET9,
  CPP2IL_RUN_ID,
  IL2CPPINTEROP_URL,
  IL2CPPINTEROP_VERSION,
  UNITY_LIBS_URL,
  downloadAndExtract,
  downloadCpp2Il,
} from './lib/il2cpp.mts'

// ─── Paths ───────────────────────────────────────────────────────────────────

const DEFAULT_GAME_ROOT = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Raiders of Blackveil'
const GAME_DATA = 'RoB_Data'
const GAME_EXE_NAME = 'RoB'

const IL2CPP_DIR = path.join(REPO_ROOT, 'game-lib', 'il2cpp')
const TOOLS_DIR = path.join(IL2CPP_DIR, 'tools')
// Cpp2IL output: signatures + attributes, no bodies
const DUMMY_DIR = path.join(IL2CPP_DIR, 'dummy')
// Il2CppInterop wrappers: what mods compile against
const INTEROP_DIR = path.join(IL2CPP_DIR, 'interop')
// Decompiled wrappers: the exact interop API
const INTEROP_SRC_DIR = path.join(IL2CPP_DIR, 'interop-src')
const SOURCE_HASH_FILE = path.join(IL2CPP_DIR, '.source.sha256')
const GAME_SRC_DIR = path.join(REPO_ROOT, 'game-src')

// ─── Prompts / user paths ────────────────────────────────────────────────────

function readStoredGameRoot(): string | null {
  const propsPath = path.join(MODS_DIR, 'UserPaths.props')
  if (!fs.existsSync(propsPath)) return null
  const m = fs
    .readFileSync(propsPath, 'utf8')
    .match(/<RaidersOfBlackveilRootPath>([^<]+)<\/RaidersOfBlackveilRootPath>/u)
  return m ? m[1].trim() : null
}

function promptGameRoot(): Promise<string> {
  const stored = readStoredGameRoot()
  const defaultPath = stored ?? DEFAULT_GAME_ROOT
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout })
  return new Promise((resolve) => {
    const label =
      stored !== null && stored !== ''
        ? `Enter to keep: ${stored}`
        : `Enter for default: ${DEFAULT_GAME_ROOT}`
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

// ─── Downloads ───────────────────────────────────────────────────────────────

/** BepInEx 6 IL2CPP — compile references only (bepinex/ is gitignored). */
async function installBepInEx(): Promise<void> {
  const bepinexDir = path.join(REPO_ROOT, 'bepinex')
  const marker = path.join(bepinexDir, 'BepInEx', 'core', 'BepInEx.Unity.IL2CPP.dll')
  if (fs.existsSync(marker)) {
    console.log('BepInEx 6 (IL2CPP) already present.')
    return
  }
  if (fs.existsSync(bepinexDir)) {
    console.log('Replacing bepinex/ with BepInEx 6 (IL2CPP)...')
    fs.rmSync(bepinexDir, { recursive: true })
  }
  console.log(`Downloading BepInEx 6 IL2CPP be.${BEPINEX_BUILD}...`)
  await downloadAndExtract(BEPINEX_URL, bepinexDir)
  console.log('BepInEx 6 downloaded to bepinex/.')
}

async function installCpp2Il(): Promise<string> {
  const exe = path.join(TOOLS_DIR, 'cpp2il', 'Cpp2IL.exe')
  if (fs.existsSync(exe)) return exe
  console.log(`Downloading Cpp2IL (CI build, run ${CPP2IL_RUN_ID})...`)
  await downloadCpp2Il(CPP2IL_ARTIFACT_NET9, path.dirname(exe))
  return exe
}

async function installIl2CppInteropCli(): Promise<string> {
  const dll = path.join(TOOLS_DIR, 'il2cppinterop', 'net6.0', 'Il2CppInterop.CLI.dll')
  if (fs.existsSync(dll)) return dll
  console.log(`Downloading Il2CppInterop CLI ${IL2CPPINTEROP_VERSION}...`)
  await downloadAndExtract(IL2CPPINTEROP_URL, path.join(TOOLS_DIR, 'il2cppinterop'))
  return dll
}

// ─── Interop generation ──────────────────────────────────────────────────────

/** Reads "6000.5.10f1" from globalgamemanagers and returns "6000.5.10" (unity.bepinex.dev naming). */
function readUnityVersion(gameRoot: string): string {
  const ggm = path.join(gameRoot, GAME_DATA, 'globalgamemanagers')
  const head = fs.readFileSync(ggm).subarray(0, 256).toString('latin1')
  const m = head.match(/(\d{4,})\.(\d+)\.(\d+)[abfp]\d+/u)
  if (!m) throw new Error(`Could not read the Unity version from ${ggm}`)
  return `${m[1]}.${m[2]}.${m[3]}`
}

function hashGameBinaries(gameRoot: string): string {
  const hash = createHash('sha256')
  hash.update(fs.readFileSync(path.join(gameRoot, 'GameAssembly.dll')))
  hash.update(
    fs.readFileSync(
      path.join(gameRoot, GAME_DATA, 'il2cpp_data', 'Metadata', 'global-metadata.dat'),
    ),
  )
  return hash.digest('hex')
}

/** Returns true when interop assemblies were (re)generated. */
async function generateInterop(gameRoot: string): Promise<boolean> {
  if (!fs.existsSync(path.join(gameRoot, 'GameAssembly.dll'))) {
    throw new Error(`${gameRoot} has no GameAssembly.dll — not an IL2CPP build of the game?`)
  }

  const currentHash = hashGameBinaries(gameRoot)
  const storedHash = fs.existsSync(SOURCE_HASH_FILE)
    ? fs.readFileSync(SOURCE_HASH_FILE, 'utf8').trim()
    : null
  if (storedHash === currentHash && fs.existsSync(path.join(INTEROP_DIR, 'Assembly-CSharp.dll'))) {
    console.log('Interop assemblies are up to date.')
    return false
  }

  const cpp2il = await installCpp2Il()
  const interopCli = await installIl2CppInteropCli()

  const unityVersion = readUnityVersion(gameRoot)
  const unityLibsDir = path.join(TOOLS_DIR, 'unity-libs', unityVersion)
  if (!fs.existsSync(unityLibsDir)) {
    console.log(`Downloading Unity ${unityVersion} base libraries (unstripping)...`)
    await downloadAndExtract(UNITY_LIBS_URL(unityVersion), unityLibsDir)
  }

  for (const dir of [DUMMY_DIR, INTEROP_DIR, INTEROP_SRC_DIR]) {
    if (fs.existsSync(dir)) fs.rmSync(dir, { recursive: true })
  }

  console.log('Running Cpp2IL (dummy assemblies)...')
  execFileSync(
    cpp2il,
    [
      '--game-path',
      gameRoot,
      '--exe-name',
      GAME_EXE_NAME,
      '--use-processor',
      'attributeinjector',
      '--output-as',
      'dummydll',
      '--output-to',
      DUMMY_DIR,
    ],
    { stdio: 'inherit', cwd: TOOLS_DIR },
  )

  console.log('Running Il2CppInterop (wrapper assemblies)...')
  execFileSync(
    'dotnet',
    [
      interopCli,
      'generate',
      '--input',
      DUMMY_DIR,
      '--output',
      INTEROP_DIR,
      '--unity',
      unityLibsDir,
      '--game-assembly',
      path.join(gameRoot, 'GameAssembly.dll'),
    ],
    { stdio: 'inherit' },
  )

  fs.writeFileSync(SOURCE_HASH_FILE, currentHash, 'ascii')
  console.log(`Interop assemblies written to ${path.relative(REPO_ROOT, INTEROP_DIR)}/`)
  return true
}

// ─── Decompiled references ───────────────────────────────────────────────────

function decompile(assembly: string, outDir: string, refDir: string): void {
  fs.mkdirSync(outDir, { recursive: true })
  execFileSync('ilspycmd', ['-p', '-o', outDir, '-r', refDir, assembly], { stdio: 'inherit' })
}

/**
 * game-src/: Cpp2IL dummy Assembly-CSharp — original signatures and attributes (e.g. [Rpc] keys),
 * method bodies are empty. game-lib/il2cpp/interop-src/: the interop wrapper as mods see it.
 */
function decompileReferences(force: boolean): void {
  if (!force && fs.existsSync(GAME_SRC_DIR) && fs.existsSync(INTEROP_SRC_DIR)) {
    console.log('game-src is up to date. Skipping decompile.')
    return
  }

  console.log('Installing ilspycmd...')
  spawnSync('dotnet', ['tool', 'install', '--global', 'ilspycmd', '--verbosity', 'minimal'], {
    stdio: 'inherit',
  })

  if (fs.existsSync(GAME_SRC_DIR)) fs.rmSync(GAME_SRC_DIR, { recursive: true })
  console.log('Decompiling dummy Assembly-CSharp into game-src/...')
  decompile(path.join(DUMMY_DIR, 'Assembly-CSharp.dll'), GAME_SRC_DIR, DUMMY_DIR)

  if (fs.existsSync(INTEROP_SRC_DIR)) fs.rmSync(INTEROP_SRC_DIR, { recursive: true })
  console.log('Decompiling interop Assembly-CSharp into game-lib/il2cpp/interop-src/...')
  decompile(
    path.join(INTEROP_DIR, 'Assembly-CSharp.dll'),
    path.join(INTEROP_SRC_DIR, 'Assembly-CSharp'),
    INTEROP_DIR,
  )
}

// ─── Main ────────────────────────────────────────────────────────────────────

console.log('=== Raiders of Blackveil Modding Setup (IL2CPP) ===')
const gameRoot = await promptGameRoot()
console.log(`Game: ${gameRoot}`)
writeUserPaths(gameRoot)
await installBepInEx()
const regenerated = await generateInterop(gameRoot)
decompileReferences(regenerated)
console.log('\nSetup complete. Next: pnpm run build')
