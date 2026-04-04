import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { REPO_ROOT } from './lib/mod.mts'

function getStagedFiles(): string[] {
  const result = spawnSync('git', ['diff', '--cached', '--name-only', '--diff-filter=d'], {
    cwd: REPO_ROOT,
    encoding: 'utf8',
  })
  return result.stdout.trim().split('\n').filter(Boolean)
}

function run(cmd: string, args: string[]): void {
  const result = spawnSync(cmd, args, { cwd: REPO_ROOT, stdio: 'inherit', shell: true })
  if (result.status !== 0) process.exit(result.status ?? 1)
}

const staged = getStagedFiles()

const tsFiles = staged.filter((f) => /\.(mts|ts|json)$/.test(f))
if (tsFiles.length > 0) {
  run('pnpm', ['exec', 'prettier', '--check', ...tsFiles])
}

const csFiles = staged.filter((f) => f.endsWith('.cs'))
if (csFiles.length > 0) {
  const sln = path.join(REPO_ROOT, 'mods', 'raiders-of-blackveil-mods.sln')
  run('dotnet', ['format', sln, '--verify-no-changes', '--include', ...csFiles])
}
