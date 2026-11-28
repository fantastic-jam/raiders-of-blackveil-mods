# Wildguard Mod Framework (WMF)

> **Very beta.** WMF 1.0.0 is the first build for the IL2CPP game (0.6.5 and later, BepInEx 6). It was only lightly tested: expect bugs, and report them with your `BepInEx/LogOutput.log`. Everyone in a session should use WMF 1.0.0.
>
> ## Requires the BepInEx IL2CPP pack: [download it from GitHub](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=bepinex)
> BepInEx 5 and the official BepInEx 6 do not work with this game.

The mod framework for Raiders of Blackveil. WMF gives players in-game control over their mods and gives mod authors a common API for discovery, game modes, and settings menus — with no engine restarts required.

- In-game Mods menu with enable/disable toggles and per-mod settings panels
- Game mode selection on the host setup screen, with optional client-requirement enforcement
- Server chat visible to all players in the session
- Player management overlay (F2 by default, rebindable) with host kick and ban controls
- Reliable networking channel for mods to exchange messages without touching the game's network layer
- Corner notifications — mods can surface messages to all WMF players in the session
- Localisation support — mods can ship translation files and follow the player's in-game language setting

**Mod authors:** see [API.md](API.md) for the full mod author reference.

---

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2246780/Raiders_of_Blackveil/)
- [BepInEx IL2CPP for Raiders of Blackveil](https://www.nexusmods.com/raidersofblackveil/mods/19) (BepInEx 5 no longer works)

---

## Installation

### 1. Install BepInEx

Skip this step if the BepInEx IL2CPP pack is already installed.

- **With Vortex:** install the *Raiders of Blackveil* game extension, it installs BepInEx for you.
- **Manually:** follow the instructions of the [BepInEx IL2CPP pack](https://www.nexusmods.com/raidersofblackveil/mods/19). The first launch takes one to two minutes longer.

### 2. Install the mod

1. Download `WildguardModFramework-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework).
2. Extract the ZIP into your game's `BepInEx` folder. `ModRegistry.dll` is bundled inside, no separate download needed.
3. Launch the game.

---

## Languages

WMF's own UI is available in **English** and **French**. The active language follows your in-game language setting — no extra configuration required.

---

## Mod Discovery

A **Mods** button is added to the main menu and the in-game pause menu. Opening it shows every installed BepInEx plugin. Mods that support enable/disable have a working toggle — turn them off permanently or bring them back without touching the filesystem.

The enabled/disabled state is saved to a config file and applied at startup. New mods are detected automatically; uninstalled mods are cleaned up from the list.

**Multiplayer awareness** — when hosting, **Allow Mods** and **Allow Cheats** toggles appear on the host setup screen. The session name is suffixed with **(cheats)** or **(modded)** so other players know what they're joining.

---

## Game Modes and Variants

A **Game Mode** stepper appears on the host setup screen and the solo start screen. Mods that register as game modes show up as selectable entries. Only one game mode can be active per session. The default is **Normal** (no game mode active).

When `IsClientRequired` is set, other players joining must also have the mod installed and enabled.

---

## Mod Menus

The Mods screen is split into a left nav bar and a right content panel. Mods that expose a settings menu get their own named entry — players can browse settings for each mod without leaving the game.

---

## Server Chat

An in-game text chat is available in every hosted session. Press Enter to open the input field, type a message, and send — everyone sees it. Chat settings live in the **WMF → Chat** sub-menu.

---

## Player Management

Hold **F2** during a run or in the lobby to reveal a live list of all connected players. Hosts get **kick** and **ban** controls. The key can be changed and the ban list edited in the **WMF → Players** sub-menu.

---

## Notifications

Mods can send short corner notifications to all WMF players in the session — for example, "Install Spectate Mode for better compatibility." Notifications appear in the bottom-right corner of the screen and auto-close after a few seconds. See [API.md](API.md#notifications-api) for how to send them from your mod.
