# Mod best practices

Rules derived from code review of RogueRun, PerfectDodge, WildguardModFramework, and ThePit. Every rule has a live codebase example.

The five most-violated rules are in `CLAUDE.md § Rules` and enforced on every task.

## Pattern docs (start here)

| Topic | File |
|---|---|
| Harmony patching mechanics — reflection handles, `Apply()`, visibility, naming, coroutines, re-entrant guards | [`docs/dev/patterns/harmony-patching.md`](patterns/harmony-patching.md) |
| Patch extraction — one-liner rule, Controllers, Orchestrators, Protocols | [`docs/dev/patterns/patch-extraction.md`](patterns/patch-extraction.md) |
| Proxify pattern — `ConditionalWeakTable`, `PvpActorColliderDetector`, lifecycle safety | [`docs/dev/patterns/proxify.md`](patterns/proxify.md) |
| Networking — `IsServer` guards, Fusion host mode, `PlayerManager` null-safety | [`docs/dev/patterns/networking.md`](patterns/networking.md) |
| State management, `IModRegistrant`, `libs/` boundary | [`docs/dev/patterns/state.md`](patterns/state.md) |

## ThePit

| Topic | File |
|---|---|
| Ability PvP coverage — pattern decision table, champion matrix | [`docs/dev/ThePit/abilities.md`](ThePit/abilities.md) |

## Other reference docs

| Topic | File |
|---|---|
| Base game classes, fusion networking, save/inventory, run structure | [`docs/dev/base_game/`](base_game/) |
| WildguardModFramework handshake protocol | [`docs/dev/WildguardModFramework/handshake.md`](WildguardModFramework/handshake.md) |
| Creating a new mod | [`docs/dev/create_mod.md`](create_mod.md) |
