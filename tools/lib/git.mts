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

export function tagExists(tag: string): boolean {
  return git(['tag', '-l', tag]) === tag
}

export function remoteTagExists(tag: string): boolean {
  return git(['ls-remote', '--tags', 'origin', `refs/tags/${tag}`]) !== ''
}

export function latestModTag(modName: string): string | null {
  const out = git(['tag', '-l', `${modName}-v*`, '--sort=-version:refname'], { allowFailure: true })
  return out ? out.split('\n')[0].trim() : null
}

export function changelog(modName: string, prevTag: string | null): string {
  const range = prevTag ? `${prevTag}..HEAD` : 'HEAD'
  const lines = git([
    'log',
    range,
    '--pretty=format:%s',
    '--no-merges',
    `--grep=(${modName})`,
    `--grep=(All)`,
    '-i',
  ])
    .split('\n')
    .filter(Boolean)
    .filter((l) => !/^chore\([^)]+\):\s*release v/.test(l))
    .map((l) => {
      const m = l.match(/^(fix|chore|new)\([^)]+\):\s*(.+)$/)
      return m ? `*${m[1]}*: ${m[2]}` : `- ${l}`
    })

  return lines.length > 0 ? `## Changelog\n\n${lines.join('\n')}` : 'No changes recorded.'
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
