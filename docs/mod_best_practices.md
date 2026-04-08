# Mod Best Practices

Rules derived from code review of RogueRun, PerfectDodge, and WildguardModFramework. Every rule here has a live example in the codebase — no generic BepInEx advice.

---

## 1. Harmony patching

### Resolve reflection handles once, in `Apply()` — never inline

Every `AccessTools.Field` and `AccessTools.Method` call must happen in `Apply()`, assigned to a `private static` field on the patch class. Patch methods then use the stored handle. This means reflection costs are paid once at startup and any resolution failure is logged immediately, not silently swallowed at runtime.

```csharp
// Good — RogueRunPatch.cs
private static FieldInfo _syncedItemsField;

public static void Apply(Harmony harmony) {
    _syncedItemsField = AccessTools.Field(typeof(Inventory), "_syncedItems");
    if (_syncedItemsField == null)
        RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find Inventory._syncedItems — strip/restore inactive.");
    // ...
}

private static void StripInventory(Inventory inv) {
    var syncedItems = (InventorySyncedItems)_syncedItemsField.GetValue(inv);
    // ...
}
```

```csharp
// Bad — resolving inline inside a patch method
static void SomePostfix(Inventory inv) {
    var field = AccessTools.Field(typeof(Inventory), "_syncedItems"); // called every time the method fires
    field.GetValue(inv);
}
```

### Warn and bail per handle — do not use a single gate

Each reflection handle gets its own null-check and its own `LogWarning`. Do not combine them into a single `if (a == null || b == null) return;` guard before patching, because that silently skips all patches if any one handle is missing. Check each independently so partial functionality survives a game update that renames one field.

```csharp
// Good — each handle independently guarded
_syncedItemsField = AccessTools.Field(typeof(Inventory), "_syncedItems");
if (_syncedItemsField == null)
    RogueRunMod.PublicLogger.LogWarning("RogueRun: ... — strip/restore inactive.");

_receivedBackendDataFlagsField = AccessTools.Field(typeof(Inventory), "_receivedBackendDataFlags");
if (_receivedBackendDataFlagsField == null)
    RogueRunMod.PublicLogger.LogWarning("RogueRun: ... — restore inactive.");

var beginLevelMethod = AccessTools.Method(typeof(BackendManager), "EventBeginLevel");
if (beginLevelMethod == null) {
    RogueRunMod.PublicLogger.LogWarning("RogueRun: ... — InRun tracking inactive.");
} else {
    harmony.Patch(beginLevelMethod, postfix: ...);
}
```

The warning message must name the missing member and state which feature goes inactive. This is the minimum information needed to diagnose a game update.

### Patch method visibility

Patch methods are `static` and package-private (no access modifier). Harmony does not require `public`, and keeping them package-private avoids polluting the public surface of the assembly. Helper methods called only from patch methods are `private static`.

```csharp
// Good
static void EventBeginLevelPostfix() { ... }
private static void StripInventory(Inventory inv) { ... }

// Avoid
public static void EventBeginLevelPostfix() { ... } // unnecessarily public
```

The exception is `Apply()`, which is `public static` because `[ModName]Mod.Awake()` calls it from outside the namespace.

### Reference patch methods by `nameof` in `HarmonyMethod`, not by string literal

```csharp
// Good
harmony.Patch(method, postfix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(EventBeginLevelPostfix))));

// Avoid
harmony.Patch(method, postfix: new HarmonyMethod(typeof(RogueRunPatch), "EventBeginLevelPostfix"));
```

`nameof` catches renames at compile time; a string literal silently breaks.

---

## 2. Critical vs optional patches — fail-safe on breaking changes

### `Apply()` returns `bool`

`Apply()` must return `bool`. Return `false` if any patch that is essential for safety cannot be applied. Return `true` only after all critical patches succeed.

