import { spawnSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { extractZip } from './zip.mts'

// ─── Pinned IL2CPP toolchain ─────────────────────────────────────────────────
// The game is IL2CPP (Unity 6000.5, global-metadata v107). Mods compile against Il2CppInterop
// wrapper assemblies and run on BepInEx 6 (Unity.IL2CPP).
//
// Cpp2IL: no release reads metadata v107 yet (pre-release.21 stops at 106), so we pin a CI build
// of the development branch. Replace with a release once one supports v107+.
// Il2CppInterop: must match the Il2CppInterop.Runtime shipped in the pinned BepInEx build.

export const BEPINEX_BUILD = 793
export const BEPINEX_COMMIT = 'dbc82f0'
export const BEPINEX_URL = `https://builds.bepinex.dev/projects/bepinex_be/${BEPINEX_BUILD}/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.${BEPINEX_BUILD}%2B${BEPINEX_COMMIT}.zip`

export const CPP2IL_REPO = 'SamboyCoding/Cpp2IL'
// development @ b5ad444 (2022.1.0-development.1743, metadata 23–108)
export const CPP2IL_RUN_ID = '34694167702'
export const CPP2IL_ARTIFACT_NET9 = 'Cpp2IL-net9-win-x64'
// .NET Framework 4.7.2 build: runs on any Windows without a .NET 9 runtime — shipped inside our BepInEx
export const CPP2IL_ARTIFACT_NET472 = 'Cpp2IL-Netframework472-Windows'

export const IL2CPPINTEROP_VERSION = '1.5.3'
export const IL2CPPINTEROP_URL = `https://github.com/BepInEx/Il2CppInterop/releases/download/v${IL2CPPINTEROP_VERSION}/Il2CppInterop.CLI.${IL2CPPINTEROP_VERSION}.zip`

export const UNITY_LIBS_URL = (version: string): string =>
  `https://unity.bepinex.dev/libraries/${version}.zip`

// ─── Helpers ─────────────────────────────────────────────────────────────────

export async function downloadAndExtract(url: string, destDir: string): Promise<void> {
  const res = await fetch(url, { headers: { 'User-Agent': 'rob-mods-tooling' } })
  if (!res.ok) throw new Error(`Download failed (${res.status}): ${url}`)
  const zipPath = path.join(os.tmpdir(), `rob-tooling-${Date.now()}.zip`)
  fs.writeFileSync(zipPath, Buffer.from(await res.arrayBuffer()))
  try {
    fs.mkdirSync(destDir, { recursive: true })
    extractZip(zipPath, destDir)
  } finally {
    fs.unlinkSync(zipPath)
  }
}

/** Downloads a Cpp2IL CI artifact into destDir (GitHub Actions artifacts need `gh auth login`). */
export function downloadCpp2Il(artifact: string, destDir: string): void {
  const result = spawnSync(
    'gh',
    ['run', 'download', CPP2IL_RUN_ID, '-R', CPP2IL_REPO, '-n', artifact, '-D', destDir],
    { stdio: 'inherit' },
  )
  if (result.status !== 0 || !fs.existsSync(path.join(destDir, 'Cpp2IL.exe'))) {
    throw new Error(
      'Cpp2IL download failed. GitHub Actions artifacts need an authenticated GitHub CLI: run `gh auth login`.',
    )
  }
}
