# JoinAnytime — Ghoulag Update

## What this mod does
Allows players to join mid-run. Patches `BackendManager.PlaySessionBeginRun` to keep the session visible in the server browser during runs.

## Potential impact
`BackendManager.PlaySessionBeginRun` still exists in the new game-src (line 891). The Ghoulag Update notes no changes to session joining. Low risk.

## Steps
1. Read `mods/JoinAnytime/Patch/JoinAnytimePatch.cs` to review all patches
2. Verify `BackendManager.PlaySessionBeginRun` still exists with a compatible signature:  
   `grep "PlaySessionBeginRun" game-src/RR/BackendManager.cs`
3. Run `pnpm run lint:cs:fix && pnpm run build`
4. If no changes needed, note "verified compatible"
5. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg JoinAnytime`
6. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg JoinAnytime`
7. Open PR to `feat/ghoulag-update`
