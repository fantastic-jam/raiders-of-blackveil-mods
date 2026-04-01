import { Octokit } from '@octokit/rest'
import { execSync } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import { REPO_ROOT } from './mod.mts'

function getOctokit(): Octokit {
  const token = process.env['GITHUB_TOKEN']
  if (!token) throw new Error('GITHUB_TOKEN environment variable is not set.')
  return new Octokit({ auth: token })
}

function parseRepo(): { owner: string; repo: string } {
  const env = process.env['GITHUB_REPOSITORY']
  if (env) {
    const [owner, repo] = env.split('/')
    return { owner, repo }
  }
  const remote = execSync('git remote get-url origin', {
    cwd: REPO_ROOT,
    encoding: 'utf8',
  }).trim()
  const m = remote.match(/github\.com[:/]([^/]+)\/([^/.]+)/)
  if (!m) throw new Error(`Could not parse GitHub owner/repo from remote: ${remote}`)
  return { owner: m[1], repo: m[2] }
}

export async function createRelease(
  tag: string,
  title: string,
  notes: string,
  assetPath: string,
): Promise<void> {
  const octokit = getOctokit()
  const { owner, repo } = parseRepo()

  const release = await octokit.repos.createRelease({
    owner,
    repo,
    tag_name: tag,
    name: title,
    body: notes,
  })

  const assetData = fs.readFileSync(assetPath)
  const assetName = path.basename(assetPath)

  await octokit.repos.uploadReleaseAsset({
    owner,
    repo,
    release_id: release.data.id,
    name: assetName,
    data: assetData as unknown as string,
  })
}
