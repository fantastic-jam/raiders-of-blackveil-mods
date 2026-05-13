# 01 — Game Mode Registration

---

Create a WMF game mode with two selectable variants. The mode appears in the host's game mode stepper; clients without the mod are blocked from joining.

## How WMF game modes work

Return `"GameMode"` from `GetModType()` to tell WMF your mod is a selectable mode rather than a passive modifier.

For multiple named variants, implement `IGameModeProvider` alongside `IModRegistrant`. WMF calls `Enable()` first, then `EnableVariant(variantId)` with the player's selection. Apply Harmony patches in `EnableVariant()` — the variant is guaranteed to be set by then.

> **Duck typing does not support `IGameModeProvider`**. Multi-variant game modes must reference both `ModRegistry.dll` and `WildguardModFramework.dll`.

## Step 1: Declare the plugin class

```csharp
using BepInEx;
using ModRegistry;
using WildguardModFramework;

[BepInPlugin(Id, Name, Version)]
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
public class LootToPowerMod : BaseUnityPlugin, IModRegistrant, IGameModeProvider {
    public const string Id      = "io.github.myname.raidersofblackveil.mods.loot-to-power";
    public const string Name    = "LootToPower";
    public const string Version = "0.1.0";

    internal static LootToPowerMod Instance { get; private set; }

    public string GetModType()        => nameof(ModType.GameMode);
    public string GetModName()        => "Loot to Power";
    public string GetModDescription() => "All loot converts to stat and perk pools.";
    public bool   Disabled            { get; private set; }
    public bool   IsClientRequired    => false;

    public void Enable()  { Disabled = false; }
    public void Disable() {
        Disabled = true;
        _harmony?.UnpatchSelf();
        LootToPowerController.Reset();
    }

    private Harmony _harmony;

    public void Awake() {
        Instance = this;
        _harmony = new Harmony(Id);
    }
}
```

> **`IsClientRequired = false`** means clients can join without the mod installed. All pickup interception and pool logic runs on the host only, which is sufficient because the host has authority over all pickups in GameMode=Host. Use `true` only when the game mode changes what clients see or interact with independently.

## Step 2: Implement IGameModeProvider

```csharp
public IReadOnlyList<GameModeVariant> GameModeVariants => new[] {
    new GameModeVariant("rush",   "Loot to Power — Rush",   "Fast pools, frequent rewards."),
    new GameModeVariant("frenzy", "Loot to Power — Frenzy", "Slow pools, rare but bigger rewards."),
};

public void EnableVariant(string variantId) {
    LootToPowerController.Activate(variantId);
    LootToPowerPatch.Apply(_harmony);
}
```

## Step 3: Track the active variant in the controller

```csharp
internal static class LootToPowerController {
    internal static bool IsActive { get; private set; }
    internal static bool IsRush   { get; private set; }

    internal static void Activate(string variantId) {
        IsActive = true;
        IsRush   = variantId == "rush";
    }

    internal static void Reset() {
        IsActive = false;
        IsRush   = false;
    }
}
```

## Result

Launch the game, host a session, and open the game mode stepper. **Loot to Power — Rush** and **Loot to Power — Frenzy** appear as selectable entries. Clients without the mod can join normally.

---

## Next

→ [02-consume-pickups.md](02-consume-pickups.md) — Intercept pickups and convert them to pool points
