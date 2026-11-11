import { unzipSync, zipSync, type Zippable } from 'fflate'
import fs from 'node:fs'
import path from 'node:path'

/**
 * Extracts a zip into destDir. Entries that would land outside destDir (zip-slip: absolute
 * paths or `..` segments) are rejected — archives come from downloads (BepInEx, UnityHotReload).
 */
export function extractZip(zipPath: string, destDir: string): void {
  const root = path.resolve(destDir)
  const entries = unzipSync(fs.readFileSync(zipPath))
  for (const [name, data] of Object.entries(entries)) {
    const target = path.resolve(root, name)
    if (target !== root && !target.startsWith(root + path.sep)) {
      throw new Error(`Refusing to extract "${name}": it would be written outside ${root}`)
    }
    if (name.endsWith('/')) {
      fs.mkdirSync(target, { recursive: true })
      continue
    }
    fs.mkdirSync(path.dirname(target), { recursive: true })
    fs.writeFileSync(target, data)
  }
}

/** Zips every file under sourceDir (paths relative to it, `/`-separated), max compression. */
export function createZip(sourceDir: string, destPath: string): void {
  const files: Zippable = {}
  for (const rel of fs.readdirSync(sourceDir, { recursive: true, encoding: 'utf8' })) {
    const abs = path.join(sourceDir, rel)
    if (fs.statSync(abs).isFile()) {
      files[rel.split(path.sep).join('/')] = fs.readFileSync(abs)
    }
  }
  fs.writeFileSync(destPath, zipSync(files, { level: 9 }))
}
