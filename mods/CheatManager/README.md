# CheatManager

Enables the game's built-in developer cheat hotkeys in release builds.

The game ships with a full set of cheat hotkeys that are hardlocked off in release builds. This mod unlocks them. Hold **Alt** in-game to display a hotkey reference overlay.

| Key | Action |
|-----|--------|
| `H` | +25% health |
| `U` | Heal all players to full |
| `L` | Kill all enemies |
| `Shift+L` | Trigger level exit |
| `Shift+K` | Restart same level |
| `M` | +200 Black Coins |
| `Shift+M` | +2500 Scrap |
| `N` | +50 XP for all players |
| `9` | Set all-players-dead flag |
| `Shift+B` | Force vending machine |
| `F4` / `F11` / `F12` | Damage player slots 0 / 1 / 2 |

Hotkeys are only active when no UI windows are open.

No configuration required.

---

## WMF

This mod registers with [WMF](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework) as a **Cheat**. When WMF is installed, an **Allow Cheats** toggle appears in the host setup screen. If the host sets it to **No**, CheatManager is disabled for that session and the session name will show a **(cheats)** suffix when it's active.

---

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2246780/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)
- [WMF](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework)

---

## Installation

### 1. Install BepInEx

Skip this step if BepInEx is already installed.

1. Download **BepInEx 5** from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) — pick the `BepInEx_win_x64` build.
2. Extract the contents into your game folder (the one containing `RoB.exe`).
3. Launch the game once and close it — BepInEx will initialize its folder structure.

### 2. Install the mod

1. Download `CheatManager-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=CheatManager).
2. Extract the ZIP into your game's `BepInEx` folder.
3. Launch the game.
