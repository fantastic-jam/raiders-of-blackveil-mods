import archiver from 'archiver'
import fs from 'node:fs'

export function createZip(sourcDir: string, destPath: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const output = fs.createWriteStream(destPath)
    const archive = archiver('zip', { zlib: { level: 9 } })

    output.on('close', resolve)
    archive.on('error', reject)

    archive.pipe(output)
    archive.glob('**/*', { cwd: sourcDir })
    void archive.finalize()
  })
}
