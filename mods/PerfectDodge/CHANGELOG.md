# Changelog

## [Unreleased]

## [0.4.2] - 2026-04-08

*fix*: Breaking-change unpatch and HasStateAuthority guards
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

*new*: Added ModManagerDescription

## [0.4.0] - 2026-04-05

*new*: Handle ModManager Enable method

## [0.3.0] - 2026-04-05

*new*: Adapt to ModManager
*fix*: use native "Dodged !" message and send to all clients

## [0.2.0] - 2026-04-01

*chore*: switch to BepInEx 5
*chore*: format & lint

## [0.1.1] - 2026-03-31

- *fix* Packaging now adheres to BepInEx mods standards

## [0.1.0] - 2026-03-30

- PerfectDodge: Localization system
- PerfectDodge: init
