# HandyPurse

Recovers excess currency banked by earlier versions of HandyPurse.

If you used HandyPurse before the Ghoulag Update, you may have currency sitting in a local bank file from when the mod managed stack limits. This version migrates that data on startup and lets you drop it back into the game.

---

## How it works

On startup HandyPurse reads any existing topup files left by prior versions and deposits them into the local bank. Open **Pause → Mods → HandyPurse** to see your current balance. In the lobby, use the **Drop bank to floor** button to spawn the stored currency as floor pickups at your feet — walk over them to pick them up normally.

The button is only available in the lobby and only to the session host (or in solo play).

---

## WMF

This mod registers with [WMF](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework) as a **Mod**. When WMF is installed, an **Allow Mods** toggle appears in the host setup screen. If the host sets it to **No**, HandyPurse is disabled for that session.

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

1. Download `HandyPurse-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=HandyPurse).
2. Extract the ZIP into your game's `BepInEx` folder.
3. Launch the game.
