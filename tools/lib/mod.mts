import fs from 'node:fs'
import path from 'node:path'

export const REPO_ROOT = path.resolve(import.meta.dirname, '../..')
export const MODS_DIR = path.join(REPO_ROOT, 'mods')
export const LIBS_DIR = path.join(REPO_ROOT, 'libs')

export type ProjectKind = 'mod' | 'lib'

// ─── Mods ────────────────────────────────────────────────────────────────────

export function listMods(): string[] {
  return fs
    .readdirSync(MODS_DIR)
    .filter((f) => fs.statSync(path.join(MODS_DIR, f)).isDirectory())
    .filter((f) => !f.startsWith('.'))
}

export function modDir(modName: string): string {
  return path.join(MODS_DIR, modName)
}

export function modSourceFile(modName: string): string {
  return path.join(modDir(modName), `${modName}Mod.cs`)
}

export function modOutputDir(modName: string): string {
  return path.join(modDir(modName), 'bin', 'Release')
}

export function modDllPath(modName: string): string {
  return path.join(modOutputDir(modName), `${modName}.dll`)
}

export function readModVersion(modName: string): string {
  const src = fs.readFileSync(modSourceFile(modName), 'utf8')
  const m = src.match(/Version\s*=\s*"([^"]+)"/)
  if (!m) throw new Error(`Could not find Version constant in ${modSourceFile(modName)}`)
  return m[1]
}

export function writeModVersion(modName: string, version: string): void {
  const filePath = modSourceFile(modName)
  const src = fs.readFileSync(filePath, 'utf8')
  if (!/Version\s*=\s*"[^"]*"/.test(src))
    throw new Error(`Could not find Version constant in ${filePath}`)
  const updated = src.replace(/Version\s*=\s*"[^"]*"/, `Version = "${version}"`)
  if (updated !== src) fs.writeFileSync(filePath, updated, 'utf8')
}

// ─── Libs ─────────────────────────────────────────────────────────────────────

export function listLibs(): string[] {
  return fs
    .readdirSync(LIBS_DIR)
    .filter((f) => fs.statSync(path.join(LIBS_DIR, f)).isDirectory())
    .filter((f) => !f.startsWith('.'))
}

export function libDir(libName: string): string {
  return path.join(LIBS_DIR, libName)
}

/** Version source file for libs: Properties/AssemblyInfo.cs */
export function libAssemblyInfoFile(libName: string): string {
  return path.join(libDir(libName), 'Properties', 'AssemblyInfo.cs')
}

export function libOutputDir(libName: string): string {
  return path.join(libDir(libName), 'bin', 'Release')
}

export function libDllPath(libName: string): string {
  return path.join(libOutputDir(libName), `${libName}.dll`)
}

export function readLibVersion(libName: string): string {
  const src = fs.readFileSync(libAssemblyInfoFile(libName), 'utf8')
  const m = src.match(/AssemblyVersion\("([^"]+)"\)/)
  if (!m) throw new Error(`Could not find AssemblyVersion in ${libAssemblyInfoFile(libName)}`)
  return m[1]
}

export function writeLibVersion(libName: string, version: string): void {
  const filePath = libAssemblyInfoFile(libName)
  const src = fs.readFileSync(filePath, 'utf8')
  const updated = src
    .replace(/AssemblyVersion\("[^"]*"\)/, `AssemblyVersion("${version}")`)
    .replace(/AssemblyFileVersion\("[^"]*"\)/, `AssemblyFileVersion("${version}")`)
  if (updated !== src) fs.writeFileSync(filePath, updated, 'utf8')
}

// ─── Unified project helpers ──────────────────────────────────────────────────

export function projectVersionFile(name: string, kind: ProjectKind): string {
  return kind === 'lib' ? libAssemblyInfoFile(name) : modSourceFile(name)
}

export function readProjectVersion(name: string, kind: ProjectKind): string {
  return kind === 'lib' ? readLibVersion(name) : readModVersion(name)
}

export function writeProjectVersion(name: string, kind: ProjectKind, version: string): void {
  if (kind === 'lib') writeLibVersion(name, version)
  else writeModVersion(name, version)
}

export interface ModMetadata {
  nexus_mod_id: string | null
  nexus_file_group_id: string | null
  patchers?: string[]
}

export function readModMetadata(modName: string): ModMetadata {
  const metaPath = path.join(modDir(modName), 'metadata.json')
  if (!fs.existsSync(metaPath)) return { nexus_mod_id: null, nexus_file_group_id: null }
  return JSON.parse(fs.readFileSync(metaPath, 'utf8')) as ModMetadata
}

// ─── Misc ─────────────────────────────────────────────────────────────────────

export function readUserPaths(): string {
  const propsPath = path.join(MODS_DIR, 'UserPaths.props')
  if (!fs.existsSync(propsPath)) {
    throw new Error('UserPaths.props not found. Run npm run setup first.')
  }
  const xml = fs.readFileSync(propsPath, 'utf8')
  const m = xml.match(/<RaidersOfBlackveilRootPath>([^<]+)<\/RaidersOfBlackveilRootPath>/)
  if (!m) throw new Error('Could not read RaidersOfBlackveilRootPath from UserPaths.props')
  return m[1].trim()
}