```csharp
// Good
public static bool Apply(Harmony harmony) {
    var getter = AccessTools.PropertyGetter(typeof(GenericItemDescriptor), nameof(GenericItemDescriptor.AmountMaximum));
    if (getter == null) {
        HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find GenericItemDescriptor.AmountMaximum — stack protection unavailable.");
        return false;
    }
    harmony.Patch(getter, postfix: ...);

    // Optional patch — vanilla fallback is safe, so warn and continue.
    _itemsArrayField = AccessTools.Field(typeof(InventorySyncedItems), "_itemsArray");
    if (_itemsArrayField == null)
        HandyPurseMod.PublicLogger.LogWarning("HandyPurse: ... — auto-pickup merge uses vanilla caps.");

    return true;
}
```

**A patch is critical if the mod could cause user harm when it is absent.** HandyPurse without `AmountMaximum` patched would let the game clamp stacks to vanilla limits on the next save. RogueRun without save suppression would persist in-run loot to the backend. Those are critical. A missing drop-rate boost or a smuggler that still opens are not critical.

### `Awake()` unpatch + disable + fatal log on failure

```csharp
private void Awake() {
    PublicLogger = Logger;
    try {
        _harmony = new Harmony(Id);
        if (!FooPatch.Apply(_harmony)) {
            _harmony.UnpatchSelf();
            FooPatch.SetDisabled();   // or equivalent state flag
            LogBreakingChange();
            return;
        }
        PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
    }
    catch (Exception ex) {
        PublicLogger.LogError(ex);
    }
}

private void LogBreakingChange() {
    PublicLogger.LogFatal("============================================================");
    PublicLogger.LogFatal($"{Name} v{Version}: game assembly breaking change detected.");
    PublicLogger.LogFatal($"Mod DISABLED — [explain what is NOT protected].");
    PublicLogger.LogFatal($"Update the mod or report a bug (include your BepInEx log).");
    PublicLogger.LogFatal("============================================================");
}
```

`_harmony.UnpatchSelf()` removes all patches applied by this instance, including any that succeeded before the failure. Always call it before returning — a partially patched mod is worse than an unpatched one.

Use `LogFatal` only when a broken patch can corrupt the player's saves — currently only HandyPurse qualifies (stacks above vanilla caps will be silently clamped on next save if the mod disables mid-session). Every other mod uses `LogError`.

The `LogBreakingChange` body: keep it short. Only mention saves if they are at risk. Do not reassure the player about saves unless you are certain — say nothing and let them check.

---

## 3. The plugin entry point (`[ModName]Mod.cs`)

### Constants layout

```csharp
public const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.[modname]";
public const string Name = "[ModName]";
public const string Version = "0.1.0";  // public — release tooling reads this
public const string Author = "christphe";
```

`Id` is `private const` only if the mod never needs it externally. `Version` must be `public const string` — the release tooling (`tools/release.mts`) reads it from the compiled assembly via reflection.

### `Awake()` structure

```csharp
private void Awake() {
    PublicLogger = Logger;  // assign before any call that might log

    try {
        [ModName]Patch.Apply(new Harmony(Id));
        PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
    }
    catch (Exception ex) {
        PublicLogger.LogError(ex);
    }
}
```

The `try/catch` ensures a patch failure does not crash BepInEx loading of other plugins. Do not re-throw. Do not add logic before `PublicLogger = Logger;`.

### `PublicLogger` is always `public static ManualLogSource`

Patch classes need to log but cannot access the BepInEx `Logger` property (that is instance-only on `BaseUnityPlugin`). The pattern is a `public static ManualLogSource PublicLogger` assigned in `Awake()`. RogueRun and PerfectDodge both follow this. Do not pass the logger as a parameter to `Apply()` — the static field is simpler and consistent with every other mod in the repo.

---

## 4. `IModRegistrant` implementation

When a mod should appear in WMF (to be toggled per-session), the plugin class implements `IModRegistrant`. Rules:

- `GetModType()` returns `nameof(ModType.Mod)`, `nameof(ModType.Cheat)`, or `nameof(ModType.GameMode)` — never a string literal, so renaming the enum is caught at compile time.
- `Disabled` reads from a state flag. For mods with dedicated state classes, delegate to that class directly: `public bool Disabled => !RogueRunState.IsActive;`
- `Enable()` and `Disable()` log and then flip the flag. They must not re-apply or unapply Harmony patches — the patch methods check the flag at the top of each call instead (the noop guard pattern).
- For `GameMode` mods, `IsActive` is the correct flag name because the game mode is active when enabled, not disabled. For `Mod` and `Cheat` types, `Disabled` is the conventional flag name on the patch class so that `bool Disabled => PerfectDodgePatch.Disabled;` reads naturally.

