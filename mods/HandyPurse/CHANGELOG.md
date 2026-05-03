# Changelog

## [0.7.1] - 2026-05-03

### Fixed
- topup entries that cannot be restored due to an inventory layout change are now deposited to the bank instead of being silently lost
- bank.json and topup files now written atomically — no data loss if the game crashes during save
- bank balance no longer doubles when joining as client multiple times in the same session

## [0.7.0] - 2026-04-30

### Added
- Dropping managed currency above the vanilla stack size now splits into multiple vanilla-sized floor pickups

### Fixed
- Stash items from previous champion sessions did not receive elevated caps — stash items are now matched by reference against the local player's item list
- Other players' currency stacks were elevated to HandyPurse caps — elevated limits now apply to the local player's items only
- Currency stacks lost amounts between saves in the same session — live amounts are now restored immediately after the cloud save completes

## [0.6.0] - 2026-04-29

### Added
- All in-game text is now localised — French translation included
- Currency clamp now applies at local save time, ensuring excess is always captured before the cloud save runs
- Safeguard: if a topup cannot be fully restored to inventory, the shortfall is automatically deposited to the bank instead of being lost

### Fixed
- Excess currencies above vanilla caps were silently lost when rejoining a session — topup was never restored on load
- Excess currencies were not banked on save when local save ran before the cloud save hook
- Bank menu status message persisted incorrectly after closing and reopening the mod overlay

## [0.5.0] - 2026-04-29

### Changed
- Implement updated WMF interface — IsClientRequired and SubMenus are now required members

## [0.4.0] - 2026-04-21

### Added
- Bank menu section: shows bank balance and pending topup; Drop bank to floor button spawns banked currency as pickups (lobby/host only)
- Bank system: excess currency above vanilla caps is stripped at save time and restored to the exact same inventory slot on next load; layout mismatch sends excess to a local bank file for manual recovery

### Changed
- Declare WMF as a BepInEx dependency for correct load ordering.

### Fixed
- Correct vanilla stack limits in uninstall description (Scrap 3,000 · Black Coin 200 · Crystals 200)

## [0.3.2] - 2026-04-08

### Added
- Added uninstall method in modmanager menu

## [0.3.1] - 2026-04-06

### Added
- Added ModManagerDescription

## [0.3.0] - 2026-04-05

### Fixed
- Disabled HandyPurse would clamp stacks at vanilla limits

## [0.2.0] - 2026-04-05

### Added
- Adapt to ModManager

### Fixed
- Items merge correctly according to caps

## [0.1.0] - 2026-04-01

### Changed
- switch to BepInEx 5
- format & lint

## [0.0.2] - 2026-03-31

### Fixed
- Packaging now adheres to BepInEx mods standards

## [0.0.1] - 2026-03-29
