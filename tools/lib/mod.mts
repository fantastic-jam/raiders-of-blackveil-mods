import fs from 'node:fs'
import path from 'node:path'

export const REPO_ROOT = path.resolve(import.meta.dirname, '../..')
export const MODS_DIR = path.join(REPO_ROOT, 'mods')

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
  if (!/Version\s*=\s*"[^"]*"/.test(src)) throw new Error(`Could not find Version constant in ${filePath}`)
  const updated = src.replace(/Version\s*=\s*"[^"]*"/, `Version = "${version}"`)
  if (updated !== src) fs.writeFileSync(filePath, updated, 'utf8')
}

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
