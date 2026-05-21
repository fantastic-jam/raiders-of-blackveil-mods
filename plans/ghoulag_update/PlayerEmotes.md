# PlayerEmotes — Ghoulag Update

## What this mod does
Adds player emote animations or UI. (Read the mod source to confirm exact functionality.)

## Potential impact
Low risk. Emote/animation systems are rarely changed in gameplay patches.

## Steps
1. Read `mods/PlayerEmotes/` to understand what game APIs are accessed
2. Check any `AccessTools` calls against game-src
3. Run `pnpm run lint:cs:fix && pnpm run build`
4. If no changes needed, note "verified compatible"
5. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg PlayerEmotes`
6. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg PlayerEmotes`
7. Open PR to `feat/ghoulag-update`
