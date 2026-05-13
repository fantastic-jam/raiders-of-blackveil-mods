# Player Emotes

Build a mod that lets players send floating text reactions above their character — visible to all players in the session. Each tutorial adds one layer on top of the previous one.

## Tutorials

| # | File | What you will learn |
|---|---|---|
| 1 | [01-init.md](01-init.md) | Scaffold a BepInEx plugin and register it with WMF via IModRegistrant |
| 2 | [02-settings-panel.md](02-settings-panel.md) | Add an in-game settings panel with a configurable keybind |
| 3 | [03-localization.md](03-localization.md) | Localize the settings panel label |
| 4 | [04-local-labels.md](04-local-labels.md) | Show a floating label above the local player on keypress |
| 5 | [05-networking.md](05-networking.md) | Broadcast the emote to all players in the session |

## Prerequisites

- BepInEx 5 installed in the game folder
- Wildguard Mod Framework installed
- A C# project targeting `netstandard2.1` with references to `BepInEx.dll`, `ModRegistry.dll`, and `WildguardModFramework.dll`

## What you will build

A fully networked emote system. By the end of tutorial 5, any player can press a key and all players in the session see a floating label above that player's head.
