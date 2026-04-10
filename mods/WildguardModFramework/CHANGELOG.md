# Changelog

## [Unreleased]

## [0.4.0] - 2026-04-08

*new*: Add WildguardModFramework mod
  Successor to ModManager. Handles mod/cheat/game-mode registration,
  session configuration, in-game menus, and the client-requirement
  network handshake. Extracts patch logic into Controller/Orchestrator/
  Injector/Protocol classes per §11 conventions.
  Also registers WMF and RogueRun in the solution file.

*chore*: switch to BepInEx 5

*chore*: format & lint

*fix*: Packaging now adheres to BepInEx mods standards
