# Server Browser & Multiplayer Session Architecture

## Overview

The server browser uses a **custom HTTP backend** ("Wombo") for session discovery and metadata, combined with **Photon Fusion** for actual networked gameplay. There is no Steam matchmaking (no `ISteamMatchmaking`, no `SteamMatchmakingServers`).

```
[Host]  → Wombo backend  → registers session
[Client] → Wombo backend → fetches session list
[Client] → Photon Fusion → connects to host by FusionSessionId
```

---

## Session Discovery (Server List)

When the join page opens, `MenuStartJoinPage` calls `RefreshListFromBackend()`. This triggers `JoinablePlaySessionsSync`, which polls the backend every ~4 seconds:

```
MenuStartJoinPage.RefreshListFromBackend()
  → BackendManager.UpdateJoinableSessionsWhenInLobbyCallback()
  → JoinablePlaySessionsSync.PollJoinableSessions()
  → HTTP POST: PlaySession_ListJoinableSessions (HMAC-signed with Blake2)
  → Response: IngressResponsePlaySessionListJoinable
  → Converted to: List<ActivePlaySession>
  → Rendered as: JoinGameButton rows in the UI
```

Client-side filtering is applied at a 5-second throttle (not every poll).

### Session Entry Data (`ActivePlaySession`)

| Field | Type | Notes |
|---|---|---|
| `GameSessionId` | `Guid` | Unique session ID |
| `OwningPlayerName` | `string` | Host display name |
| `SessionTag` | `string` | Human-readable name shown in browser (max 31 chars) |
| `FusionInfo.region` | `string` | Region code (e.g. `"eu"`, `"us"`) |
| `FusionInfo.ping` | `int` | Latency to that region in ms |
| `GameState` | `enum` | `InLobby` (joinable), `InPlaySession`, `Online` |
| `PlayerCount` | `int` | 0–3 |
| `isPasswordProtected` | `bool` | |

### Session States

```csharp
public enum State
{
    InLobby,        // Waiting for players — visible and joinable
    InPlaySession,  // Game in progress — not joinable
    Online          // Post-game — not joinable
}
```

---

## Hosting Flow

```
User fills host form (session name, optional password)
  → BackendManager.BeginPlaySession(sessionTag, password, MultiPlayer)
  → HTTP POST: PlaySession_BeginSession (signed)
  → Response: { play_session.id, fusion.session_id, fusion.region }
  → GetAvailableRegions() → auto-picks lowest-ping region
  → NetworkManager.Launch(GameMode.Host, FusionSessionId)
  → ConfirmPlaySessionStartupRegion() → tells backend chosen region
  → Wait in lobby
```

