# BeginnersWelcome — Ghoulag Update

## What this mod does
Handicap system for new players. Reduces damage taken and displays a HUD overlay. Hooks into:
- `BaseHUDPage.OnInit/OnUpdate/OnDeactivate` — for HUD overlay injection
- `Health.TakeBasicDamage` / `Health.TakeDOTDamage` — for damage reduction
- `Health._stats` field (via `HandicapManager`) — for accessing stats

## Potential impact
The game made several balance changes to health and damage systems. Verify the patched methods still exist and have compatible signatures.

The Ghoulag Update changed champion level cap and ability levels — this shouldn't affect BeginnersWelcome directly since it works on `Health`, not abilities.

## Steps
1. Read `mods/BeginnersWelcome/Patch/BeginnersWelcomePatch.cs` and `HandicapManager.cs`
2. Verify `BaseHUDPage.OnInit/OnUpdate/OnDeactivate` still exist — check `game-src/` for `BaseHUDPage.cs`
3. Verify `Health.TakeBasicDamage` and `Health.TakeDOTDamage` still exist and have the same parameter signatures
4. Verify `Health._stats` field still exists
5. Run `pnpm run lint:cs:fix && pnpm run build`
6. If no changes needed, note "verified compatible"
7. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg BeginnersWelcome`
8. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg BeginnersWelcome`
9. Open PR to `feat/ghoulag-update`