```csharp
// GameMode mod (RogueRun) — uses IsActive, inverted for Disabled
public bool Disabled => !RogueRunState.IsActive;
public void Enable() { RogueRunState.IsActive = true; PublicLogger.LogInfo($"{Name}: enabled."); }
public void Disable() { RogueRunState.IsActive = false; PublicLogger.LogInfo($"{Name}: disabled."); }
```

```csharp
// Regular mod (PerfectDodge) — delegates Disabled to the patch class
public bool Disabled => PerfectDodgePatch.Disabled;
public void Enable() { PublicLogger.LogInfo($"{Name}: enabled."); PerfectDodgePatch.SetEnabled(); }
public void Disable() { PublicLogger.LogInfo($"{Name}: disabled."); PerfectDodgePatch.SetDisabled(); }
```

---

## 5. Networking and `IsServer` guards

### Guard at the level of the collection, not per-player

Host-only operations (inventory mutations, state writes) belong in a single `IsServer` check before the player loop — not repeated inside each iteration.

```csharp
// Good — one check, then loop
var players = PlayerManager.Instance?.GetPlayers();
if (players == null || players.Count == 0) return;
if (!players[0].Inventory.Object.Runner.IsServer) return;

foreach (var player in players) {
    StripInventory(player.Inventory);
}
```

```csharp
// Bad — guard repeated per player (wasteful, and inconsistent if IsServer changes mid-frame)
foreach (var player in players) {
    if (!player.Inventory.Object.Runner.IsServer) continue; // wrong pattern
    StripInventory(player.Inventory);
}
```

`players[0].Inventory.Object.Runner` is the Fusion NetworkRunner. It is the same runner for all players in the session, so checking it once is correct and sufficient.

### `PlayerManager.Instance` null-check

`PlayerManager.Instance` can be null during startup, shutdown, or if the level setup has not completed. Always null-propagate: `PlayerManager.Instance?.GetPlayers()`. If `players` is null, return early — do not proceed to `players[0]`.

---

## 6. State management

### Separate state class for non-trivial mods

When a mod needs shared state between the plugin class and the patch class, put it in a dedicated static class in the mod's root namespace. See `RogueRunState`:

- `IsActive` — controlled by `Enable()/Disable() calls from WMF. `public` getter, `internal` setter.
- `InRun` — controlled by patches that track level entry/exit. `internal` on both getter and setter.
- Snapshot data — `internal static readonly`, cleared explicitly via a named method.

The state class is not the right place for patch logic. It holds data only.

### `InRun` must be cleared on every exit path

`InRun = false` happens in `EventLevelEndPostfix`. This is correct for the normal case, but abnormal exits (disconnect, crash, server shutdown) may not fire `EventLevelEnd`. If `InRun` stays `true` after a failed session, the next session will suppress saves incorrectly.

**Known limitation in RogueRun:** there is no cleanup hook for disconnect or crash. Acceptable for now, but when adding networking event patches in future, reset `InRun` and `ClearSnapshot()` in any handler that fires on unclean level exit.

### `ClearSnapshot()` after restore — not before

Snapshot cleanup at the end of `EventLevelEndPostfix` happens after the restore loop. This is correct. If you move it before the loop (or call it in a finally block that runs before restoration completes), you lose the data you are trying to restore.

---

## 7. Coroutine prefix pattern

### Replacing a coroutine return value requires setting `__result` and returning `false`

The game's `SavePlayerGameStates` is `IEnumerator<WaitForSeconds>`. To suppress it cleanly without hanging the game's flow control, the prefix must:

1. Invoke the callback parameter immediately (the game expects it to be called eventually).
2. Assign a non-null, valid (but empty) enumerator to `__result`.
3. Return `false` to skip the original.

```csharp
static bool SavePlayerGameStatesPrefix(ref IEnumerator<WaitForSeconds> __result, Action backendRequestCompleted) {
    if (!RogueRunState.IsActive || !RogueRunState.InRun) return true;
    RogueRunMod.PublicLogger.LogInfo("RogueRun: save suppressed.");
    backendRequestCompleted?.Invoke();
    __result = EmptyCoroutine();
    return false;
}

