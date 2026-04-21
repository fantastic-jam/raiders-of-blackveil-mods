# Changelog

## [Unreleased] - minor

### Added
- localise all WMF UI strings via TranslationService; add en + fr translation files
- Translation system: mods can call TranslationService.For() to load flat JSON translation files from their Assets/Localization/ folder, with automatic fallback to English and support for third-party override files
- `WmfNetwork` now exposes three session lifecycle events for mods to subscribe to:
  - `OnPlayerJoined(PlayerRef, bool isModded)` — fires on the host immediately when a player joins; `isModded` is true if they connected with a WMF token.
  - `OnPlayerConfirmed(PlayerRef, bool isModded)` — fires on the host when a player is ready to receive `WmfNetwork` messages: immediately on join for non-client-required scenarios, or after the handshake ACK for client-required game modes.
  - `OnPlayerLeft(PlayerRef)` — fires on all machines when a player leaves the session.

### Fixed
- `ModRegistry.dll` is now listed under `plugin_dlls` instead of `patchers` in `metadata.json`, correcting its deploy and package destination to `BepInEx/plugins`.

## [0.4.0] - 2026-04-08

### Added
- Add WildguardModFramework mod: successor to ModManager. Handles mod/cheat/game-mode registration,
  session configuration, in-game menus, and the client-requirement network handshake. Extracts patch
  logic into Controller/Orchestrator/Injector/Protocol classes per §11 conventions.
  Also registers WMF and RogueRun in the solution file.

### Changed
- switch to BepInEx 5
- format & lint

### Fixed
- Packaging now adheres to BepInEx mods standards
