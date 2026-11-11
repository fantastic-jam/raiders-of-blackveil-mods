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
    .filter((f) => readModMetadata(f).deprecated !== true)
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

export function readModPluginId(modName: string): string | null {
  const src = fs.readFileSync(modSourceFile(modName), 'utf8')
  const m = src.match(/\bId\s*=\s*"([^"]+)"/u)
  return m ? m[1] : null
}

export function readModVersion(modName: string): string {
  const src = fs.readFileSync(modSourceFile(modName), 'utf8')
  const m = src.match(/Version\s*=\s*"([^"]+)"/u)
  if (!m) throw new Error(`Could not find Version constant in ${modSourceFile(modName)}`)
  return m[1]
}

export function writeModVersion(modName: string, version: string): void {
  const filePath = modSourceFile(modName)
  const src = fs.readFileSync(filePath, 'utf8')
  if (!/Version\s*=\s*"[^"]*"/u.test(src))
    throw new Error(`Could not find Version constant in ${filePath}`)
  const updated = src.replace(/Version\s*=\s*"[^"]*"/u, `Version = "${version}"`)
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
  const m = src.match(/AssemblyVersion\("([^"]+)"\)/u)
  if (!m) throw new Error(`Could not find AssemblyVersion in ${libAssemblyInfoFile(libName)}`)
  return m[1]
}

export function writeLibVersion(libName: string, version: string): void {
  const filePath = libAssemblyInfoFile(libName)
  const src = fs.readFileSync(filePath, 'utf8')
  const updated = src
    .replace(/AssemblyVersion\("[^"]*"\)/u, `AssemblyVersion("${version}")`)
    .replace(/AssemblyFileVersion\("[^"]*"\)/u, `AssemblyFileVersion("${version}")`)
  if (updated !== src) fs.writeFileSync(filePath, updated, 'utf8')
}

// ─── Unified project helpers ──────────────────────────────────────────────────

export function projectVersionFile(name: string, kind: ProjectKind): string {
  return kind === 'lib' ? libAssemblyInfoFile(name) : modSourceFile(name)
}

export function projectChangelogFile(name: string, kind: ProjectKind): string {
  return path.join(kind === 'lib' ? libDir(name) : modDir(name), 'CHANGELOG.md')
}

export function readProjectVersion(name: string, kind: ProjectKind): string {
  return kind === 'lib' ? readLibVersion(name) : readModVersion(name)
}

export function writeProjectVersion(name: string, kind: ProjectKind, version: string): void {
  if (kind === 'lib') writeLibVersion(name, version)
  else writeModVersion(name, version)
}

/** The metadata.json fields the tooling reads (nexus_* fields are only read by CI scripts). */
export interface ModMetadata {
  patchers?: string[]
  plugin_dlls?: string[]
  deprecated?: boolean
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((item) => typeof item === 'string')
}

function parseModMetadata(json: unknown, metaPath: string): ModMetadata {
  if (typeof json !== 'object' || json === null || Array.isArray(json)) {
    throw new TypeError(`Invalid ${metaPath}: expected a JSON object`)
  }
  const meta: ModMetadata = {}
  if ('patchers' in json && json.patchers !== undefined) {
    if (!isStringArray(json.patchers)) {
      throw new TypeError(`Invalid ${metaPath}: "patchers" must be an array of strings`)
    }
    meta.patchers = json.patchers
  }
  if ('plugin_dlls' in json && json.plugin_dlls !== undefined) {
    if (!isStringArray(json.plugin_dlls)) {
      throw new TypeError(`Invalid ${metaPath}: "plugin_dlls" must be an array of strings`)
    }
    meta.plugin_dlls = json.plugin_dlls
  }
  if ('deprecated' in json && json.deprecated !== undefined) {
    if (typeof json.deprecated !== 'boolean') {
      throw new TypeError(`Invalid ${metaPath}: "deprecated" must be a boolean`)
    }
    meta.deprecated = json.deprecated
  }
  return meta
}

export function readModMetadata(modName: string): ModMetadata {
  const metaPath = path.join(modDir(modName), 'metadata.json')
  if (!fs.existsSync(metaPath)) return {}
  const json: unknown = JSON.parse(fs.readFileSync(metaPath, 'utf8'))
  return parseModMetadata(json, metaPath)
}

// ─── Misc ─────────────────────────────────────────────────────────────────────

export function readUserPaths(): string {
  const propsPath = path.join(MODS_DIR, 'UserPaths.props')
  if (!fs.existsSync(propsPath)) {
    throw new Error('UserPaths.props not found. Run npm run setup first.')
  }
  const xml = fs.readFileSync(propsPath, 'utf8')
  const m = xml.match(/<RaidersOfBlackveilRootPath>([^<]+)<\/RaidersOfBlackveilRootPath>/u)
  if (!m) throw new Error('Could not read RaidersOfBlackveilRootPath from UserPaths.props')
  return m[1].trim()
}
