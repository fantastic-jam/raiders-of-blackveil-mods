# ThePit

A proving ground for raiders and newcomers. The Pit hosts brutal free-for-all brawls — an honoured tradition used to test the mettle of those who want to earn their place among the crew.

Only the host needs this mod installed.

## Game Mode: The Pit — PvP

A free-for-all arena match where everyone starts bare and grows more powerful as the clock runs down. The player with the most kills when time expires wins.

### How a match works

**Before the arena**

When the host interacts with the planning table in the lobby, a match settings overlay appears instead of the normal raid selection screen. Configure your match, hit OK, and the session begins.

The run passes through one room before reaching the arena. In that room, a series of perk chest rounds play out — by default six rounds — so every player picks up starting perks before combat begins. The door to the arena only opens once all chest rounds are complete.

**Entering the arena**

On arrival in the arena, a 10-second grace period begins. During the grace period:
- All players are invincible and their abilities are locked.
- Movement speed is doubled so you can spread out from spawn.

Once the grace period ends, the match timer starts counting down and combat is open.

**Perks and XP during the match**

Perks drop automatically near each player at a fixed interval throughout the match. The rarity of drops escalates as the match progresses — early drops are mostly Common, and by the later stages of a long match you will see Epic and Legendary perks regularly. Each player can hold up to 20 perks.

XP accumulates passively and continuously. Ability points are granted each time you level up. Every other level also grants a max health increase. All players start the match at level 5 by default, which means skills are available from the first second of combat.

**Death and respawn**

When you die, you respawn at the spawn point after 3 seconds. On respawn you receive 10 seconds of invincibility, invisibility, and a large movement speed boost so you can get clear of the spawn area safely. Abilities are also locked for those 10 seconds.

**End of match**

When the timer reaches zero, the player with the most kills is declared the winner. All other players die and are resurrected 5 seconds later so everyone can reach the exit door together. The winner is immune during this period. The team then votes on whether to continue the run as normal or return to lobby.

A player who reaches the exit before the timer fires also ends the match and returns everyone to the lobby immediately.

## Host Configuration

When you interact with the planning table as the host, the match settings overlay lets you configure the following options for that session. Settings are saved and reloaded the next time you open the overlay.

| Setting | Default | Options |
|---|---|---|
| Duration | 10 min | 5 min, 8 min, 10 min, 15 min, 20 min |
| Drop Rate | Normal | Trickle (3× interval), Slow (2×), Normal (1×), Fast (0.67×), Rapid (0.5×), Frenzy (0.33×) |
| Initial Perks | 6 | 1 – 12 chest rounds before the arena door opens |
| Initial Level | 5 | 1 – 20 (starting XP level for all players) |
| Dmg Reduction | Strong | Off, Gentle, Medium, Strong, Extreme |

**Drop Rate** scales both the perk drop interval and the XP tick interval by the same multiplier. At Frenzy (0.33×), perks and XP arrive roughly three times faster than Normal.

**Initial Level** determines the XP level all players start at. Level 1 means you enter the arena with no skills unlocked. Level 5 (the default) means all ability slots are available from the start.

**Damage Reduction** reduces incoming champion-to-champion damage as players gain XP levels, so freshly spawned players are not immediately one-shot by a fully built opponent. At the Strong setting, a player at maximum XP level (20) takes damage divided by 20. Players at lower XP levels receive proportionally less reduction. Set to Off to disable this entirely.

## Advanced Configuration (BepInEx config)

The `BepInEx/config/io.github.fantastic-jam.raidersofblackveil.mods.thepit.cfg` file exposes additional options edited outside the game.

**[Progression]**

| Key | Default | Description |
|---|---|---|
| `PerkIntervalSeconds` | `30` | Base seconds between perk drops. The Drop Rate stepper multiplies this. |
| `XpTickIntervalSeconds` | `45` | Base seconds per XP level. The Drop Rate stepper multiplies this. |
| `MatchDurationSeconds` | `600` | Fallback match duration (seconds) when no overlay choice is saved. |

**[Steppers]**

These keys let you customise the choices available in the host config overlay. Parse errors revert to the built-in defaults.

| Key | Format | Default |
|---|---|---|
| `DurationOptions` | `Label:seconds,...` | `5 min:300,8 min:480,10 min:600,15 min:900,20 min:1200` |
| `DropRateOptions` | `Label:multiplier,...` (1.0 = normal, 2.0 = half rate) | `Trickle:3.0,Slow:2.0,Normal:1.0,Fast:0.67,Rapid:0.5,Frenzy:0.33` |
| `InitialPerksOptions` | Comma-separated integers, or `Label:int` pairs | `0,1,2,...,12` |
| `DamageReductionOptions` | `Label:maxFactor,...` (1 = no reduction) | `Off:1,Gentle:5,Medium:10,Strong:20,Extreme:40` |

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2060990/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)
- [WildguardModFramework](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework)

## Installation

1. Install BepInEx 5 into your Raiders of Blackveil game folder.
2. Download the latest ThePit ZIP from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=ThePit).
3. Extract the ZIP directly into your game folder — the `plugins/` folder inside the ZIP maps to `BepInEx/plugins/`.
4. Only the host needs the mod. Other players can join a hosted session without installing anything.

## Selecting the game mode

ThePit registers as a game mode through WildguardModFramework. In the lobby, the host opens the game mode selector (provided by WildguardModFramework), selects **The Pit — PvP**, then interacts with the planning table to configure and start the match.
