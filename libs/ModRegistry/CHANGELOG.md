# Changelog

## [0.4.0] - 2026-05-03

### Changed
- GameModeVariant and IGameModeProvider moved to WildguardModFramework.GameMode

## [0.3.0] - 2026-04-08

*new*: Add JoinMessage and RunStartMessage to GameModeVariant
  Optional fields for game-mode variants:
  - JoinMessage: shown to clients joining a session running the variant
  (once per launch for modded clients, every join for unmodded clients)
  - RunStartMessage: shown as a HUD corner notification at the first level
  of a run
  Both default to null (no message shown).

*new*: Added GameMode modType

## [0.2.0] - 2026-04-06

*new*: Add IModMenuProvider interface and UIElements ref

## [0.1.0] - 2026-04-05

*chore*: Init
