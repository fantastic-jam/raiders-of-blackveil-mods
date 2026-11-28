# JoinAnytime

> **Very beta.** JoinAnytime 1.0.0 is the first build for the IL2CPP game (0.6.5 and later, BepInEx 6). It was only lightly tested: mid-run joins may fail or misbehave. Report bugs with your `BepInEx/LogOutput.log`.
>
> ## Requires the BepInEx IL2CPP pack: [download it from GitHub](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=bepinex)
> BepInEx 5 and the official BepInEx 6 do not work with this game.

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
- [BepInEx IL2CPP for Raiders of Blackveil](https://www.nexusmods.com/raidersofblackveil/mods/19) (BepInEx 5 no longer works)
- [Wildguard Mod Framework](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework) 1.0.0 or later

## Installation

1. Install the [BepInEx IL2CPP pack](https://www.nexusmods.com/raidersofblackveil/mods/19) (or, with Vortex, the *Raiders of Blackveil* game extension, which installs it for you).
2. Install [Wildguard Mod Framework](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework) — required dependency.
3. Download the latest JoinAnytime ZIP from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=JoinAnytime).
4. Extract the ZIP into your game's `BepInEx` folder (so the `plugins` folder is merged).
5. Launch the game — JoinAnytime will be active automatically.
