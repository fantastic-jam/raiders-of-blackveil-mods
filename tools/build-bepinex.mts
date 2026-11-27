/**
 * Builds a BepInEx 6 IL2CPP distribution that can generate interop assemblies for this game.
 *
 * Stock be.793 bundles a Cpp2IL that stops at metadata v106; the game is v107. Newer Cpp2IL builds
 * depend on AsmResolver 6.x and can't be loaded in-process next to Il2CppInterop 1.5.3
 * (BepInEx#1395, #1428, Il2CppInterop#295). tools/bepinex/external-cpp2il.patch makes BepInEx run a
 * standalone Cpp2IL in its own process instead — the same split `pnpm run setup` uses.
 *
 * Unity 6000.5 also reordered the native Il2CppClass struct (events/properties/nestedTypes moved after
 * typeHierarchy); Il2CppInterop 1.5.3's newest layout is 2023.1, so class injection crashed.
 * tools/bepinex/il2cppinterop-unity6000_5-class.patch adds a 6000.5+ struct handler.
 *
 * Output: dist/BepInEx-IL2CPP-RoB/ (game-folder layout) and dist/BepInEx-IL2CPP-RoB.zip.
 * Differs from be.793 by BepInEx.Unity.IL2CPP.dll and Il2CppInterop.Runtime.dll (same assembly
 * versions), plus BepInEx/cpp2il/.
 */
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import {
  BEPINEX_BUILD,
  BEPINEX_COMMIT,
  BEPINEX_URL,
  CPP2IL_ARTIFACT_NET472,
  IL2CPPINTEROP_VERSION,
  downloadAndExtract,
  downloadCpp2Il,
} from './lib/il2cpp.mts'
import { REPO_ROOT } from './lib/mod.mts'
import { createZip } from './lib/zip.mts'

const PATCH = path.join(REPO_ROOT, 'tools', 'bepinex', 'external-cpp2il.patch')
const INTEROP_PATCH = path.join(
  REPO_ROOT,
  'tools',
  'bepinex',
  'il2cppinterop-unity6000_5-class.patch',
)
const WORK_DIR = path.join(REPO_ROOT, 'dist', 'bepinex-build')
const SRC_DIR = path.join(WORK_DIR, 'BepInEx')
const INTEROP_SRC_DIR = path.join(WORK_DIR, 'Il2CppInterop')
const INTEROP_BUILT_DLL = path.join(
  INTEROP_SRC_DIR,
  'bin',
  'Il2CppInterop.Runtime',
  'net6.0',
  'Il2CppInterop.Runtime.dll',
)
const OUT_DIR = path.join(REPO_ROOT, 'dist', 'BepInEx-IL2CPP-RoB')
const OUT_ZIP = `${OUT_DIR}.zip`
const IL2CPP_PROJECT = path.join(
  'Runtimes',
  'Unity',
  'BepInEx.Unity.IL2CPP',
  'BepInEx.Unity.IL2CPP.csproj',
)
const BUILT_DLL = path.join(
  SRC_DIR,
  'Runtimes',
  'Unity',
  'BepInEx.Unity.IL2CPP',
  'bin',
  'Unity.IL2CPP',
  'BepInEx.Unity.IL2CPP.dll',
)

function run(cmd: string, args: string[], cwd?: string): void {
  execFileSync(cmd, args, { stdio: 'inherit', cwd })
}

function checkoutSource(): void {
  if (fs.existsSync(SRC_DIR)) fs.rmSync(SRC_DIR, { recursive: true, force: true })
  fs.mkdirSync(WORK_DIR, { recursive: true })
  console.log(`Cloning BepInEx @ ${BEPINEX_COMMIT}...`)
  run('git', [
    'clone',
    '--quiet',
    '--filter=blob:none',
    'https://github.com/BepInEx/BepInEx.git',
    SRC_DIR,
  ])
  run('git', ['checkout', '--quiet', BEPINEX_COMMIT], SRC_DIR)
  run('git', ['apply', '--whitespace=nowarn', PATCH], SRC_DIR)
}

