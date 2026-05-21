# PerfectDodge — Ghoulag Update

## What this mod does
Adds a perfect dodge window — blocks damage for a configurable duration and percentage after a dodge. Patches `Health.TakeBasicDamage` and dash ability lifecycle.

## Potential impact
The Ghoulag Update reduced Bleed initial hit damage (25% → 15%) and Chill stacks (4 → 3). These are balance changes that don't affect the dodge patch mechanism.

Champion level cap raised to 30, ability levels raised — these don't affect dodge patching.

Low risk overall.

## Steps
1. Read `mods/PerfectDodge/Patch/PerfectDodgePatch.cs` to review what's patched
2. Verify `Health.TakeBasicDamage` signature hasn't changed (same check as BeginnersWelcome)
3. Verify dash ability hooks still work — check any `DashAbility` patches against game-src
4. Run `pnpm run lint:cs:fix && pnpm run build`
5. If no changes needed, note "verified compatible"
6. Add changelog: `fchange changed "verified compatibility with Ghoulag Update" --pkg PerfectDodge`
7. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg PerfectDodge`
8. Open PR to `feat/ghoulag-update`
