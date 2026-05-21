# ghoulag-urskov — Ghoulag Update

## What this mod does
Custom character mod — adds the Ghoulag Urskov character to the game. Uses Unity addressable asset bundles for the character model/animations.

## Potential impact
The Ghoulag Update introduced the official Ghoulag biome and new characters. Verify that:
- The character registration API hasn't changed
- The addressable bundle format is still compatible
- Any patches to character spawning or selection still compile

## Steps
1. Read `mods/ghoulag-urskov/` to understand what game APIs are accessed
2. Check any `AccessTools` calls against game-src
3. Run `pnpm run lint:cs:fix && pnpm run build`
4. If no changes needed, note "verified compatible"
5. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg ghoulag-urskov`
6. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg ghoulag-urskov`
7. Open PR to `feat/ghoulag-update`
