# WildguardModFramework — Ghoulag Update

## What this mod does
Shared infrastructure used by ThePit, RogueRun, and other mods.  
Key roles:
- Injects the "Mods" button into the main menu (`MenuStartPagePatch`)
- Provides `GameModeProtocol`: client mod validation on join, run-start notification
- Provides server chat, RPC helpers, FusionRpcHelper

## Known breakage from game update

### 1. `BackendManager.JoinPlaySession` → `V2_JoinPlaySession`
**File:** `mods/WildguardModFramework/Network/NetworkPatch.cs`  
The method was renamed and its signature changed.

Old: `void JoinPlaySession(Guid JoinGameSessionId, string serverName, string sessionPassword)`  
New: `async UniTask<bool> V2_JoinPlaySession(JoinablePlaySession joinablePlaySession)` — different parameter type entirely.

The patch validated that joining clients have compatible mods. The new method has a completely different parameter model (a `JoinablePlaySession` object instead of individual params). You must:
- Find the new join hook. Check `game-src/RR/BackendManager.cs` for `V2_JoinPlaySession`.
- Read `GameModeProtocol` to understand what data the prefix needs (`JoinGameSessionId`, `serverName`) and find where to get that from the new `JoinablePlaySession` parameter.
- Update `NetworkPatch.JoinPlaySessionPrefix` accordingly.
- Also update the direct call in `GameModeProtocol.cs:100` (`BackendManager.Instance.JoinPlaySession(...)`) to use the new API — or remove it if client-side joining is now handled differently.

### 2. `BackendManager.EventBeginLevel` — GONE
**File:** `mods/WildguardModFramework/Network/NetworkPatch.cs`  
`EventBeginLevel` no longer exists in `BackendManager`. It was used to notify `GameModeProtocol.OnEventBeginLevel()`, which in turn signals ThePit and RogueRun that a run level has started.

You must find a replacement hook. Candidates to check in `game-src/`:
- `BackendManager.PlaySessionBeginLevel` (async) — called when a level begins
- `BackendManager.PlaySessionBeginRun` (async) — called when a run begins
- Any `LevelManager` or `AppManager` method that fires when a dungeon level is entered

Read `game-src/RR/BackendManager.cs` and `game-src/AppManager.cs` to find a reliable hook for level/run start. Patch that method instead.

`GameModeProtocol.OnEventBeginLevel()` must still be called at the right moment (when the first dungeon level of a run starts). Preserve that contract — ThePit and RogueRun depend on it.

## Other patches to verify
- `MenuStartPagePatch` patches `AppManager.Init`, `MenuStartPage.OnInit/OnActivate/OnNavigateInput` — verify these still exist in game-src.
- `HostStartPagePatch` patches `MenuStartHostPage.OnActivate` and `BackendManager.BeginPlaySession` — `BeginPlaySession` still exists (line 845 in new BackendManager), verify the patch still works.
- `FusionRpcHelper` accesses `NetworkRunner.Simulation` and `NetworkBehaviour.ObjectIndex` — verify both still exist in game-src Fusion types.
- `ServerChatHudPatch` patches `BaseHUDPage` methods and `GameHUDPage.IsAnyPageOpen` — verify.

## Steps
1. Read `mods/WildguardModFramework/Network/NetworkPatch.cs` and `GameModeProtocol.cs`
2. Read `game-src/RR/BackendManager.cs` for the full new API surface
3. Fix `JoinPlaySession` → `V2_JoinPlaySession` (update method name, adapt parameters)
4. Find and fix `EventBeginLevel` replacement
5. Verify the other patches compile and are targeting valid methods
6. Run `pnpm run lint:cs:fix && pnpm run build`
7. Add changelog entry: `fchange fixed "BackendManager API compatibility for Ghoulag Update" --pkg WildguardModFramework`
8. Commit with `fcommit fixed "BackendManager API compatibility for Ghoulag Update" --pkg WildguardModFramework`
9. Open PR to `feat/ghoulag-update`
