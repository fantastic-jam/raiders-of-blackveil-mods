# Changelog

## [Unreleased] - patch

### Fixed
- Items of the same ItemType but different AssetID (e.g. hex doll and pig stew) could be merged into the same stack by dragging or double-clicking — CanMergeItem now requires matching AssetID
- DoorVoteFix: re-evaluate door vote when a player disconnects mid-vote — fixes vote getting stuck when a non-slot-0 player leaves
- BarrierSelfGrantFix: re-apply self-and-nearest-ally perk effects to caster — fixes effects only landing on allies in multiplayer

## [0.0.1] - 2026-04-29

### Fixed
- Stackable items auto-stack when transferred from champion inventory to stash
- Hosted session disappears from the server list after returning from a run
