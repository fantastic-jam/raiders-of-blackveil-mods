# Mod Tutorials

Step-by-step tutorial series for building Raiders of Blackveil mods. Each series is self-contained and builds a working mod from scratch.

## Series

### [Player Emotes](player-emotes/)

Build a fully networked emote system. Players press a key and a floating label appears above their character, visible to everyone in the session.

**Covers:** BepInEx plugin setup, WMF registration, settings panel, keybind config, localization, floating text via `EffectText`, Harmony patching, WMF networking.

| Language | Link |
|---|---|
| English | [player-emotes/en/](player-emotes/en/) |
| Français | [player-emotes/fr/](player-emotes/fr/) |

---

### [Loot to Power](loot-to-power/)

Build a WMF game mode that replaces the loot economy with a stat and perk progression system. Pickups are consumed and converted into pools; reaching a threshold grants a reward.

**Covers:** `IGameModeProvider`, multi-variant game modes, pickup interception, threshold rewards, drop blocking.

| Language | Link |
|---|---|
| English | [loot-to-power/en/](loot-to-power/en/) |

---

## Prerequisites (all series)

- Raiders of Blackveil with BepInEx 5
- Wildguard Mod Framework installed
- C# project targeting `netstandard2.1`
- Familiarity with Harmony patching — read [`docs/dev/patterns/harmony-patching.md`](../docs/dev/patterns/harmony-patching.md) first
