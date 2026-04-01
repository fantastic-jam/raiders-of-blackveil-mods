import { execSync } from 'node:child_process'
import path from 'node:path'
import { REPO_ROOT } from './lib/mod.mts'

const sln = path.join(REPO_ROOT, 'mods', 'raiders-of-blackveil-mods.sln')

console.log('Building solution...')
execSync(`dotnet build "${sln}" -c Release`, { cwd: REPO_ROOT, stdio: 'inherit' })