function buildIl2CppInteropRuntime(): void {
  if (fs.existsSync(INTEROP_SRC_DIR)) fs.rmSync(INTEROP_SRC_DIR, { recursive: true, force: true })
  console.log(`Cloning Il2CppInterop @ v${IL2CPPINTEROP_VERSION}...`)
  run('git', [
    'clone',
    '--quiet',
    '--filter=blob:none',
    'https://github.com/BepInEx/Il2CppInterop.git',
    INTEROP_SRC_DIR,
  ])
  run('git', ['checkout', '--quiet', `v${IL2CPPINTEROP_VERSION}`], INTEROP_SRC_DIR)
  run('git', ['apply', '--whitespace=nowarn', INTEROP_PATCH], INTEROP_SRC_DIR)
  // global.json pins the .NET 8 SDK; any newer SDK builds the net6.0 runtime fine.
  fs.rmSync(path.join(INTEROP_SRC_DIR, 'global.json'), { force: true })
  console.log('Building Il2CppInterop.Runtime...')
  run(
    'dotnet',
    [
      'build',
      path.join('Il2CppInterop.Runtime', 'Il2CppInterop.Runtime.csproj'),
      '-c',
      'Release',
      '-p:IsPackable=false',
      '-v',
      'minimal',
    ],
    INTEROP_SRC_DIR,
  )
  if (!fs.existsSync(INTEROP_BUILT_DLL))
    throw new Error(`Build output not found: ${INTEROP_BUILT_DLL}`)
}

function buildRuntime(): void {
  console.log('Building BepInEx.Unity.IL2CPP...')
  // -p:IsPackable=false: the upstream pack step expects repo assets that the build doesn't need.
  run(
    'dotnet',
    ['build', IL2CPP_PROJECT, '-c', 'Release', '-p:IsPackable=false', '-v', 'minimal'],
    SRC_DIR,
  )
  if (!fs.existsSync(BUILT_DLL)) throw new Error(`Build output not found: ${BUILT_DLL}`)
}

async function assemble(): Promise<void> {
  if (fs.existsSync(OUT_DIR)) fs.rmSync(OUT_DIR, { recursive: true })
  console.log(`Downloading BepInEx be.${BEPINEX_BUILD}...`)
  await downloadAndExtract(BEPINEX_URL, OUT_DIR)

  fs.copyFileSync(BUILT_DLL, path.join(OUT_DIR, 'BepInEx', 'core', 'BepInEx.Unity.IL2CPP.dll'))
  fs.copyFileSync(
    INTEROP_BUILT_DLL,
    path.join(OUT_DIR, 'BepInEx', 'core', 'Il2CppInterop.Runtime.dll'),
  )

  console.log('Downloading Cpp2IL (net472)...')
  downloadCpp2Il(CPP2IL_ARTIFACT_NET472, path.join(OUT_DIR, 'BepInEx', 'cpp2il'))

  fs.writeFileSync(
    path.join(OUT_DIR, 'BepInEx', 'cpp2il', 'README.txt'),
    [
      `BepInEx 6 be.${BEPINEX_BUILD} with an external-Cpp2IL patch (raiders-of-blackveil-mods/tools/bepinex).`,
      'BepInEx runs this Cpp2IL in a separate process to read IL2CPP metadata newer than its bundled Cpp2IL.',
      'Config: [IL2CPP] ExternalCpp2ILPath in BepInEx/config/BepInEx.cfg (empty = use the bundled Cpp2IL).',
      '',
    ].join('\n'),
    'utf8',
  )

  if (fs.existsSync(OUT_ZIP)) fs.rmSync(OUT_ZIP)
  createZip(OUT_DIR, OUT_ZIP)
}

checkoutSource()
buildRuntime()
buildIl2CppInteropRuntime()
await assemble()
console.log(
  `\nDone: ${path.relative(REPO_ROOT, OUT_DIR)}/ and ${path.relative(REPO_ROOT, OUT_ZIP)}`,
)
console.log('Install: copy the folder contents into the game folder (replaces BepInEx 5).')
