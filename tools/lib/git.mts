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
  const raw = git([
    'log',
    range,
    '--pretty=format:%x00%B',
    '--no-merges',
    `--grep=(${modName})`,
    `--grep=(All)`,
    '-i',
  ])

  const entries = raw
    .split('\x00')
    .map((block) => block.trim())
    .filter(Boolean)
    .filter((block) => !/^chore\([^)]+\):\s*release v/.test(block))
    .filter((block) => !/^tidy\([^)]+\):/.test(block))
    .map((block) => {
      const [subject, ...rest] = block.split('\n')
      const body = rest.map((l) => l.trim()).filter(Boolean)
      const m = subject.trim().match(/^(fix|chore|new)\([^)]+\):\s*(.+)$/)
      const header = m ? `*${m[1]}*: ${m[2]}` : `- ${subject.trim()}`
      return body.length > 0 ? `${header}\n${body.map((l) => `  ${l}`).join('\n')}` : header
    })

  return entries.length > 0 ? `## Changelog\n\n${entries.join('\n\n')}` : 'No changes recorded.'
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
