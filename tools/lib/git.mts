import { execSync, spawnSync } from 'node:child_process'
import { REPO_ROOT } from './mod.mts'

function git(args: string[], opts: { allowFailure?: boolean } = {}): string {
  const result = spawnSync('git', args, { cwd: REPO_ROOT, encoding: 'utf8' })
  if (!opts.allowFailure && result.status !== 0) {
    throw new Error(`git ${args[0]} failed:\n${result.stderr}`)
  }
  return result.stdout.trim()
}

export function currentBranch(): string {
  return git(['rev-parse', '--abbrev-ref', 'HEAD'])
}

export function isWorkingTreeClean(): boolean {
  return git(['status', '--porcelain']) === ''
}

export function dirtyFiles(): string[] {
  const out = git(['status', '--porcelain'])
  if (!out) return []
  return out
    .split('\n')
    .filter(Boolean)
    .map((line) => line.slice(3).trim().replace(/\\/g, '/'))
}

export function tagExists(tag: string): boolean {
  return git(['tag', '-l', tag]) === tag
}

export function remoteTagExists(tag: string): boolean {
  return git(['ls-remote', '--tags', 'origin', `refs/tags/${tag}`]) !== ''
}

export function stageFile(file: string): void {
  git(['add', file])
}

export function commit(message: string): void {
  const result = spawnSync('git', ['diff', '--cached', '--quiet'], { cwd: REPO_ROOT })
  if (result.status !== 0) {
    execSync(`git commit -m ${JSON.stringify(message)}`, { cwd: REPO_ROOT, stdio: 'inherit' })
  }
}

export function createTag(tag: string): void {
  execSync(`git tag -a ${tag} -m ${tag}`, { cwd: REPO_ROOT, stdio: 'inherit' })
}

export function push(branch: string, tag: string): void {
  execSync(`git push origin ${branch}`, { cwd: REPO_ROOT, stdio: 'inherit' })
  execSync(`git push origin ${tag}`, { cwd: REPO_ROOT, stdio: 'inherit' })
}
