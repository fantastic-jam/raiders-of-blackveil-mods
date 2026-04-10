# Fusion Networked State — Propagation and Ptr Access

## State authority and automatic replication

In `GameMode.Host`, the host holds `StateAuthority` for every `NetworkObject`. Writing to any `[Networked]` property (including those accessed through the raw `Ptr` buffer) on the state authority automatically propagates to all clients on the next simulation snapshot. No RPC is needed — this is standard Fusion snapshot replication.

The host is the only machine that should write. Clients reconcile to the server state each tick. A client that writes to a `[Networked]` property will have its value overwritten at the next snapshot reconciliation, and can cause display glitches (see `fusion_networking.md` for the `AddDamageData` counter example).

## `NetworkBehaviour.Ptr` — what it is and how to access it

`Ptr` is declared on `NetworkBehaviour` as:
```csharp
internal unsafe int* Ptr;
```

It is an `internal` **field** (not a property). It is not `public`. Access requires reflection:
```csharp
private static FieldInfo _ptrField;
// in Apply():
_ptrField = AccessTools.Field(typeof(NetworkBehaviour), "Ptr");
if (_ptrField == null) { Log.LogWarning("Ptr field not found"); return; }
```

The raw pointer type (`int*`) is not directly recoverable via `FieldInfo.GetValue`. Instead, use an unsafe helper:
```csharp
private static unsafe int* GetPtr(NetworkBehaviour nb) {
    // Ptr is an internal field — read it via Traverse
    return (int*)Traverse.Create(nb).Field<IntPtr>("Ptr").Value;
    // NOTE: Traverse boxes IntPtr, which loses the unsafe ptr.
    // The correct approach is a typed delegate or IL emit.
}
```

In practice, the cleanest approach from mod code is **not** to read `Ptr` directly, but to call the methods that already do the pointer arithmetic. See "Preferred approach" below.

## `ChampionAbilityWithCooldown` state layout

From ILSpy decompile of the weaver-generated properties:

| Property | Word offset | Byte offset | Type | Access |
|---|---|---|---|---|
| `NetworkedCooldownReduction` | 14 | 56 | `float` | `private` |
| `CooldownTimer` | 15 | 60 | `PausableTickTimer` (16 bytes / 4 words) | `private` |
| `ChargeCount` | 19 | 76 | `byte` | `public` getter, `protected` setter |
| `Charge_Actual` | 20 | 80 | `byte` | `public` getter, `private` setter |
| `ChargeRate` | 21 | 84 | `float` | `protected`, `private` setter |

Word offset N means `Ptr + N` in int-pointer arithmetic = byte offset `N * 4`. Exception: byte properties use direct byte addressing — `Charge_Actual` getter reads `((byte*)Ptr)[80]` and setter writes `((sbyte*)Ptr)[80]`.

`PausableTickTimer` is `[StructLayout(LayoutKind.Explicit, Size = 16)]` (`[NetworkStructWeaved(4)]` — 4 int-words). Starts at byte 60.

## Writing `CooldownTimer` — risks of direct Ptr writes

Both `CooldownTimer` and `Charge_Actual` are `private` setters. `ChargeCount` has a `protected` setter. None are accessible directly from external code.

A direct unsafe write by getting `Ptr` via reflection is mechanically possible:

```csharp
// Unsafe example — NOT the recommended approach
private static unsafe void ForceStartCooldown(ChampionAbilityWithCooldown ability, float seconds) {
    var nb = (NetworkBehaviour)ability;
    // Ptr is internal — use a typed field pointer trick or IL emit
    // Writing directly bypasses all business logic in StartCooldownTimer()
    var timer = PausableTickTimer.CreateFromSeconds(ability.Runner, seconds);
    // *(PausableTickTimer*)(nb.Ptr + 15) = timer;  // requires unsafe + IL emit to get nb.Ptr
}
```

Risks:
- `Ptr` is `internal` — reflection boxing makes retrieving a live `int*` non-trivial; you need `IL.Emit` or `Marshal.ReadIntPtr` from the field's memory address.
- Bypasses `StartCooldownTimer()` which also calls `_champion.CheckNumberOfAbilityCooldowns()`.
- Bypasses SAP pause and not-in-combat pause logic in `FixedUpdateNetwork()`.
- No null-check on `Ptr` — the generated properties guard `Ptr == null`; a direct write does not.
- `Charge_Actual` setter is `private` — the field it maps to is at byte 80. Writing byte 80 with a wrong value while `ChargeCount > 1` can leave inconsistent multi-charge state.

## Preferred approach — call the public API

`ChampionAbilityWithCooldown` exposes these public methods that correctly manage all cooldown state:

```csharp
// Reset one charge and restart timer (public)
ability.ResetActualCooldown();

// Signal "reset when next idle" (public) — safe even mid-animation
ability.ResetCooldownWhenPossible();

// ModifyProperty with AllActualCooldownReset, DashActualCooldownReset, etc.
// Dispatches to ResetActualCooldown() internally.
ability.ModifyProperty(Property.AllActualCooldownReset, 0f);
```

`ModifyProperty` is `public override` — callable via reflection on the concrete ability type or via `AccessTools.Method(typeof(ChampionAbilityWithCooldown), "ModifyProperty")`.

To start a cooldown from the host as if the ability just fired:

```csharp
// Host only — call via reflection since StartCooldownTimer is private
private static MethodInfo _startCooldownTimer;
// in Apply():
_startCooldownTimer = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "StartCooldownTimer");

// in patch:
if (ability.Runner?.IsServer != true) return;
_startCooldownTimer.Invoke(ability, null);
// This also calls _champion.CheckNumberOfAbilityCooldowns(), consistent with game logic.
```

`StartCooldownTimer` is `private void` — no parameters. It reads `ChargeRate`, `CooldownTime` (which reads `NetworkedCooldownReduction`), and calls `PausableTickTimer.CreateFromSeconds`. Since it writes to `CooldownTimer` (private setter via the property itself), the generated property's null-check on `Ptr` still runs.

## Writing `Charge_Actual` — via `ModifyProperty`

`Charge_Actual` has a `private` setter. The only safe external way to reduce it (simulate using a charge) is through `ModifyProperty`. To set charges to a specific value, use the `Charge` property variants:

```csharp
// Reduce charge count by 1 (simulates consuming a charge)
ability.ModifyProperty(Property.OffensiveCharge, -1f);
// Restore charges
ability.ModifyProperty(Property.OffensiveCharge, 1f);
```

Decrement of `Charge_Actual` directly from host code requires calling `StartCooldownTimer` afterward to be consistent with the game's state machine.

## Summary: when to use what

| Goal | Approach |
|---|---|
| Put ability on cooldown (host) | `AccessTools.Method(..., "StartCooldownTimer").Invoke(ability, null)` |
| Reset cooldown now | `ability.ResetActualCooldown()` |
| Reset cooldown when idle | `ability.ResetCooldownWhenPossible()` |
| Apply CDR | `ability.ModifyProperty(Property.AllCooldownReduction, pct)` |
| Fast-forward remaining time | `ability.ModifyProperty(Property.AllActualCooldownDecrement, seconds)` |
| Direct Ptr write | Avoid — requires unsafe IL emit, bypasses business logic |

All of these propagate automatically to clients because they write `[Networked]` properties on the state authority.
