# Changelog

## [Unreleased]

## [0.2.3] - 2026-04-08

*fix*: Implement Disable() and guard menu button injection
  Disable() was a no-op — when ModManager disabled OfflineMode at
  startup the Offline Mode button still appeared in the main menu.
  - Add _disabled flag and SetDisabled() in OfflineModePatch
  - Disable() now calls SetDisabled() and logs the state change
  - MenuStartPageOnInitPostfix skips the button injection when disabled

## [0.2.2] - 2026-04-06

*new*: Changed button placement

## [0.2.1] - 2026-04-05

*fix*: fixed error on online play

## [0.2.0] - 2026-04-05

*fix*: Fixed black screen on Host and Join server

## [0.1.0] - 2026-04-05

*chore*: Init
*chore*: switch to BepInEx 5
*chore*: format & lint
*fix*: Packaging now adheres to BepInEx mods standards
