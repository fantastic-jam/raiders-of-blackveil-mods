# Changelog

## [0.3.0] - 2026-05-03

### Changed
- update WildguardModFramework namespace import for relocated game mode types

## [0.2.0] - 2026-04-29

### Added
- All UI strings (overlay title, stepper labels, buttons, variant names, mod description) are now loaded from translation files — English and French are included out of the box
- Game version check on startup — if the running game build does not match the tested version,
  ThePit registers as a plain `Mod` instead of `GameMode`, hiding it from the session picker
  and logging a warning rather than patching an unknown game.

### Changed
- Declare WMF as a BepInEx dependency for correct load ordering.

## [0.1.1] - 2026-04-12

### Changed
- Arena grace period reduced from 10 s to 8 s; respawn invincibility reduced from 10 s to 3 s
- Players respawn at full HP
- Beatrice attacks now deal damage to opponents
- Beatrice projectiles now pass through her own aura
- Jamera no longer chains ultimates after a kill
- Root damage now stops when the victim respawns

## [0.1.0] - 2026-04-12

### Added
- Initial release — Perk Draft PvP arena mode for Raiders of Blackveil.
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
