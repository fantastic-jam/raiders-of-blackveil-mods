# Changelog

## [0.7.0] - 2026-10-19

### Added
- The player list key (F2 by default) can be changed in Mods → WMF → Players

### Fixed
- Game no longer freezes when the host sends an emote or other mod message that is relayed to all players

## [0.6.2] - 2026-10-19

### Fixed
- Option labels in the Mods menu (chat toggle, game mode steppers) show their text again instead of "--"
- Only the player without the required mods sees the explanation popup; it can no longer close the game for the host
- Mod handshake and mod data channel work with the updated network layer
- Mods button in the main menu is reachable with keyboard/gamepad again
- Compatibility with the October 2026 game update (main menu, pause menu, HUD and network changes)

## [0.6.1] - 2026-05-21

### Fixed
- Updated for compatibility with Ghoulag Update

## [0.6.0] - 2026-05-03

### Added
- API.md reference doc for mod authors
- Game mode types moved from ModRegistry: GameModeVariant and IGameModeProvider now live in WildguardModFramework.GameMode
- Notifications API: WmfNotifications.Show() for displaying in-game toast notifications with configurable level (Info, Warning, Error)

## [0.5.0] - 2026-04-29

### Added
- Player management overlay — hold F2 to see all connected players; hosts can kick and ban players; manage the ban list in WMF → Players
- Server chat — in-game text chat between all players; toggle with the chat key; configure visibility and history in WMF → Chat
- Mods menu left bar supports expandable accordion sub-sections via the new SubMenus property on IModMenuProvider
- localise all WMF UI strings via TranslationService; add en + fr translation files
- Translation system: mods can call TranslationService.For() to load flat JSON translation files from their Assets/Localization/ folder, with automatic fallback to English and support for third-party override files
- `WmfNetwork` now exposes three session lifecycle events for mods to subscribe to:
  - `OnPlayerJoined(PlayerRef, bool isModded)` — fires on the host immediately when a player joins; `isModded` is true if they connected with a WMF token.
  - `OnPlayerConfirmed(PlayerRef, bool isModded)` — fires on the host when a player is ready to receive `WmfNetwork` messages: immediately on join for non-client-required scenarios, or after the handshake ACK for client-required game modes.
  - `OnPlayerLeft(PlayerRef)` — fires on all machines when a player leaves the session.

### Changed
- IModRegistrant and IModMenuProvider: remove default implementations — all interface members are now required

### Fixed
- Host player name not visible to clients — host now re-broadcasts its username when each client joins
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
