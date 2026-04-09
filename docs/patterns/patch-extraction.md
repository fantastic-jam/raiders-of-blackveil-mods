# Patch extraction — patches are wiring only

## Rule: patch methods must be one-liners

Every Harmony patch method body is a single delegating call. Any logic beyond a guard check belongs in a Controller, Sidecar, Orchestrator, or Protocol class.

```csharp
// Good
static void OnPlayerJoinedPostfix(NetworkRunner runner, PlayerRef playerRef) =>
    GameModeProtocol.OnPlayerJoined(runner, playerRef);

// Bad — logic lives in the patch
static void OnPlayerJoinedPostfix(NetworkRunner runner, PlayerRef playerRef) {
    if (!runner.IsServer) { return; }
    // ... 20 lines of logic ...
}
```

The exception is `Apply()` (wires hooks, logs warnings) and the mode guard at the top of patch methods for mods that support enable/disable.

## Naming conventions for extracted classes

| Suffix | Purpose | Example |
|---|---|---|
| `*Patch` | Harmony glue only — `Apply()` + one-liner hooks | `RhinoAttackPatch` |
| `*Controller` | Owns UI page state for a page instance lifetime | `HostPageController` |
| `*Orchestrator` | Stateless or cross-cutting session/business logic | `SessionOrchestrator` |
| `*Injector` | One-time UI injection, owns overlay/page state | `ModsButtonInjector` |
| `*Lifecycle` | Plugin-level lifecycle (startup, shutdown) | `ModLifecycle` |
| `*Protocol` | Network protocol state and dispatch | `GameModeProtocol` |
| `Pvp*Ability` | Per-instance PvP sidecar for a game ability class | `PvpRhinoAttackAbility` |

## Controller vs. static class — choose by lifetime

**Instance class** (`*Controller`) when the class holds references to `VisualElement` or `MonoBehaviour` objects tied to a specific page/object instance. Static fields holding `VisualElement` references go stale when the page is destroyed.

**Static class** when the class holds no UI references, or when the data is genuinely global.

## Reflection handles in extracted classes

Resolve `AccessTools` handles once as `private static readonly` fields — never inline in a method body.

```csharp
// Good
private static readonly FieldInfo CursorField = AccessTools.Field(typeof(MenuStartHostPage), "_cursor");

// Bad
private void Inject(MenuStartHostPage page) {
    var cursor = AccessTools.Field(typeof(MenuStartHostPage), "_cursor").GetValue(page); // every call
}
```

## `ref` parameters stay in the patch method

`ref` parameters from Harmony cannot be passed into async methods or captured by lambdas. Keep `ref` read/write in the patch; let the extracted class return the value to assign.

```csharp
static void BeginPlaySessionPrefix(ref string sessionTag, BackendManager.PlaySessionMode mode) =>
    SessionOrchestrator.Begin(ref sessionTag,
        HostPageController.Current?.AllowMods   ?? true,
        HostPageController.Current?.AllowCheats ?? true,
        mode);
```

## Coroutine hosting — never use `__instance.StartCoroutine`

`__instance` can be destroyed mid-wait. Use a dedicated `CoroutineRunner` MonoBehaviour created in `Awake()` with `DontDestroyOnLoad`:

```csharp
// In [ModName]Mod.Awake()
var go = new GameObject("WMF.CoroutineRunner");
DontDestroyOnLoad(go);
Runner = go.AddComponent<CoroutineRunner>();

// Anywhere
WmfMod.Runner.StartCoroutine(MyCoroutine());
```
