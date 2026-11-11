import fs from 'node:fs'
import path from 'node:path'
import { REPO_ROOT } from './mod.mts'

// Replaces `dotenv/config`: loads the repo's .env into process.env if present.
// Variables already set in the environment win, as with dotenv.
export function loadDotEnv(): void {
  const envPath = path.join(REPO_ROOT, '.env')
  if (fs.existsSync(envPath)) {
    process.loadEnvFile(envPath)
  }
}
