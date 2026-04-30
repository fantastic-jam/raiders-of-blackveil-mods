# RaiderRoughPatches

A collection of community patches for Raiders of Blackveil, fixing bugs and rough edges that slipped through the official releases. Each fix is a small, targeted patch — no balance changes, no new features.

Each fix can be toggled individually in the BepInEx config file.

## Fixes

- **Session visibility** — hosted session disappears from the server list after returning from a run
- **Stash auto-stack** — stackable items are automatically merged with existing stacks when double-clicking to transfer between champion inventory and stash
- **Cross-item merge** — items of the same type (e.g. hex doll and pig stew) could be merged into the same stack by dragging or double-clicking
- **Barrier self-grant** — self-and-nearest-ally perk effects do not apply to the caster in multiplayer
- **Door vote on disconnect** — door vote gets stuck when a player disconnects mid-vote

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

1. Download `RaiderRoughPatches-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=RaiderRoughPatches).
2. Extract the ZIP into your game's `BepInEx` folder.
3. Launch the game.
