# JoinAnytime

JoinAnytime lets anyone join a session whose run is already in progress. The game normally only lets a player back in if they were part of the run and got disconnected. JoinAnytime extends that to new players: they spawn right next to the host, in the current room, with XP, ability levels and perks matched to the rest of the party.

Only the host needs the mod. Joining players don't need anything installed.

## How it works

1. While a run is in progress, the session stays listed in the server browser as long as there is a free slot.
2. A new player joins. The host prepares a character for them, using the average XP, ability levels and number of perks of the current party. Perks are random, but with the same rarity as the party's, and fit the champion the newcomer picked.
3. The newcomer spawns next to the host with full health, just like a player reconnecting to their own run. The shrine and other room rewards catch up for them automatically.

From then on, they are a regular member of the run. If they disconnect, they can reconnect like anyone else.

## Good to know

- A party still has at most 3 players.
- Joining opens once the first room of the run has loaded. Before that, joining players are turned away.
- The current room keeps the difficulty it was started with. From the next room on, the run is scaled for the bigger party.
- Talents start empty: the newcomer picks them as they level up.

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2440490/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)
- [Wildguard Mod Framework](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework)

## Installation

1. Install BepInEx 5 into your Raiders of Blackveil game folder by following the [BepInEx installation guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html).
2. Install [Wildguard Mod Framework](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework) — required dependency.
3. Download the latest JoinAnytime ZIP from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=JoinAnytime).
4. Extract the ZIP contents directly into your Raiders of Blackveil game folder (so the `BepInEx/plugins/` folder is merged).
5. Launch the game — JoinAnytime will be active automatically.
