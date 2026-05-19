# RogueRun — Ghoulag Update

## What this mod does
Alternative rogue-run game mode. Tracks run state (in-level vs lobby), provides a custom experience layer on top of the base game loop.

## Version string
Already updated to `0.1.0_WIN_2026-05-19_134018_e0da1ed24c` in `RogueRunMod.cs`. ✓

## Known breakage

### `BackendManager.EventBeginLevel` — GONE
**File:** `mods/RogueRun/Patch/RogueRunPatch.cs`

RogueRun patches `EventBeginLevel` to set `RogueRunState.IsInLevel = true`. This is how it knows when a dungeon level has been entered.

The method no longer exists in the new `BackendManager`. The BepInEx log confirms:
```
[Error :  RogueRun] RogueRun v0.1.1: wrong game version.
```
(Version check prevents patches from loading, so the EventBeginLevel warning comes from WMF at startup.)

**Fix:** Find the replacement hook for level entry. Coordinate with WMF — WMF is also looking for an `EventBeginLevel` replacement. Once WMF defines a new `GameModeProtocol.OnEventBeginLevel()` hook, RogueRun should use that rather than patching BackendManager directly.

Candidates to check in `game-src/RR/BackendManager.cs`:
- `PlaySessionBeginLevel` (async UniTask method, line 866)
- `PlaySessionBeginRun` (still exists, line 891) — used by JoinAnytime, may also be relevant

Read `game-src/RR/BackendManager.cs` for the full new session lifecycle and find the most appropriate hook for "entering a dungeon level."

## Steps
1. Read `mods/RogueRun/Patch/RogueRunPatch.cs` to understand all patches
2. Read `game-src/RR/BackendManager.cs` to find the replacement for `EventBeginLevel`
3. Update `RogueRunPatch.cs` to patch the new method
4. Run `pnpm run lint:cs:fix && pnpm run build`
5. Add changelog: `fchange fixed "BackendManager.EventBeginLevel compatibility for Ghoulag Update" --pkg RogueRun`
6. Commit: `fcommit fixed "BackendManager.EventBeginLevel compatibility for Ghoulag Update" --pkg RogueRun`
7. Open PR to `feat/ghoulag-update`
