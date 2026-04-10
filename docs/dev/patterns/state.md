# State management and mod registration

## Separate state class for non-trivial mods

When a mod needs shared state between the plugin class and the patch class, put it in a dedicated static class:

```
[ModName]State.cs   — data only, no patch logic
```

Conventions:
- `IsActive` — controlled by `Enable()`/`Disable()` from WMF. `public` getter, `internal` setter.
- `InRun` (or equivalent) — controlled by patches. `internal` on both getter and setter.
- Snapshot data — `internal static readonly`, cleared via a named method e.g. `ClearSnapshot()`.

## `IModRegistrant` implementation

```csharp
// GameMode mod (RogueRun) — uses IsActive, inverted for Disabled
public bool Disabled => !RogueRunState.IsActive;
public void Enable()  { RogueRunState.IsActive = true;  PublicLogger.LogInfo($"{Name}: enabled."); }
public void Disable() { RogueRunState.IsActive = false; PublicLogger.LogInfo($"{Name}: disabled."); }

// Regular mod (PerfectDodge) — delegates Disabled to the patch class
public bool Disabled => PerfectDodgePatch.Disabled;
public void Enable()  { PublicLogger.LogInfo($"{Name}: enabled.");  PerfectDodgePatch.SetEnabled(); }
public void Disable() { PublicLogger.LogInfo($"{Name}: disabled."); PerfectDodgePatch.SetDisabled(); }
```

`GetModType()` returns `nameof(ModType.Mod)`, `nameof(ModType.Cheat)`, or `nameof(ModType.GameMode)` — never a string literal.

`Enable()`/`Disable()` must not re-apply or unapply Harmony patches. Patch methods check the flag at the top of each call instead (the noop guard pattern).

## State must be cleared on every exit path

`InRun = false` and `ClearSnapshot()` belong in the level-end handler, but abnormal exits (disconnect, crash) may not fire that event. When adding network event patches in the future, reset state in any handler that fires on unclean level exit.

**Known limitation in RogueRun:** no cleanup hook for disconnect or crash. If `InRun` stays true after a failed session, the next session will suppress saves incorrectly.

## `ClearSnapshot()` after restore — not before

Snapshot cleanup at the end of `EventLevelEndPostfix` happens after the restore loop. Moving it before the loop (or in a `finally`) loses the data being restored.

## What belongs in `libs/` vs. the mod

Extract to `libs/` only when two or more mods share the logic and the interface is stable. `libs/ModRegistry/` exists because WMF and every registrant need `IModRegistrant` and `ModType`. Do not extract a helper to `libs/` for one mod's convenience — keep it in the mod. State classes stay in the mod namespace always.
