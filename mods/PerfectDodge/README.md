# PerfectDodge

Adds a **just-dodge** mechanic: if you get hit within a short window right after pressing dash, the hit is fully blocked and your dash charge is instantly refunded. A *dodged* label appears overhead to confirm it.

The timing window is configurable.

---

## ModManager

This mod registers with [ModManager](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=ModManager) as a **Mod**. When ModManager is installed, an **Allow Mods** toggle appears in the host setup screen. If the host sets it to **No**, PerfectDodge is disabled for that session and the session name will show a **(modded)** suffix when it's active.

---

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2246780/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)
- [ModManager](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=ModManager)

---

## Installation

### 1. Install BepInEx

Skip this step if BepInEx is already installed.

1. Download **BepInEx 5** from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) — pick the `BepInEx_win_x64` build.
2. Extract the contents into your game folder (the one containing `RoB.exe`).
3. Launch the game once and close it — BepInEx will initialize its folder structure.

### 2. Install the mod

1. Download `PerfectDodge-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=PerfectDodge).
2. Extract the ZIP into your game's `BepInEx` folder.
3. Launch the game.

---

## Configuration

After the first launch, a config file is created at:

```
BepInEx/config/io.github.fantastic-jam.raidersofblackveil.mods.perfectdodge.cfg
```

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| `PerfectDodgeWindowSeconds` | `0.3` | 0.01 – 1.0 | How many seconds after pressing dash the just-dodge window stays open. |

Restart the game after saving.
