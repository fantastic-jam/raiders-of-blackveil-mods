# EmoteMod — Ghoulag Update

## What this mod does
Adds emote functionality to the game. Likely patches player input or animation systems.

## Potential impact
Low risk. Emote systems are rarely affected by gameplay balance patches.

## Steps
1. Read `mods/EmoteMod/` to understand what game APIs are accessed
2. Check any `AccessTools` calls against game-src
3. Run `pnpm run lint:cs:fix && pnpm run build`
4. If no changes needed, note "verified compatible"
5. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg EmoteMod`
6. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg EmoteMod`
7. Open PR to `feat/ghoulag-update`