The `IngressMessagePlaySessionBeginSession` payload is fixed — see [Custom Payloads](#custom-payloads-mod-data-injection) below.

---

## Join Flow

```
User clicks Join (password prompt if needed)
  → BackendManager.JoinPlaySession(sessionId, password)
  → HTTP POST: PlaySession_JoinPlaySession (signed, includes Blake2 nonce)
  → Response: { fusion.session_id, fusion.region }
  → NetworkManager.Launch(GameMode.Client, FusionSessionId)
  → Photon Fusion connects to host using FusionSessionId
```

---

## Host vs Client Role

Set via Photon Fusion's `GameMode` enum in `NetworkManager.Launch()`:

| Role | `GameMode` | `ProvideInput` | Responsibility |
|---|---|---|---|
| Host | `GameMode.Host` | `false` | Runs simulation, chose region, spawns managers |
| Client | `GameMode.Client` | `true` | Sends input, receives simulation state |
| Solo | `GameMode.Single` | — | Local only, no backend |

`NetworkManager.IsServer` → `FusionGameMode == GameMode.Server`

The `StartGameArgs` passed to Fusion:

```csharp
new StartGameArgs {
    GameMode        = FusionGameMode,
    SessionName     = FusionSessionId.ToString(), // GUID from backend
    Scene           = scene,
    SceneManager    = _sceneManager,
    CustomPhotonAppSettings = fusionAppSettings   // region, appId, appVersion
    // No SessionProperties, no CustomProperties
}
```

---

## Filtering

`MenuStartJoinPage.FilterData` applies client-side:

| Filter | How |
|---|---|
| Name search | Matches `SessionTag` or `OwningPlayerName` (case-insensitive) |
| Region | `"*"` for any, or one of 15 region codes |
| Private only | Toggle to show only password-protected lobbies |

### Available Regions

`"*"` (any), `"asia"`, `"au"`, `"cae"`, `"cn"`, `"eu"`, `"hk"`, `"in"`, `"jp"`, `"za"`, `"sa"`, `"kr"`, `"tr"`, `"uae"`, `"us"`, `"usw"`, `"ussc"`

Auto-selection picks the lowest-ping region from `BackendManager.WomboPlayerRegionPings` and caches it in `PlayerSettings.Gen_LastJoinRegion`.

---

## Custom Payloads / Mod Data Injection

**There is no supported mechanism to attach custom data to a session at creation time.**

### Why

`IngressMessagePlaySessionBeginSession` has a closed, fixed schema:

```csharp
public class Data
{
    public Fusion.GameMode networking_mode;
    public BackendManager.PlaySessionMode session_mode;
    public string session_tag;           // max 31 chars
    public string session_password;      // max 20 chars
    public IngressGameSettings game_settings;
    public IngressGameMode game_mode;
    public IngressPlayerInfo[] players;
    // No custom fields, no metadata dict
}
```

Fusion's `SessionProperties` (`Dictionary<string, SessionProperty>`) is also **never passed** in `StartGameArgs`. The game only sends `SessionName` (a GUID) to Fusion.

### What You Can Hook (and their limits)

| Hook point | What you can do | Limitation |
|---|---|---|
| Prefix on `BackendManager.StartPlaySession()` | Modify the request object before JSON serialization | Wombo backend ignores unknown fields |
| `session_tag` (31 chars) | Encode a tiny bit-packed payload | Overwrites the visible lobby name |
| Postfix on `NetworkManager.OnNetworkLaunched()` | Read `runner.SessionInfo.Name` | Read-only, just the Fusion GUID |
| Fusion RPC / Networked properties | Share mod state with all clients | Only works **after** join — not visible in server browser |

### Practical Approach for Mods

**At runtime (post-join):** Use Fusion RPCs or `[Networked]` properties to sync mod state between host and clients once they're connected. This is the only clean path.

**At discovery time (server browser):** No reliable mechanism. The `session_tag` field (31 chars) could encode a tiny bitmask of active mods, but it overwrites the lobby name visible to all players.

**Requiring backend changes:** Would need the Wombo backend to store and return custom metadata per session — not feasible for mods.

---

## Key Source Files

| File | Role |
|---|---|
| `game-src/RR/BackendManager.cs` | Central hub — join/host/session lifecycle |
| `game-src/RR.UI.Pages/MenuStartJoinPage.cs` | Server browser UI + filter logic |
| `game-src/RR.UI.Pages/MenuStartHostPage.cs` | Host creation UI |
| `game-src/RR/NetworkManager.cs` | Photon Fusion init, GameMode selection |
| `game-src/RR.Backend.API.V1/ActivePlaySession.cs` | Session data model |
| `game-src/RR.Backend.API.V1/JoinablePlaySessionsSync.cs` | Backend polling (4s interval) |
| `game-src/RR.Backend.API.V1.Ingress.Message/IngressMessagePlaySessionBeginSession.cs` | Host creation request schema |
| `game-src/RR.Backend.API.V1.Ingress.Message/IngressResponsePlaySessionListJoinable.cs` | Session list response schema |
| `game-src/RR.Backend.Integration.FusionIntegration/FusionRegionInfo.cs` | Region definitions |
| `game-src/RR.UI.Controls.Menu.JoinHost/JoinGameButton.cs` | Per-session UI row |
