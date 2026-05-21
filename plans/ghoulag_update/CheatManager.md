# CheatManager — Ghoulag Update

## What this mod does
Developer cheat panel for in-game testing. Provides cheat commands (god mode, item spawning, etc.) accessed via a UI overlay.

## Potential impact
Low risk. CheatManager primarily injects UI and dispatches commands. It's unlikely the Ghoulag Update broke its core functionality.

## Steps
1. Read `mods/CheatManager/` to understand what game APIs it accesses
2. Check any `AccessTools` calls against game-src for renamed/removed methods
3. Run `pnpm run lint:cs:fix && pnpm run build`
4. If no changes needed, note "verified compatible"
5. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg CheatManager`
6. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg CheatManager`
7. Open PR to `feat/ghoulag-update`
