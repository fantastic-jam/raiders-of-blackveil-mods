# Sidecar pattern — per-instance logic without polluting the patch class

## When to use

Use a sidecar when a patch needs per-instance state (detectors, cached fields, derived data) tied to the lifetime of a specific `MonoBehaviour`/`NetworkBehaviour`. The alternative — static dictionaries keyed by `NetworkId` — leaks entries and makes lifetime management manual.

## Three-file layout

| File | Role |
|---|---|
| `[X]Patch.cs` | Harmony glue only — `Apply()`, `ConditionalWeakTable`, one-liner postfixes |
| `Pvp[X]Ability.cs` | Per-instance sidecar — owns detectors, business logic |
| `PvpActorColliderDetector.cs` | Shared infrastructure — collider-list API over `PvpDetector` |

## `ConditionalWeakTable` for sidecar storage

```csharp
private static readonly ConditionalWeakTable<RhinoAttackAbility, PvpRhinoAttackAbility> _sidecars = new();

private static void SpawnedPostfix(RhinoAttackAbility __instance) {
    _sidecars.Remove(__instance);   // evict stale sidecar — Fusion reuses the same C# object on respawn
    _sidecars.Add(__instance, new PvpRhinoAttackAbility(__instance));
}

private static void DoHitPostfix(RhinoAttackAbility __instance) {
    if (_sidecars.TryGetValue(__instance, out var sidecar)) { sidecar.DoHit(); }
}
```

`ConditionalWeakTable` releases the entry automatically when the game object is GC'd. `Remove` + `Add` (not just `Add`) is required because Fusion recycles `NetworkObject` C# instances from its pool — `Spawned()` fires again on the same CLR object.

## Sidecar constructor — public fields directly, reflection only for private

```csharp
internal PvpRhinoAttackAbility(RhinoAttackAbility instance) {
    _inst = instance;
    var self = new[] { instance.Stats };
    // normalHitCollider1 etc. are public — no reflection needed
    _normalDetector = new PvpActorColliderDetector(
        new[] { instance.normalHitCollider1, instance.normalHitCollider2 }, self);
    _lastDetector = new PvpActorColliderDetector(instance.lastHitCollider1, self);
}
```

## `PvpActorColliderDetector` — fixed mask, explicit excludes

`PvpActorColliderDetector` always uses `PvpDetector.PvpLayerMask` (includes Player layer). No `Target` parameter — the mask is fixed because every PvP detector needs the same layer set. Accepts `StatsManager[] excludes` to skip the caster.

```csharp
// Good
new PvpActorColliderDetector(new[] { col1, col2 }, excludes: new[] { self })

// Bad — accepting a Target you then ignore is misleading
new PvpActorColliderDetector(col, ActorColliderDetector.AllTargetForChampions, excludes: new[] { self })
```

## Lifecycle safety

- `DoHit` fires as a postfix on the instance's own method → instance is alive while it runs.
- `if (c == null) { continue; }` in `DoDetection()` uses Unity's `==` — correctly handles destroyed objects.
- Physics queries in a `FixedUpdateNetwork` postfix are safe **only with an `IsServer` guard**. The server never resimulates; removing the guard would cause multi-hits during client-side prediction.

## Document deliberate PvE divergences

```csharp
// PvP: flat force, no distance falloff (unlike the PvE version which
// lerps 1→0.6 over 5 units). Intentional — distance-falloff feels
// unreliable in tight PvP engagements.
target.Character.AddPushForce(pushDir.normalized * _inst.pushForceLastHit);
```
