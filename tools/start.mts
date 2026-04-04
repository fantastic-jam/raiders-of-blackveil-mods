import { execSync } from 'node:child_process'

const STEAM_APP_ID = '3352240'

console.log(`Launching Raiders of Blackveil via Steam (AppID ${STEAM_APP_ID})...`)
execSync(`start steam://rungameid/${STEAM_APP_ID}`, { shell: 'cmd.exe' })
