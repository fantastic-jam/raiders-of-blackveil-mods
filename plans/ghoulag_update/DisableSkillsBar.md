# DisableSkillsBar — Ghoulag Update

## What this mod does
Hides the champion skills bar from the HUD. Pure UI patch.

## Potential impact
The Ghoulag Update made significant UI changes (new atlas UI, new merchant UI, new health bars, etc.). Ability upgrades now require holding Alt. The skills bar UI may have changed.

## Steps
1. Read `mods/DisableSkillsBar/Patch/` to see what UI elements are patched
2. Check if the patched class/method names still exist in game-src (look for `SkillsBar`, `AbilityBar`, or related HUD classes)
3. Run `pnpm run lint:cs:fix && pnpm run build`
4. If no changes needed, note "verified compatible"
5. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg DisableSkillsBar`
6. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg DisableSkillsBar`
7. Open PR to `feat/ghoulag-update`
