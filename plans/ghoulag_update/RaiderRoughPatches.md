# RaiderRoughPatches — Ghoulag Update

## What this mod does
Bug fix mod. Corrects vanilla gameplay issues that the game hasn't fixed yet.  
Current fixes:
- **SessionVisibilityFix** — re-confirms Fusion region on lobby load, fixes session disappearing from server list
- **ItemStackFix (StashAutoStack)** — auto-stacks items when transferring to/from stash
- **BarrierSelfGrantFix** — re-applies "self and nearest ally" perk effects to the caster (fixes ally-only bug)
- **DoorVoteFix** — re-evaluates door vote when a player disconnects mid-vote
- **FusionStaleRefFix** — clears fake-null Fusion NetworkBehaviourId backing fields

## Game-fixed bugs — remove BarrierSelfGrantFix

The Ghoulag Update fixed all the "ally-only" perk bugs that `BarrierSelfGrantFix` was addressing:
- Fixed Holy Barrier only being able to bless one player at a time
- Fixed Grace only blessing allies
- Fixed Sanctuary only being able to grant Barrier to one player at a time
- Fixed Barrier only being able to grant Barrier to one player at a time
- Fixed Priest rank 2/8 bonus only applying to allies
- Fixed Mage rank 8 bonus only applying to allies
- Fixed Intellect Blessing / Divine Presence / Holy Presence only applying to allies

The `_chooseOwnerLast` field on `AreaCharacterSelector` is confirmed gone from game-src — the game rewrote that targeting logic. `BarrierSelfGrantFix.IsReady` will return false (because `_chooseOwnerLastField == null`), so it already self-disables. But the dead code should be removed cleanly.

### Files to change
- Delete `mods/RaiderRoughPatches/BarrierSelfGrantFix.cs`
- In `mods/RaiderRoughPatches/Patch/RaiderRoughPatchesPatch.cs`:
  - Remove `private static MethodInfo _doAreaSelection;`
  - Remove `private static ConfigEntry<bool> _fixBarrierSelfGrant;`
  - Remove the `_fixBarrierSelfGrant` config.Bind(...) block
  - Remove the `if (_fixBarrierSelfGrant.Value) { ... }` Init block (BarrierSelfGrantFix.Init + DoAreaSelection lookup)
  - Remove the `if (_fixBarrierSelfGrant?.Value == true && _doAreaSelection != null)` Patch block
  - Remove the `DoAreaSelectionPostfix` method
  - Remove `using RR.Game.Perk;` if it becomes unused

## Verify remaining fixes still work

Check each remaining fix against game-src to confirm the methods they patch still exist:

### SessionVisibilityFix
Patches `LobbyManager.OnSceneLoadDone`. Check: `grep "OnSceneLoadDone" game-src/RR.Level/LobbyManager.cs`

### ItemStackFix
Patches `GameInventoryPage.TransferItem` and `InventorySlotNormal.CanMergeItem`.  
Note: "All Champions now share the same Loot Inventory" in the Ghoulag Update — verify the stash/inventory UI classes haven't been restructured.  
Check: `grep "TransferItem" game-src` and `grep "CanMergeItem" game-src`

### DoorVoteFix
Patches `DoorManager.Activate`, `DoorManager.RPC_VoteState`, `PlayerManager.OnPlayerLeft`. Verify these still exist.

### FusionStaleRefFix
Patches `SunStrikeArea.FixedUpdateNetwork` and `NetworkCharacterBase.FixedUpdateNetwork`. `SunStrikeArea` still exists in game-src ✓.

## Steps
1. Delete `BarrierSelfGrantFix.cs`
2. Clean up all references in `RaiderRoughPatchesPatch.cs` (see above)
3. Run `pnpm run lint:cs:fix && pnpm run build` — should compile clean
4. Spot-check remaining 4 fix methods exist in game-src
5. Add changelog: `fchange removed "BarrierSelfGrantFix — game fixed the underlying ally-targeting bugs in Ghoulag Update" --pkg RaiderRoughPatches`
6. Commit: `fcommit removed "BarrierSelfGrantFix — game fixed ally-targeting bugs" --pkg RaiderRoughPatches`
7. Open PR to `feat/ghoulag-update`
