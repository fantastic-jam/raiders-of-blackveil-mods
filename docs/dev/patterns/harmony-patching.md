# Harmony patching

## Resolve reflection handles once, in `Apply()` — never inline

```csharp
// Good
private static FieldInfo _syncedItemsField;

public static void Apply(Harmony harmony) {
    _syncedItemsField = AccessTools.Field(typeof(Inventory), "_syncedItems");
    if (_syncedItemsField == null)
        RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find Inventory._syncedItems — strip/restore inactive.");
}

// Bad — resolves on every call
static void SomePostfix(Inventory inv) {
    var field = AccessTools.Field(typeof(Inventory), "_syncedItems");
}
```

## Warn and bail per handle — independent guards

Each handle gets its own null-check and `LogWarning`. Do not combine them into one `if (a == null || b == null)` — that silently disables everything when only one field is missing.

```csharp
_syncedItemsField = AccessTools.Field(typeof(Inventory), "_syncedItems");
if (_syncedItemsField == null)
    Mod.PublicLogger.LogWarning("...: Could not find Inventory._syncedItems — feature inactive.");

_flagsField = AccessTools.Field(typeof(Inventory), "_receivedBackendDataFlags");
if (_flagsField == null)
    Mod.PublicLogger.LogWarning("...: Could not find Inventory._receivedBackendDataFlags — restore inactive.");
```

Warning message must name the missing member and state which feature goes inactive.

## `Apply()` returns `bool` — fail-safe on breaking changes

```csharp
public static bool Apply(Harmony harmony) {
    var getter = AccessTools.PropertyGetter(typeof(GenericItemDescriptor), nameof(GenericItemDescriptor.AmountMaximum));
    if (getter == null) {
        Mod.PublicLogger.LogWarning("...: AmountMaximum not found — stack protection unavailable.");
        return false;  // critical: unpatched mod could corrupt saves
    }
    harmony.Patch(getter, postfix: ...);
    return true;
}
```

Return `false` only if the absent patch could cause user harm. Optional features warn and continue.

## `Awake()` — unpatch + disable on failure

```csharp
private void Awake() {
    PublicLogger = Logger;
    try {
        _harmony = new Harmony(Id);
        if (!FooPatch.Apply(_harmony)) {
            _harmony.UnpatchSelf();
            LogBreakingChange();
            return;
        }
        PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
    }
    catch (Exception ex) {
        PublicLogger.LogError(ex);
    }
}
```

`_harmony.UnpatchSelf()` before returning — a partially patched mod is worse than an unpatched one.

## Patch method visibility

Patch methods are `static` and package-private (no modifier). `Apply()` is `internal static` (called from outside namespace). Helper methods are `private static`.

## Reference by `nameof`, not string literal

```csharp
// Good
harmony.Patch(method, postfix: new HarmonyMethod(typeof(MyPatch), nameof(MyPostfix)));

// Bad — silently breaks on rename
harmony.Patch(method, postfix: new HarmonyMethod(typeof(MyPatch), "MyPostfix"));
```

## `PublicLogger` — always `public static ManualLogSource`

```csharp
// In [ModName]Mod.cs
public static ManualLogSource PublicLogger;

private void Awake() {
    PublicLogger = Logger;  // assign before anything that might log
    ...
}
```

## Calling a base-class method non-virtually from a prefix

`MethodInfo.Invoke` on Mono/CLR **always uses virtual dispatch** — it walks the vtable of the runtime type. If you obtain `AccessTools.Method(typeof(BaseClass), "VirtualMethod")` and call `.Invoke(derivedInstance, ...)`, the derived override is called, not the base. In a prefix this causes Harmony's reentrancy guard to skip the prefix on the second call, running the vanilla body you meant to block.

**Wrong — triggers virtual dispatch into the patched override:**

```csharp
private static MethodInfo _baseFixedUpdate;
// Apply(): _baseFixedUpdate = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "FixedUpdateNetwork");

static bool MyPrefix(DerivedAbility __instance) {
    _baseFixedUpdate?.Invoke(__instance, null);  // dispatches to DerivedAbility.FixedUpdateNetwork — re-enters patch
    ...
}
```

**Right — emit `OpCodes.Call` (non-virtual) via `DynamicMethod`:**

```csharp
using System.Reflection.Emit;

private static Action<ChampionAbilityWithCooldown> _baseFixedUpdateCall;

// In Apply():
var baseMethod = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "FixedUpdateNetwork");
if (baseMethod != null) {
    var dm = new DynamicMethod(
        "__BaseFixedUpdate",
        typeof(void),
        new[] { typeof(ChampionAbilityWithCooldown) },
        typeof(MyPatch).Module,
        skipVisibility: true);
    var il = dm.GetILGenerator();
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Call, baseMethod);   // non-virtual — calls ChampionAbilityWithCooldown directly
    il.Emit(OpCodes.Ret);
    _baseFixedUpdateCall = (Action<ChampionAbilityWithCooldown>)dm.CreateDelegate(typeof(Action<ChampionAbilityWithCooldown>));
}

static bool MyPrefix(DerivedAbility __instance) {
    _baseFixedUpdateCall?.Invoke(__instance);   // safe — non-virtual, no re-entry
    ...
}
```

For methods with parameters, declare a matching `private delegate` and use `Ldarg_1`, `Ldarg_2`, etc. The delegate's first parameter type must be assignment-compatible with the method's declaring type.

## Re-entrant guard — use `[ThreadStatic]`, not `static bool`

```csharp
[ThreadStatic] private static bool _rerolling;

static void TestDropChancePostfix(EnemyDropRuntime __instance, ref bool __result, ...) {
    if (!State.IsActive || __result || _rerolling) return;
    _rerolling = true;
    try {
        __result = __instance.TestDropChance(...);
    } finally {
        _rerolling = false;
    }
}
```

## Coroutine prefix — set `__result` before returning `false`

```csharp
static bool SavePlayerGameStatesPrefix(ref IEnumerator<WaitForSeconds> __result, Action backendRequestCompleted) {
    if (!State.IsActive) return true;
    backendRequestCompleted?.Invoke();  // caller may block until this fires
    __result = EmptyCoroutine();
    return false;
}

private static IEnumerator<WaitForSeconds> EmptyCoroutine() { yield break; }
```

Never return `false` without setting `__result` when the original returns a reference type.

## Naming conventions

| Thing | Convention | Example |
|---|---|---|
| Patch class | `[Subject]Patch` | `RhinoAttackPatch` |
| Plugin class | `[ModName]Mod` | `ThePitMod` |
| State class | `[ModName]State` | `ThePitState` |
| Reflection fields | `_camelCaseField` | `_syncedItemsField` |
| Postfix method | `[OriginalName]Postfix` | `DoHitPostfix` |
| Prefix method | `[OriginalName]Prefix` | `SavePostfix` |
| Log prefix | `"[ModName]: message"` | `"ThePit: ..."` |

Avoid "Manager" suffix — clashes with the game's own `NetworkManager`, `PlayerManager`, etc.
