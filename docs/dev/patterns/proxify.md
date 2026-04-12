# Proxy pattern — per-instance logic alongside or replacing vanilla

Two variants depending on whether vanilla should run or not:

| Variant | Patch type | When to use |
|---|---|---|
| **Augmentation** | postfix | Vanilla runs; our class adds behavior (e.g. collider-based PvP damage) |
| **Proxy** | prefix + return false | Vanilla is fully replaced; the game class becomes a thin shell |

---

## Augmentation variant (postfix)

Use this when vanilla logic should still execute and you're layering PvP behavior on top.

### Three-file layout

| File | Role |
|---|---|
| `[X]Patch.cs` | Harmony glue only — `Apply()`, `ConditionalWeakTable`, one-liner postfixes |
| `Pvp[X]Ability.cs` | Per-instance implementation — owns detectors, business logic |
| `PvpActorColliderDetector.cs` | Shared infrastructure — collider-list API over `PvpDetector` |

### `ConditionalWeakTable` for per-instance storage

```csharp
private static readonly ConditionalWeakTable<RhinoAttackAbility, PvpRhinoAttackAbility> _proxies = new();

private static void SpawnedPostfix(RhinoAttackAbility __instance) {
    _proxies.Remove(__instance);   // evict stale entry — Fusion reuses the same C# object on respawn
    _proxies.Add(__instance, new PvpRhinoAttackAbility(__instance));
}

private static void DoHitPostfix(RhinoAttackAbility __instance) {
    if (_proxies.TryGetValue(__instance, out var proxy)) { proxify.DoHit(); }
}
```

`ConditionalWeakTable` releases entries automatically when the game object is GC'd. `Remove` + `Add` (not just `Add`) is required because Fusion recycles `NetworkObject` C# instances from its pool — `Spawned()` fires again on the same CLR object.

### Implementation constructor — public fields directly, reflection only for private

```csharp
internal PvpRhinoAttackAbility(RhinoAttackAbility instance) {
    _inst = instance;
    var self = new[] { instance.Stats };
    _normalDetector = new PvpActorColliderDetector(
        new[] { instance.normalHitCollider1, instance.normalHitCollider2 }, self);
    _lastDetector = new PvpActorColliderDetector(instance.lastHitCollider1, self);
}
```

### Patch every overridden public method

When the target class overrides multiple lifecycle methods, patch **all of them** and forward each to the matching implementation method.

```csharp
// Patch.cs — one postfix per override, each a one-liner
harmony.Patch(spawned,      postfix: new HarmonyMethod(..., nameof(SpawnedPostfix)));
harmony.Patch(fixedUpdate,  postfix: new HarmonyMethod(..., nameof(FixedUpdateNetworkPostfix)));
harmony.Patch(onCharEvent,  postfix: new HarmonyMethod(..., nameof(OnCharacterEventPostfix)));

private static void SpawnedPostfix(MyAbility __instance) {
    _proxies.Remove(__instance);
    _proxies.Add(__instance, new PvpMyAbility(__instance));
}
private static void FixedUpdateNetworkPostfix(MyAbility __instance) {
    if (_proxies.TryGetValue(__instance, out var s)) { s.OnFixedUpdate(); }
}
private static void OnCharacterEventPostfix(MyAbility __instance) {
    if (_proxies.TryGetValue(__instance, out var s)) { s.OnCharacterEvent(); }
}
```

`Spawned` uses `Remove + Add` (eager creation). All other methods use `TryGetValue`.

---

## Proxy variant (prefix + return false)

Use this when the PvP behavior is a **complete replacement** — the vanilla method body must not run at all. The game class becomes a thin proxy; the `Pvp[X]` class owns all logic including the state machine.

### How it works

A prefix returning `false` blocks the entire method body, including any `base.X()` calls inside it. You must explicitly invoke the base-class method via a stored `MethodInfo` if its infrastructure is still needed (e.g. cooldown management in `ChampionAbilityWithCooldown.FixedUpdateNetwork`).

