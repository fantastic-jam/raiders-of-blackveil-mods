# PerfectDodge

Adds a **just-dodge** mechanic: if you get hit within a short window right after pressing dash, the hit is fully blocked and your dash charge is instantly refunded. A *dodged* label appears overhead to confirm it.

The timing window is configurable.

---

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2246780/Raiders_of_Blackveil/)
- [BepInEx 6](https://builds.bepinex.dev/projects/bepinex_be) (bleeding edge, Unity Mono build)

---

## Installation

### 1. Install BepInEx

Skip this step if BepInEx is already installed.

1. Download **BepInEx 6** (bleeding edge) from the [BepInEx releases page](https://builds.bepinex.dev/projects/bepinex_be) — pick the `BepInEx_UnityMono_x64` build.
2. Extract the contents into your game folder (the one containing `RoB.exe`).
3. Launch the game once and close it — BepInEx will initialize its folder structure.

### 2. Install the mod

1. Download `PerfectDodge-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=PerfectDodge).
2. Extract the ZIP directly into your game folder.
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

---

## Translations

The *dodged* label is translated automatically based on your in-game language. To add or override a translation, create a file named `PerfectDodge.<lang>.json` in:

```
BepInEx/plugins/PerfectDodge/Assets/Localization/
```

Where `<lang>` is your language code (e.g. `de`, `es`, `pt-br`). Example content:

```json
{
  "perfect_dodge.dodged_label": "*esquivé*"
}
```
