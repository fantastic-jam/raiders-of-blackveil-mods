# Changelog

## [0.0.4] - 2026-10-19

### Fixed
- Door and biome vote screens no longer break while a modded player is spectating mid-run
- Raid members reconnecting mid-run keep their own XP, perks and health (restored by the game) instead of being treated as new joiners
- Compatibility with the October 2026 game update

## [0.0.3] - 2026-05-21

### Fixed
- Fix compile errors for Ghoulag Update: NetworkChampionBase.XP struct is now private — use XPAmount (read), SetXP (write)

## [0.0.2] - 2026-05-08

### Fixed
- Ability points now correctly assigned when averaging XP from existing players

## [0.0.1] - 2026-05-03

### Added
- Initial release: mid-run joining with XP and rarity-matched perk averaging