```csharp
// Patch.cs
private static MethodInfo _baseFixedUpdate;
private static MethodInfo _baseOnCharEvent;

internal static void Apply(Harmony harmony) {
    PvpMyAbility.Init();

    _baseFixedUpdate = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "FixedUpdateNetwork");
    _baseOnCharEvent = AccessTools.Method(typeof(ChampionAbility), "OnCharacterEvent",
        new[] { typeof(StatsManager), typeof(CharacterEvent), typeof(TriggerParams) });

    // Spawned: postfix — vanilla registers events; we just attach our implementation
    harmony.Patch(spawned,     postfix: new HarmonyMethod(..., nameof(SpawnedPostfix)));
    // All other overrides: prefix returning false — vanilla body must not run
    harmony.Patch(fixedUpdate, prefix:  new HarmonyMethod(..., nameof(FixedUpdateNetworkPrefix)));
    harmony.Patch(onCharEvent, prefix:  new HarmonyMethod(..., nameof(OnCharacterEventPrefix)));
}

private static void SpawnedPostfix(MyAbility __instance) {
    _proxies.Remove(__instance);
    _proxies.Add(__instance, new PvpMyAbility(__instance));
}

private static bool FixedUpdateNetworkPrefix(MyAbility __instance) {
    if (!ThePitState.IsDraftMode) { return true; }     // vanilla runs outside PvP
    _baseFixedUpdate?.Invoke(__instance, null);         // cooldown / base infrastructure
    if (_proxies.TryGetValue(__instance, out var s)) { s.OnFixedUpdate(); }
    return false;                                       // block vanilla body
}

private static bool OnCharacterEventPrefix(MyAbility __instance,
    StatsManager owner, CharacterEvent gameplayEvent, TriggerParams triggerParam) {
    if (!ThePitState.IsDraftMode) { return true; }
    _baseOnCharEvent?.Invoke(__instance, new object[] { owner, gameplayEvent, triggerParam });
    if (_proxies.TryGetValue(__instance, out var s)) { s.OnCharacterEvent(gameplayEvent); }
    return false;
}
```

### Implementation class owns the full state machine

All private fields and protected methods needed to replicate vanilla behavior are resolved once in `Init()` and stored as `private static` fields.

```csharp
internal class PvpMyAbility {
    private static FieldInfo    _durationField;
    private static MethodInfo   _startInnerStateMethod;
    private static PropertyInfo _stateTimerProp;
    // ... other reflection handles

    internal static void Init() {
        _durationField       = AccessTools.Field(typeof(MyAbility), "_duration");
        _startInnerStateMethod = AccessTools.Method(typeof(ChampionAbility), "StartInnerState",
            new[] { typeof(int), typeof(int) });
        // warn on null per handle
    }

    internal void OnFixedUpdate() {
        if (!_inst.Object.HasStateAuthority) { return; }
        // Full state machine here — no vanilla running behind us
    }
}
```

---

## `PvpActorColliderDetector` — fixed mask, explicit excludes

Always uses `PvpDetector.PvpLayerMask` (includes Player layer). No `Target` parameter — the mask is fixed because every PvP detector needs the same layer set. Accepts `StatsManager[] excludes` to skip the caster.

```csharp
// Good
new PvpActorColliderDetector(new[] { col1, col2 }, excludes: new[] { self })

// Bad — accepting a Target you then ignore is misleading
new PvpActorColliderDetector(col, ActorColliderDetector.AllTargetForChampions, excludes: new[] { self })
```

## Lifecycle safety

- Physics queries in a `FixedUpdateNetwork` postfix are safe **only with an `IsServer` guard**. The server never resimulates; removing the guard would cause multi-hits during client-side prediction.
- `if (c == null) { continue; }` in `DoDetection()` uses Unity's `==` — correctly handles destroyed objects.

## Document deliberate PvE divergences

```csharp
// PvP: flat force, no distance falloff (unlike the PvE version which
// lerps 1→0.6 over 5 units). Intentional — distance-falloff feels
// unreliable in tight PvP engagements.
target.Character.AddPushForce(pushDir.normalized * _inst.pushForceLastHit);
```
