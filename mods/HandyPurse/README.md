# HandyPurse

Raises the stack limits for all currencies so you stop hitting the cap mid-run.

| Currency | Default cap |
|----------|-------------|
| Scrap | 9 999 |
| Black Coin | 999 |
| Crystals (BlackBlood & Glitter) | 999 |

All caps are configurable.

---

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2246780/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)

---

## Installation

### 1. Install BepInEx

Skip this step if BepInEx is already installed.

1. Download **BepInEx 5** from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) — pick the `BepInEx_win_x64` build.
2. Extract the contents into your game folder (the one containing `RoB.exe`).
3. Launch the game once and close it — BepInEx will initialize its folder structure.

### 2. Install the mod

1. Download `HandyPurse-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=HandyPurse).
2. Extract the ZIP directly into your game folder.
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
