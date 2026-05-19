# ThePit — Ghoulag Update

## What this mod does
PvP arena game mode. Spawns a SlashBash room, manages match lifecycle (grace period, timer, respawn), and makes all champion abilities affect other players.  
**This is a PvP mod, not a bug fix mod.** Do not fix vanilla bugs here.

## Version string
Already updated to `0.1.0_WIN_2026-05-19_134018_e0da1ed24c` in `ThePitMod.cs`. ✓

## Known breakage

### 1. `BeatriceSpecialObject.FlowerEffect` → `FlowerEffectOnHost`
**File:** `mods/ThePit/FeralEngine/Abilities/BeatriceSpecialObjectPatch.cs`

The method was renamed in the new game build. Currently patching `"FlowerEffect"` — change to `"FlowerEffectOnHost"`. Also update the warning message string to match.

Verify: `grep "FlowerEffectOnHost" game-src/RR.Game.Character/BeatriceSpecialObject.cs`

### 2. `BackendManager.EventBeginLevel` — GONE
**File:** `mods/ThePit/Patch/ThePitPatch.cs`

ThePit patches `EventBeginLevel` directly (independent of WMF). If the method is gone, ThePit can't detect when to start a match. This is fatal for the mod.

**However:** ThePit already depends on WMF for `GameModeProtocol.OnEventBeginLevel()` notification. Coordinate with the WMF plan — once WMF finds a replacement hook, ThePit may be able to rely entirely on `GameModeProtocol.OnEventBeginLevel()` rather than patching `EventBeginLevel` itself.

Check `ThePitPatch.cs` to see how the direct `EventBeginLevel` patch is used vs the WMF protocol path. Consolidate if possible.

### 3. `ProjectileCaster._projectileHits` — GONE
**File:** `mods/ThePit/FeralEngine/Abilities/ProjectileCasterSelfSkipPatch.cs`

The field no longer exists in `ProjectileCaster`. The patch already handles null gracefully — it will log a warning and self-disable. No crash risk.

**Action:** Check `game-src/RR.Game.Character/ProjectileCaster.cs` to see if there is a replacement mechanism for duplicate hit suppression. If not, remove the patch entirely and note it in the changelog.

### 4. `ProjectileCaster._excludeCasterLayer` — GONE
**File:** `mods/ThePit/FeralEngine/Abilities/ProjectileCasterExpander.cs`

Same situation — field gone, expander self-disables. Without `_excludeCasterLayer`, expanded projectile casters (for PvP hit detection on players) may not function correctly.

**Action:** Read `ProjectileCaster.cs` in game-src to understand the new layer/hit detection mechanism. Find the replacement field or method to re-enable player targeting for projectile casters.

## Champion Talents — verify ability patches
The game added Champion Talents that "completely change how abilities function entirely" and raised ability levels (Standard → L10, Ultimate → L6). Verify that the internal methods ThePit patches are still called in the same code paths with the same signatures:

- `BlazeDevastation.Recastable` property — still exists in game-src ✓ (verified)
- `BlazeBlastWave.CollectAndGrabEnemies/PushAwayEnemies/DamageEnemies` — still exist ✓
- `BlazeSpecialArea.UpdateAuraEffect(bool)` — still exists ✓
- `RhinoShieldsUpAbility.HitEnemies(float)` — still exists ✓
- `RhinoStampedeAbility.DetectEnemiesToGrab` — still exists ✓
- `ShameleonShadowDanceAbility.LetsDance` — still exists ✓
- `ManEaterPlantBrain.Aim` and `HitEnemiesInArch` — still exist ✓
- `SunStrikeArea.DamageCheck` — still exists ✓
- `NetworkChampionMinion.SelectEnemyTarget/GeneralAttackConditionsOk/OnDead` — still exist ✓

Spot-check a few of these by grepping game-src to confirm the signatures haven't changed. Pay attention to any new Talent-related overloads.

## AreaCharacterSelectorPatch — re-check ally mode logic
`_chooseOwnerLast` is now gone from `AreaCharacterSelector`. The comment in `AreaCharacterSelectorPatch.cs` explains that vanilla used to remove the owner via `_chooseOwnerLast`. Since the game fixed ally-targeting bugs, the vanilla behavior in ally mode has changed.

Read `game-src/RR.Game.Perk/AreaCharacterSelector.cs` to understand the new ally mode behavior and verify that ThePit's postfix (clear candidates, re-add only owner) is still correct for PvP isolation.

## Steps
1. Fix `BeatriceSpecialObjectPatch.cs`: `"FlowerEffect"` → `"FlowerEffectOnHost"` (and warning message)
2. Fix `EventBeginLevel` — coordinate with WMF fix or patch the new replacement method directly
3. Investigate `ProjectileCasterExpander` — find replacement for `_excludeCasterLayer` or remove the expander and note it
4. Investigate `ProjectileCasterSelfSkipPatch` — find replacement or remove
5. Verify ability patch signatures against game-src (spot-check 3-4 of the confirmed-ok list above to be sure)
6. Re-check `AreaCharacterSelectorPatch` ally mode logic against new game-src
7. Run `pnpm run lint:cs:fix && pnpm run build`
8. Add changelog entry: `fchange fixed "Ghoulag Update compatibility" --pkg ThePit`
9. Commit with `fcommit fixed "Ghoulag Update compatibility" --pkg ThePit`
10. Open PR to `feat/ghoulag-update`