private static IEnumerator<WaitForSeconds> EmptyCoroutine() { yield break; }
```

Do not return `false` without setting `__result` when the method returns a reference type — Harmony will use whatever `__result` happens to be, which may be null and may crash the caller.

The signature of the prefix must match the parameters you need. You do not need to declare all original parameters — only those you use. `ref IEnumerator<WaitForSeconds> __result` is required when you replace the return value.

### Check the game source before patching a coroutine

Read the `game-src/` decompilation to understand how the original handles its callback. `SavePlayerGameStates` calls `backendRequestCompleted()` in the success branch, the retry callback, and the error branch on client-initiated saves. The suppression prefix must call it unconditionally (via `?.Invoke()`) because the game's caller may block until it fires.

---

## 8. Re-entrant patch guard with `[ThreadStatic]`

When a patch method calls into the same method it patches, use a `[ThreadStatic]` flag to break the recursion. Do not use a plain `static bool` — that is not thread-safe if Fusion ever calls from multiple threads.

```csharp
// Good — RogueRunPatch.cs
[ThreadStatic] private static bool _rerolling;

static void TestDropChancePostfix(EnemyDropRuntime __instance, ref bool __result, EnemyInfo enemyInfo, DifficultyInfo diffInfo) {
    if (!RogueRunState.IsActive || __result || _rerolling) return;
    _rerolling = true;
    try {
        __result = __instance.TestDropChance(in enemyInfo, in diffInfo);
    } finally {
        _rerolling = false;
    }
}
```

The `try/finally` guarantees the flag is cleared even if `TestDropChance` throws.

---

## 9. Error paths — what to do, what not to do

### Null returns from `GetPlayers()` or `Instance`

Framework singletons (`PlayerManager.Instance`, `GameManager.Instance`) can return null at any point outside an active game session. Always null-propagate or early-return. Do not log a warning — this is expected during startup and shutdown. Only log if null at a time when you expect it to exist (e.g., inside an `EventBeginLevel` postfix where a null `PlayerManager.Instance` would be genuinely unexpected).

### Missing snapshot at restore time

`EventLevelEndPostfix` skips players whose slot index is not in the snapshot (`TryGetValue` returns false). This is correct — do not attempt a restore for a player who was not present at entry. Do not fall back to a default snapshot. Log the skip if you need to diagnose it.

### Null `Inventory` per player

The loop in both entry and exit patches does `if (inv == null) continue;`. This guards against a player object that exists in the list but whose inventory component has not initialized. Keep this guard — it is not defensive overengineering, it is protection against a real race condition during level setup.

---

## 10. Naming conventions

- Patch class: `[ModName]Patch`, in `mods/[ModName]/Patch/[ModName]Patch.cs`
- Plugin class: `[ModName]Mod`, in `mods/[ModName]/[ModName]Mod.cs`
- State class (if needed): `[ModName]State`, in `mods/[ModName]/[ModName]State.cs`
- Private reflection handles: `_camelCaseField` / `_camelCaseMethod`
- Postfix methods: `[OriginalMethodName]Postfix`
- Prefix methods: `[OriginalMethodName]Prefix`
- Log prefix in messages: `"[ModName]: message"` — always include the mod name so logs are filterable

---

## 11. Patch extraction — patches are wiring only

### Rule: patch methods must be one-liners (except Disabled/Active guards)

Every Harmony patch method body must be a single delegating call to a named collaborator class. Any logic that is more than a guard check belongs in a Controller, Service, or Lifecycle class — never in the patch method itself.

```csharp
// Good — NetworkPatch.cs
static void OnPlayerJoinedPostfix(NetworkRunner runner, PlayerRef playerRef) =>
    GameModeProtocol.OnPlayerJoined(runner, playerRef);

