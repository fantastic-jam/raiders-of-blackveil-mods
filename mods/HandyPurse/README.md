# HandyPurse

Raises the stack limits for all currencies so you stop hitting the cap mid-run.

| Currency | Default cap |
|----------|-------------|
| Scrap | 9 999 |
| Black Coin | 999 |
| Crystals (BlackBlood & Glitter) | 999 |

All caps are configurable.

When you drop a currency stack that exceeds the vanilla limit it is automatically split into multiple vanilla-sized pickups so the game handles them correctly.

---

## Before uninstalling

Use the **Prepare for Uninstall** button in the in-game pause menu (Mods › HandyPurse) before removing the mod.

It drops every currency above the vanilla limit as vanilla-sized pickups at your feet, then disables HandyPurse automatically. Pick the stacks back up — they will merge normally within vanilla limits. You can then safely uninstall.

Vanilla limits: Scrap 3,000 · Black Coin 200 · Crystals 200

> Must be solo or session host. The button is in **Pause → Mods → HandyPurse**.

If you skip this step and uninstall without preparing, the game will silently clamp any over-cap stacks to the vanilla limit on the next save — the excess is lost.

---

## Bank

The Bank is a safety net for stacks that exceed your configured caps at save time — for example when you join a session where you do not have save authority.

**How it works:**

When the game saves and a currency stack would be clamped, HandyPurse strips the excess and writes it to a local file instead of discarding it:

- If your inventory slot layout is the same as when the excess was saved, it is written to `BepInEx/data/HandyPurse/topup.json` and restored to the exact same slot on your next load.
- If the slot layout has changed (items moved or a slot is now occupied by something else), the excess is moved to `BepInEx/data/HandyPurse/bank.json` for safe keeping until you manually recover it.

**Recovering banked funds:**

Open **Pause → Mods → HandyPurse** to see your current bank balance and any pending topup. If you have a balance in `bank.json`, use the **Drop bank to floor** button — this spawns the stored currency as floor pickups at your feet so you can walk over them and pick them up normally. The button is only available in the lobby and only to the session host.

---

## WMF

This mod registers with [WMF](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework) as a **Mod**. When WMF is installed, an **Allow Mods** toggle appears in the host setup screen. If the host sets it to **No**, HandyPurse is disabled for that session and the session name will show a **(modded)** suffix when it's active.

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

---

## Configuration

After the first launch, a config file is created at:

```
BepInEx/config/io.github.fantastic-jam.raidersofblackveil.mods.handypurse.cfg
```

Open it with any text editor:

```ini
[Limits]
ScrapCap = 9999
BlackCoinCap = 999
CrystalCap = 999
```

Set any value to whatever cap you want. Restart the game after saving.
