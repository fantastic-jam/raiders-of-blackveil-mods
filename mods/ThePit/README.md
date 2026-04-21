# ThePit

The Pit is where raiders test their skills (and gear) against each other — a free-for-all brawl where players draft perks, earn XP passively, and fight for the most kills before time runs out.

Host-only mod. Other players join without installing anything.

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2060990/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)
- [WildguardModFramework](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework)

## Installation

1. Install BepInEx 5 into your Raiders of Blackveil game folder.
2. Download the latest ThePit ZIP from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=ThePit).
3. Extract the ZIP directly into your game folder — the `plugins/` folder inside the ZIP maps to `BepInEx/plugins/`.

## Selecting the game mode

In the lobby, the host opens the game mode selector (provided by WildguardModFramework), selects **The Pit — PvP**, then interacts with the planning table to configure and start the match.

## Host Configuration

Configure the match from the planning table before starting. Settings are saved between sessions.

| Setting | Default | Options |
|---|---|---|
| Duration | 5 min | 2 min, 5 min, 10 min, 15 min, 20 min |
| Drop Speed | Normal | Sluggish (3×), Slow (2×), Normal (1×), Fast (0.67×), Rapid (0.5×), Frenzy (0.33×) |
| Initial Perks | 6 | 0 – 12 chest rounds before the arena door opens |
| Initial Level | 5 | 1 – 20 starting XP level |
| Dmg Reduction | Off | Off, Gentle, Medium, Strong, Extreme |

**Drop Speed** scales how fast perks are granted and XP ticks. XP stops at level 20; perks continue for the rest of the match.

**Damage Reduction** reduces champion-to-champion damage as players gain XP levels, protecting recently respawned players from fully-built opponents.

## Languages

The configuration UI is available in **English** and **French**. The active language follows the game's language setting.

## Rebalancing (BepInEx config)

The `BepInEx/config/io.github.fantastic-jam.raidersofblackveil.mods.thepit.cfg` file exposes base values for fine-tuning.

| Key | Default | Description |
|---|---|---|
| `PerkIntervalSeconds` | `30` | Base seconds between perk drops (multiplied by Drop Speed). |
| `XpTickIntervalSeconds` | `45` | Base seconds per XP level (multiplied by Drop Speed). Stops at level 20. |
| `MatchDurationSeconds` | `300` | Fallback duration when no overlay choice is saved. |
| `MaxPerksPerPlayer` | `30` | Maximum perks a player can hold (chest rounds and drip combined). |
| `DurationOptions` | `2 min:120,...` | Customise the duration stepper choices. |
| `DropRateOptions` | `Sluggish:3.0,...` | Customise the Drop Speed stepper choices. |
| `InitialPerksOptions` | `0,1,...,12` | Customise the initial perks stepper choices. |
| `DamageReductionOptions` | `Off:1,...` | Customise the damage reduction stepper choices. |
