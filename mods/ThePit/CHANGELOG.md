# Changelog

## [Unreleased]

- minor: Initial release — Perk Draft PvP arena mode for Raiders of Blackveil
  Only the host needs the mod installed.

  Perk Draft match:
  - Players enter the Slash & Bash arena with no gear
  - A series of perk chest rounds (6 by default) play out in the ante-room
    before the door opens, so everyone arrives with a starting build
  - 10-second grace period after the door opens before combat begins
  - Perks drop automatically near each player throughout the match;
    rarity escalates as the clock runs down (Common → Epic/Legendary)
  - XP accumulates passively; players start at level 5 so all abilities
    are available from the first second (max 20 perks, max level 20)
  - Match ends when the timer reaches zero; most kills wins

  Respawn system:
  - Death is not elimination — players respawn after 3 seconds
  - 10 seconds of invincibility, invisibility, and a speed boost on respawn

  Host configuration overlay (planning table in lobby):
  - Match duration (5 – 20 minutes)
  - Perk and XP drop rate
  - Initial perk chest rounds (0 – 12)
  - Starting XP level (1 – 20)
  - Damage reduction (softens PvP damage at higher XP levels)
  - Settings saved per-host and reloaded each session

  PvP ability support — champion abilities reworked to target opposing players:
  - Blaze, Rhino, Beatrice, and Shameleon fully supported
