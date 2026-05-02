# SpectateMode — Implementation Plan

## Goal

Allow a player to connect to a session that is already in a run, and join the
game cleanly at the **start of the next level**. Until then, the joiner sits in
a "pre-joined" waiting state — no champion, no entry in the active player list,
no presence in the networked world.

The first room the joiner actually plays should look exactly like the first
room of a normal run: full champion init, normal HUD, normal control hand-off,
including the few seconds of "can't move yet" that the lobby-to-room-1
transition naturally produces.

---

## Design

### Two-phase join

| Phase | Duration | What the joiner is | What everyone else sees |
|---|---|---|---|
| **Pre-join** | From connection until the start of the next room load | A connected `NetworkRunner` peer with no `Player` registered, no champion spawned | The session player count is bumped (host advertises N + pre-joiners). No new champion, no UI noise. |
| **Real join** | At `GameManager.NextLevel` (after the previous room's level-exit gate has cleared) | Goes through the same setup the lobby does before a run start: champion spawn → input enable → HUD | A new player appears at the start of the next room as if they had been there from the lobby. |

### Why `GameManager.NextLevel` and not `OutroManager.Activate`

The natural-looking hook is `OutroManager.Activate(LevelWin)` — "previous room
ended". That hook fires too early. The vanilla level-transition gate at
[`DungeonManager.RPC_ObjectsCleared`](../../game-src/RR.Level/DungeonManager.cs)
waits for `_closedLevelCount >= PlayerManager.Instance.GetPlayers().Count`
before scheduling `LevelExit`. If we `runner.Spawn` the pre-joiner's `Player`
during that window, the new player joins `_activePlayers` mid-cleanup, the
gate's threshold goes up by one, and the new client — half-initialized,
mid-Fusion-spawn — never sends `RPC_ObjectsCleared`. The host then waits
forever, the next room never loads, and every connected client (host
included) gets stuck on a black scene-clear screen.

`GameManager.NextLevel` runs after the gate has already cleared and after
`PlayerManager.SendAllPlayersGameStatesToBackend` has resolved its callback,
so adding a new `Player` to `_activePlayers` at that point cannot stall the
transition. The body of `NextLevel` then calls
`LevelLoadingHandler.RPC_StartSceneLoad`, dragging the freshly-spawned
`Player` into the new scene through Fusion's `DontDestroyOnLoad` machinery.

### Why this shape

The vanilla "spawn the player immediately and patch around the consequences"
approach forces us to patch dozens of systems that legitimately assume every
spawned `Player` has a champion, a backend game-state, and a slot in
`_activePlayers`. The previous iteration of this mod went down that path and
ended up with ~17 patches just to suppress NPEs.

By **deferring the actual `Player` registration** until a clean entry point
(scene pre-load between rooms), all of those systems remain in their natural
state. Pre-joiners are not in the world model at all — they are sitting on a
network connection with no behaviour attached.

---

## Required patches (minimal)

The patch surface is small. Everything else lives in `SpectateModeManager`.
All bodies are one-liners that delegate to `SpectateModeManager`.

| Method | Type | Purpose |
|---|---|---|
| `BackendManager.PlaySessionBeginRun` | Prefix | **Block.** Don't tell the backend the run started — the session must remain joinable in the server browser. |
| `MetricsManager.SendPlaySessionUpdateEvent` (`event_type == LobbyEnd`) | Prefix | Block, same reason. |
| `NetworkManager.OnConnectRequest` | Prefix | Vanilla refuses connections during `IsInActiveRun`. Replace with: accept iff `GetPlayers().Count + PreJoinerCount < 3`. The pre-joiner count is added **only here**, not on the `PlayerCount` getter (see below). |
| `PlayerManager.OnPlayerJoined` | Prefix | If `IsInActiveRun`: register the `PlayerRef` in `SpectateModeManager.PreJoiners` and **skip** the vanilla `runner.Spawn(PlayerPrefab, …)` — nothing else needs blocking, because the entire chain (`Player.Spawned → AddPlayer`, `AfterSpawned → RPC_PlayerJoinedPlaySession`, champion spawn, progression registration) is gated on the Player object existing. |
| `PlayerManager.OnPlayerLeft` | Prefix | If the leaver is a pre-joiner: drop from list, skip vanilla. No `Player` was ever spawned, so there is nothing to despawn. |
| `GameManager.NextLevel` | Prefix | Server-only. Called by `DungeonManager.ProceedWithLevelExit` only for `LevelWin` / `CheatFinish`, and only after the previous room's exit cutscene + every existing client's `RPC_ObjectsCleared` has cleared the level-exit gate (`_closedLevelCount >= GetPlayers().Count`). Spawn each pre-joiner's `Player` prefab via `runner.Spawn(PlayerPrefab, Vector3.zero, Quaternion.identity, playerRef)` here, then clear the list. The vanilla flow takes over from there — `Player.Spawned → AddPlayer`, `AfterSpawned → RPC_PlayerJoinedPlaySession → champion spawn`, the body of `NextLevel` then issues `LevelLoadingHandler.RPC_StartSceneLoad`, scene transition (Player + champion are `DontDestroyOnLoad`), `IntroManager.RPC_IntroActivation → InitPlayerCharacterAtSpawnPoint`, `Handle_LevelEvent_IntroFinished` enables input. The prefix skips when `LevelProgressionHandler.NextToFinish` is true (NextLevel is taking the run-end branch, no new room to spawn into). |

That is the entire patch surface. **6 prefixes/postfixes, all one-liners.**

### Why `PlayerCount` is *not* patched

An earlier draft postfixed `PlayerManager.PlayerCount` to add `PreJoinerCount`.
That broke real gameplay: every system that reads it for in-world scaling
(`DoorManager.CheckVotes` waiting for votes that can never arrive,
`DifficultyManager.PlayerFactor`, `EnemySpawnManager` enemy budget,
`PerkHandler` cadence, `Health` revive thresholds, …) saw a count higher
than the number of players that can actually act. **Pre-joiners are not in
the world; they must not influence anything that happens in it.**

The capacity gate is the only legitimate place a pre-joiner counts as a
"player": the host already has a Fusion connection for them and must not
accept a fourth peer. That gate lives in `OnConnectRequest`, where the count
is computed inline — `GetPlayers().Count + PreJoinerCount`. Vanilla
`OnPlayerJoined`'s own `PlayerCount < 3` check never triggers during a
mid-run join because our prefix returns `false` and skips the vanilla body.

The lobby browser's slot count stays accurate without any `PlayerCount`
patch. `SpectateModeManager.SendBackendCountUpdate` calls
`MetricsManager.SendPlaySessionUpdateEvent` directly when a pre-joiner is
added or dropped, with `player_count = GetPlayers().Count + PreJoinerCount`
and `player_id = Guid.Empty.ToString()` (the real UUID is unknown until
promotion, when the vanilla `RPC_PlayerJoinedPlaySession` fires the canonical
event with the real ID).

### Why these and not more

Every other side-effect of "no Player exists yet" is automatically a no-op:
- `_activePlayers` does not contain them — every iteration that touches
  champion state skips them.
- `GetPlayer(PlayerRef)` returns null — but no game system calls it for a
  PlayerRef that was never registered, because no RPC carries that PlayerRef.
- HUD / nameplates / vote counts / enemy AI / `PlayersLeft` — all read
  `_activePlayers`, all naturally exclude pre-joiners.

This is the structural difference from the previous "stage and patch around"
design: a pre-joiner is **not in the world model at all**, instead of being
in it with a special flag.

---

## SpectateModeManager (new helper)

Lives next to `SpectateModeMod`. Single responsibility: own the pre-join list
and trigger the deferred Player spawn.

```
SpectateModeManager (server-authoritative — pre-join list only exists on host)
├── State
│   └── PreJoiners : HashSet<PlayerRef>
├── Lifecycle
│   ├── BeginPreJoin(PlayerRef)                     // from OnPlayerJoined prefix
│   ├── CancelPreJoin(PlayerRef)                    // from OnPlayerLeft prefix
│   └── PromoteAll(NetworkRunner)                   // from OutroManager.Activate postfix (LevelWin)
└── (no PromoteOne method)
```

There is **no** `PromoteOne` method. Promotion is just:

```csharp
foreach (var playerRef in PreJoiners) {
    runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, playerRef);
}
PreJoiners.Clear();
```

That is the only thing the manager does at promotion time. The vanilla chain
that runs from `runner.Spawn(PlayerPrefab, ...)` is exactly the lobby's
new-player path — `Player.Spawned → AddPlayer`, `AfterSpawned →
RPC_PlayerJoinedPlaySession`, champion spawn, progression registration,
scene transition (DDOL), `IntroManager.RPC_IntroActivation`,
`InitPlayerCharacterAtSpawnPoint`, `Handle_LevelEvent_IntroFinished`. The few
seconds of "can't move yet" the user noted in the lobby are the same few
seconds between champion spawn and intro-finished — they happen for the late
joiner identically because they go through the same code path.

`PlayerPrefab` is a public field on `PlayerManager.Instance` (used by the
vanilla `OnPlayerJoined` for the same `runner.Spawn` call) — we read it from
there rather than capturing our own reference.

---

## Player count

Three distinct counts, with deliberately different semantics:

| Count | Source | Includes pre-joiners? |
|---|---|---|
| **Capacity** (host accepts/refuses an incoming peer) | `OnConnectRequestPrefix` computes `GetPlayers().Count + PreJoinerCount` inline | **Yes** — pre-joiners hold a Fusion connection slot. |
| **Backend / lobby browser** (`MetricsManager.SendPlaySessionUpdateEvent`) | `SpectateModeManager.SendBackendCountUpdate` fires `PlayerJoinedSession` / `PlayerLeftSession` on pre-join/cancel with `count = GetPlayers().Count + PreJoinerCount` | **Yes** — the browser shows the real number of connected peers. |
| **Gameplay** (vote count, difficulty scaling, enemy budget, perk cadence, etc.) | `PlayerManager.PlayerCount` getter, unpatched | **No** — pre-joiners cannot vote, take damage, or otherwise act. |

If a pre-joiner disconnects before promotion, `CancelPreJoin` drops them
from the list and fires `PlayerLeftSession` so the browser count drops back.
No champion teardown, no `_activePlayers` mutation — they were never
registered.

---

## Cross-client visibility of pre-joiners

The pre-joiner is a Fusion peer with no `Player` object. Other clients should
**not** see them in any in-game UI (player list, HUD nameplates, vote counts).
Because no `Player` is spawned, this is automatic — the only thing that
changes is the server-browser slot count, plus the optional chat
notification described next.

---

## Pre-join chat notification

When `TryBeginPreJoin` succeeds on the host, the manager calls
`WmfChatBridge.HostNotify(...)` to post a system message in the WMF chat
overlay using the sender tag `<server>`:

> `[<server>] A new player is joining. They will spawn in at the start of the next room.`

The bridge is reflection-only (no project reference to WMF, no
`BepInDependency`), so SpectateMode keeps loading and working when WMF is
absent — the call simply becomes a no-op.

When WMF is loaded, the bridge resolves three handles at startup:

| Reflection target | Used for |
|---|---|
| `WildguardModFramework.Network.WmfNetwork.Send(PlayerRef, string, byte[])` | Send framed `wmf.chat` payload to a specific remote ref. |
| `WildguardModFramework.Chat.ServerChat.ReceiveMessage(string, string)` | Display the message locally on the host (Fusion does not loop reliable-data sends back to the sender). |
| `WildguardModFramework.Network.GameModeProtocol.ConfirmedPlayers` (HashSet&lt;PlayerRef&gt;) | Iterate post-handshake modded clients to send to. |

The payload is framed exactly as `WildguardModFramework.Chat.ServerChatNetwork.Encode`:
`[1 byte: sender length][sender UTF-8][text UTF-8]`. Remote modded clients
receive it through the existing `ServerChatNetwork.OnChatReceive` handler,
which routes it to `ServerChat.ReceiveMessage` for display.

To avoid the host's own copy triggering `ServerChatNetwork.OnChatReceive`'s
"server echoes received chat to all remotes" branch (which would cause every
remote to see the message twice), the bridge mirrors `ServerChat.HostBroadcast`'s
pattern: skip the local ref when sending, and dispatch the local copy directly
through `ServerChat.ReceiveMessage`.

Vanilla (non-WMF) clients receive nothing — they simply notice the new
player at the start of the next room.

---

## Out of scope (for v1)

- **Spectator camera while pre-joining.** The joiner sees a black/load screen
  until the next room. No live spectate view, no free-cam.
- **XP / perk averaging on promotion.** Vanilla lobby behaviour gives a fresh
  champion. If averaging is desired we add it later — explicitly, not as a
  side-effect.

> **Never in scope.** No mid-room revive, no mid-fight join, no pre-joiner
> visibility in the world. A pre-joiner waits, and the next room starts
> exactly as the lobby starts a run. Anything that would inject the joiner
> into the active room belongs in a different mod.

---

## Future improvements

- **Champion selection while waiting.** Show the lobby champion-select UI to
  the pre-joiner so they can pick before promotion. Their choice is held by
  `SpectateModeManager` and applied on the next-room spawn.
- **Notification UI for non-WMF clients.** Today the pre-join notification is
  WMF-only. A small in-game toast (e.g. via `CornerNotificationContainer`)
  would let vanilla clients also see "a player is joining" without requiring
  WMF.
- **Notify unmodded joiners to install SpectateMode.** When a pre-joiner has
  WMF but no SpectateMode (detected by absence of the `"spectatemode:present"`
  handshake message), the host should send them a WMF chat message explaining
  that they need to install SpectateMode to get the full shrine experience.
  Currently they silently receive perk-choice pickups instead of shrines with
  no explanation.
