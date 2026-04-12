# Changelog

## [Unreleased]
- patch: Arena grace period reduced from 10 s to 8 s; respawn invincibility reduced from 10 s to 3 s
- patch: Players respawn at full HP
- patch: Beatrice attacks now deal damage to opponents
- patch: Beatrice projectiles now pass through her own aura
- patch: Jamera no longer chains ultimates after a kill
- patch: Root damage now stops when the victim respawns

## [0.1.0] - 2026-04-12

- minor: Initial release — Perk Draft PvP arena mode for Raiders of Blackveil
  Only the host needs the mod installed.

  Perk Draft match:
  - Players enter the Slash & Bash arena
  - A series of perk chest rounds (6 by default) play out in the ante-room
    before the door opens, so everyone arrives with a starting build
  - 10-second grace period after the door opens before combat begins
  - Perks are granted to each player throughout the match;
    rarity escalates as the clock runs down (Common → Epic/Legendary)
  - XP accumulates passively; players start at level 5 so all abilities
    are available from the first second (up to 30 perks total across chest rounds and drip)
  - Match ends when the timer reaches zero; most kills wins

  Respawn system:
  - Death is not elimination — players respawn after 3 seconds
  - 10 seconds of invincibility and a speed boost on respawn

  Host configuration overlay (planning table in lobby):
  - Match duration (2 – 20 minutes)
  - Perk and XP drop rate
  - Initial perk chest rounds (0 – 12)
  - Starting XP level (1 – 20)
  - Damage reduction (softens PvP damage at higher XP levels)
  - Settings saved per-host and reloaded each session

  PvP ability support — champion abilities reworked to target opposing players:
  - Blaze, Rhino, Beatrice, and Shameleon fully supported
