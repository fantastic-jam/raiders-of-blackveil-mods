# Changelog

## [Unreleased] - minor

### Added
- Handicaps are now synced to all WMF clients via a dedicated channel: joining players receive the current session snapshot, value changes are broadcast immediately, and departing players are dropped from the snapshot sent to remaining clients.

### Changed
- Implement updated WMF interface — IsClientRequired and SubMenus are now required members
- Declare WMF as a BepInEx dependency for correct load ordering.

## [0.4.0] - 2026-04-06

### Added
- Added menu in ModManager to configure input key

## [0.3.0] - 2026-04-05

### Added
- Handle ModManager Enable method

## [0.2.0] - 2026-04-05

### Added
- Adapt to ModManager
- Save values per player

## [0.1.0] - 2026-04-03

### Added
- init

### Changed
- switch to BepInEx 5
- format & lint

### Fixed
- Packaging now adheres to BepInEx mods standards
