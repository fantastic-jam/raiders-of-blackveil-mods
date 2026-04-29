# Changelog

## [0.5.0] - 2026-04-29

### Changed
- Implement updated WMF interface — IsClientRequired is now a required member
- Declare WMF as a BepInEx dependency for correct load ordering.

## [0.4.2] - 2026-04-08

### Fixed
- Breaking-change unpatch and HasStateAuthority guards.
  Breaking-change handling:
  - Apply() now returns bool; false means a critical reflection field or
    method was not found
  - Awake() calls UnpatchSelf() and logs a prominent error block when
    Apply() returns false, so players know immediately why the mod is off
  - SetDisabled() added for consistent disabled-state tracking
  Networked write guards:
  - _resetAllCooldown is consumed under HasStateAuthority in
    FixedUpdateNetwork; setting it on a client has no effect
  - AddDamageData writes unconditionally to a NetworkArray; calling it
    on a client skips the server's own display entry
  - Both writes now gated on Object.HasStateAuthority

## [0.4.1] - 2026-04-06

### Added
- Added ModManagerDescription

## [0.4.0] - 2026-04-05

### Added
- Handle ModManager Enable method

## [0.3.0] - 2026-04-05

### Added
- Adapt to ModManager

### Fixed
- use native "Dodged !" message and send to all clients

## [0.2.0] - 2026-04-01

### Changed
- switch to BepInEx 5
- format & lint

## [0.1.1] - 2026-03-31

### Fixed
- Packaging now adheres to BepInEx mods standards

## [0.1.0] - 2026-03-30

### Added
- Localization system
- init
