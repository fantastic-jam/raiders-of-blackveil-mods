import { execSync } from 'node:child_process'
import path from 'node:path'
import { REPO_ROOT } from './lib/mod.mts'

const isDebug = process.argv.includes('--debug')
const config = isDebug ? 'Debug' : 'Release'
const sln = path.join(REPO_ROOT, 'mods', 'raiders-of-blackveil-mods.sln')

console.log(`Building solution (${config})...`)
execSync(`dotnet build "${sln}" -c ${config}`, { cwd: REPO_ROOT, stdio: 'inherit' })
