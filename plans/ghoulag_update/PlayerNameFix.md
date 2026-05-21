# PlayerNameFix — Ghoulag Update

## What this mod does
Fixes player name display issues.

## Potential impact
Low risk. Player name display is a UI concern. The Ghoulag Update's UI changes focus on menus, health bars, and atlas — player names in-game are unlikely to have changed.

## Steps
1. Read `mods/PlayerNameFix/` to understand what game APIs are accessed
2. Check any `AccessTools` calls against game-src
3. Run `pnpm run lint:cs:fix && pnpm run build`
4. If no changes needed, note "verified compatible"
5. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg PlayerNameFix`
6. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg PlayerNameFix`
7. Open PR to `feat/ghoulag-update`