// Bad — logic lives in the patch
static void OnPlayerJoinedPostfix(PlayerManager __instance, NetworkRunner runner, PlayerRef playerRef) {
    if (!runner.IsServer) { return; }
    // ... 20 more lines ...
}
```

The exception is `Apply()` (wires hooks and logs warnings) and the Disabled/Active guard at the top of patch methods for mods that support enable/disable.

### Naming conventions for extracted classes

| Type | Purpose | Example |
|---|---|---|
| `*Patch` | Harmony glue only — `Apply()` + one-liner hooks | `NetworkPatch`, `HostStartPagePatch` |
| `*Controller` | Owns UI page state for the lifetime of a page instance | `HostPageController` |
| `*Orchestrator` | Stateless or cross-cutting session/business logic | `SessionOrchestrator` |
| `*Injector` | One-time UI injection, owns overlay/page state | `ModsButtonInjector`, `SoloModePickerInjector` |
| `*Lifecycle` | Plugin-level lifecycle operations (startup, shutdown) | `ModLifecycle` |
| `*Protocol` | Network protocol state and dispatch | `GameModeProtocol` |
| `CoroutineRunner` | Empty `MonoBehaviour` for coroutine hosting only | `CoroutineRunner` |

Avoid the suffix "Manager" — the game already uses `NetworkManager`, `PlayerManager`, `UIManager`, etc. Clashing names cause namespace confusion and noisy greps.

### Controller vs. static class — choose by lifetime

Use an **instance class** (`*Controller`) when the class holds references to `VisualElement` or `MonoBehaviour` objects that are tied to a specific page instance lifetime. Static fields holding `VisualElement` references go stale when the page is destroyed or recreated, causing silent bugs.

Use a **static class** when the class holds no UI references, or when the data is genuinely global (e.g., `ConfirmedPlayers` in `GameModeProtocol`).

```csharp
// Controller pattern — HostPageController
internal sealed class HostPageController {
    internal static HostPageController Current { get; private set; }

    internal static void Activate(MenuStartHostPage page) {
        if (Current != null) { Current.Reset(); return; }
        Current = new HostPageController(page);  // fresh instance per first inject
    }
}
```

### Coroutine hosting — never use Harmony `__instance`

Do not call `__instance.StartCoroutine(...)` in a patch or extracted class. The `__instance` reference is a game object you do not control; it can be destroyed mid-wait and will silently stop the coroutine.

Instead, use a dedicated `CoroutineRunner` MonoBehaviour created in `Awake()` with `DontDestroyOnLoad`:

```csharp
// WmfMod.Awake()
var go = new GameObject("WMF.CoroutineRunner");
DontDestroyOnLoad(go);
Runner = go.AddComponent<CoroutineRunner>();

// In any class
WmfMod.Runner.StartCoroutine(MyCoroutine());
```

### Reflection handles in extracted classes

Extracted classes that access private game members must resolve `AccessTools.Field` / `AccessTools.Method` handles once, as `private static readonly` fields at class scope — never inline inside a method body.

```csharp
// Good — resolved once at class init
private static readonly FieldInfo CursorField = AccessTools.Field(typeof(MenuStartHostPage), "_cursor");

// Bad — resolved every call
private void Inject(MenuStartHostPage page) {
    var cursor = AccessTools.Field(typeof(MenuStartHostPage), "_cursor").GetValue(page);
}
```

### `ref` parameters stay in the patch method

`ref` parameters from Harmony cannot be passed into async methods or captured by lambdas. Keep the actual `ref` read/write in the patch method; let the extracted class compute and return the value to assign.

```csharp
// Good — ref stays in the patch; SessionOrchestrator takes ref directly (static method call is fine)
static void BeginPlaySessionPrefix(ref string sessionTag, BackendManager.PlaySessionMode mode) =>
    SessionOrchestrator.Begin(ref sessionTag,
        HostPageController.Current?.AllowMods   ?? true,
        HostPageController.Current?.AllowCheats ?? true,
        mode);
```

---

## 12. What belongs in `libs/` vs. in the mod

Extract to `libs/` only when two or more mods share the logic and the interface is stable. The current `libs/ModRegistry/` exists because both WMF and every registrant mod need `IModRegistrant` and `ModType`. Do not extract a helper to `libs/` for a single mod's convenience — keep it in the mod.

State classes (`RogueRunState`) stay in the mod namespace. They are not candidates for `libs/` even if they look generic.
