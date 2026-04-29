# Changelog

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
