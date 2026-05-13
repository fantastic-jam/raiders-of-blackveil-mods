# Loot to Power

Build a WMF game mode that replaces the loot economy with a stat and perk progression system. Every pickup is consumed and converted into one of two pools: **stats** (from scrap and equipment) or **perks** (from black coins and glitter). Reaching a threshold grants a reward and resets that pool.

## Tutorials

| # | File | What you will learn |
|---|---|---|
| 1 | [01-game-mode.md](01-game-mode.md) | Register a multi-variant game mode with IGameModeProvider |
| 2 | [02-consume-pickups.md](02-consume-pickups.md) | Intercept pickups and convert them to pool points |
| 3 | [03-threshold-rewards.md](03-threshold-rewards.md) | Grant stats and perks when a pool threshold is reached |
| 4 | [04-block-dropping.md](04-block-dropping.md) | Prevent players from dropping items to the floor |

## Prerequisites

- Familiarity with WMF duck typing and Harmony patching (see the Emote Mod series)
- A reference to both `ModRegistry.dll` and `WildguardModFramework.dll` in your project
- The pattern docs: `docs/dev/patterns/harmony-patching.md`, `patch-extraction.md`

## What you will build

A game mode called **Loot to Power** with two variants:

| Variant | Feel |
|---|---|
| Rush | Low threshold, frequent rewards — perks come fast |
| Frenzy | High threshold, rare rewards — each perk feels earned |

Players never accumulate currency or equipment; everything they touch feeds the progression pools.
