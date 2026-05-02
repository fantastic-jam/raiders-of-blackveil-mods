# JoinAnytime

JoinAnytime lets a player join a session that is already mid-run. Instead of being turned away at the door, they connect, wait out the current room, and spawn in at the start of the next room with XP and perks averaged to match the existing players — as if they had been there from the beginning.

Only the host needs the mod. Joining players with the mod installed get the full experience (including shrine rooms). Joining players without the mod get perk choice pickups instead of shrines, which works without any client-side installation.

## How it works

1. The joining player connects and waits — no champion, no presence in the world.
2. When the current room ends and the next room loads, they spawn in normally.
3. Their champion receives floor-averaged XP and rarity-matched random perks from the existing players.

The existing players see no disruption. Enemy count, vote thresholds, and difficulty scaling are all based on the players currently in the world — the waiting joiner does not count.

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

## Notes

- The session browser shows the correct player count including waiting joiners, so the session fills correctly.
- If a waiting player disconnects before the room ends, they are simply removed — no impact on the ongoing run.
- The mod is not required on joining clients. Clients with the mod get shrine rooms; clients without get perk choice pickups instead.
- Clients without the mod may see console errors during the waiting phase (door UI and shrine initialization expect a local player that doesn't exist yet). These are non-breaking — gameplay is unaffected and the errors stop once the joiner is promoted. Clients with the mod installed will not see these errors.
