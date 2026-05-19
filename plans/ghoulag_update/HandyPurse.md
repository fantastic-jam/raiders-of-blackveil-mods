# HandyPurse — Ghoulag Update

## What this mod does
Adds a persistent bank. When joining a session without state authority (client), excess item stacks are drained to a local bank file. Hooks into `BackendManager.LoadPlayerGameState` to restore bank on load.

## Version string
Already updated to `0.1.0_WIN_2026-05-19_134018_e0da1ed24c` in `HandyPurseMod.cs`. ✓

## Potential impact: Shared Loot Inventory

The Ghoulag Update changed: **"All Champions now share the same Loot Inventory and Safe Pockets"** (equipped items remain individual).

HandyPurse tracks items in the player's stash/bank. With the inventory now shared across champions, verify:
- Does `BackendManager.LoadPlayerGameState` still exist and fire at the same point? (It was present in the previous game version.)
- Does the patch in `HandyPursePatch.cs` still correctly intercept it?
- Are the item/stash APIs HandyPurse uses still structured the same way?

Read `mods/HandyPurse/Patch/HandyPursePatch.cs` and `mods/HandyPurse/Bank/BankOrchestrator.cs` to understand what game APIs are accessed, then verify against game-src.

## Steps
1. Read `HandyPursePatch.cs` — check every `AccessTools` call for method/field names
2. Verify each against game-src (especially `LoadPlayerGameState` and any inventory-related fields)
3. Assess if "shared inventory" changes affect how HandyPurse identifies item stacks
4. Run `pnpm run lint:cs:fix && pnpm run build`
5. If no code changes needed, just verify build passes and note "verified compatible"
6. Add changelog: `fchange changed "verified compatibility with Ghoulag Update (shared inventory)" --pkg HandyPurse`
7. Commit: `fcommit changed "verified compatibility with Ghoulag Update" --pkg HandyPurse`
8. Open PR to `feat/ghoulag-update`
