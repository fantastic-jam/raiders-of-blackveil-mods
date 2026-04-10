# WMF Client Handshake

## Purpose

When a session has an `IsClientRequired` game mode active, WMF enforces that joining clients have the mod enabled. Since Photon Fusion provides no per-client kick mechanism and no custom join metadata, enforcement happens post-join via a two-RPC sequence.

## Constraints

- No `SessionProperties` in `StartGameArgs` — Fusion session carries no custom data
- No per-client kick RPC exists in the game
- `RpcInfo` is Fusion-managed — cannot carry custom payload
- `RPC_Close_Lobby` does not disconnect clients from Fusion; it tears down local lobby state (`LobbyManager.OnSceneUnload`)

The session tag suffix (`[DisplayName]`, max 31 chars) is the only pre-join signal. It provides a soft gate in `JoinPlaySessionPrefix` but is client-side only and bypassable.

## Handshake Sequence

Triggered from `OnPlayerJoinedPostfix` on the host whenever a player joins a session with an `IsClientRequired` game mode:

```
Host
  → RPC_ErrorMessageAll("[modmanager:{variantId}] ...")   // carries variant ID
  → RPC_Close_Lobby()                                      // triggers lobby teardown
```

Both RPCs are reliable and on the same `GameManager` NetworkBehaviour — Fusion guarantees delivery in order.

## Message Format

```
[modmanager:{variantId}] Human-readable message
```

`variantId` is `RegisteredGameMode.VariantId`:
- Single-variant mod: equals the plugin GUID (e.g. `fantastic-jam-roguerun`)
- Multi-variant mod: `pluginGuid::variantId` (e.g. `fantastic-jam-roguerun::hard`)

## Receiver Behaviour

### Modded client (WMF installed, matching game mode registered)

Harmony prefix on `RPC_ErrorMessageAll`:
1. Detects `[modmanager:]` prefix, extracts `variantId`
2. Looks up `RegisteredGameMode` by `VariantId`
3. If not the active variant → calls `mode.Enable()` (handles `mod.Enable()` + `EnableVariant()` internally) and updates `SelectedGameModeVariantId`
4. Sets `_suppressNextClose = true`
5. Returns `false` — suppresses fatal error popup and `EnableControl(false)`

Harmony prefix on `RPC_Close_Lobby`:
1. If `_suppressNextClose` → clears flag, returns `false` (suppresses `OnSceneUnload`)
2. Otherwise → passes through

### Unmodded client (mod not installed or WMF absent)

- `RPC_ErrorMessageAll` runs normally: player control disabled, fatal error popup shown
- `RPC_Close_Lobby` runs normally: `LobbyManager.OnSceneUnload()` tears down lobby state

The client ends up with no control and a broken lobby state, effectively soft-kicked.

### Host

The host is in the "All" target and receives both RPCs locally. Since it selected the variant, `SelectedGameModeVariantId == variantId` — `Enable()` is skipped (noop), flag is set, both RPCs are suppressed. The host stays in the lobby.

## Noop on Repeat Joins

`OnPlayerJoinedPostfix` fires for every player that joins (including subsequent players). The prefix skips `mode.Enable()` if the variant is already active, so repeated handshakes are idempotent.

## Limitations

- **Lobby only**: `RPC_Close_Lobby` only tears down lobby state. In-game, `RPC_ErrorMessageAll` disables player control permanently — a stronger enforcement.
- **Soft enforcement**: the pre-join `JoinPlaySessionPrefix` check (session tag) is the first gate. The handshake handles cases where that check was bypassed or the client joined before the mode was selected.
- **No true kick**: there is no mechanism to forcibly disconnect a specific Fusion client. A determined unmodded client could reconnect.
