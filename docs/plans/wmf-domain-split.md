# Plan: WMF domain split

## Goal

Reorganize `mods/WMF/` from a flat structure into clearly separated domain
folders — without splitting the `.csproj`. Each domain owns its concern; no domain
reaches into another's internals.

This also lays the groundwork for the future `WMFNetwork` channel multiplexer
(the network domain is scaffolded here and the multiplexer is implemented as part of
this plan).

---

## New folder structure

```
mods/WMF/
├── WMFMod.cs               root — plugin entry point, calls all Apply()
├── WMFConfig.cs            root — cross-cutting config, no domain
├── Registry/
│   ├── RegisteredMod.cs           data class
│   ├── RegisteredGameMode.cs      data class
│   └── ModScanner.cs              discovery + shared lists (renamed from WMFRegistrants)
├── Lifecycle/
│   └── ModLifecycle.cs            ApplyStartupDisables (thin, reads Registry)
├── ModMenu/
│   ├── HostStartPagePatch.cs      host-page UI (split from WMFPatch)
│   ├── MenuStartPagePatch.cs      solo start page patch
│   ├── MenuPausePagePatch.cs      pause menu patch
│   ├── ModsMenuOverlay.cs         mods overlay
│   └── SoloStartPage.cs          solo modal
└── Network/
    ├── NetworkPatch.cs            handshake + OnReliableDataReceived router (split from WMFPatch)
    └── WMFNetwork.cs     channel-102 pub/sub multiplexer (new)
```

`Patch/SoloGameModeOverlay.cs` — deleted (already a superseded tombstone).

---

## Namespace map

| Folder       | Namespace                 |
|--------------|---------------------------|
| root         | `WMF`              |
| Registry/    | `WMF.Registry`     |
| Lifecycle/   | `WMF.Lifecycle`    |
| ModMenu/     | `WMF.ModMenu`      |
| Network/     | `WMF.Network`      |

`WMF.Patch` namespace is retired entirely.

---

## File-by-file actions

| Before | After | Action |
|--------|-------|--------|
| `WMFMod.cs` | `WMFMod.cs` | Update Apply() calls + usings |
| `WMFConfig.cs` | `WMFConfig.cs` | No change |
| `RegisteredMod.cs` | `Registry/RegisteredMod.cs` | Move + namespace |
| `RegisteredGameMode.cs` | `Registry/RegisteredGameMode.cs` | Move + namespace |
| `WMFRegistrants.cs` | `Registry/ModScanner.cs` + `Lifecycle/ModLifecycle.cs` | Split — scanner keeps lists + Scan(); lifecycle keeps ApplyStartupDisables() |
| `Patch/WMFPatch.cs` | `ModMenu/HostStartPagePatch.cs` + `Network/NetworkPatch.cs` | Split at network boundary |
| `Patch/ModsMenuOverlay.cs` | `ModMenu/ModsMenuOverlay.cs` | Move + namespace |
| `Patch/MenuStartPagePatch.cs` | `ModMenu/MenuStartPagePatch.cs` | Move + namespace |
| `Patch/MenuPausePagePatch.cs` | `ModMenu/MenuPausePagePatch.cs` | Move + namespace |
| `Patch/SoloStartPage.cs` | `ModMenu/SoloStartPage.cs` | Move + namespace |
| `Patch/SoloGameModeOverlay.cs` | *(deleted)* | Tombstone |

---

## Key technical decisions

### WMFRegistrants → ModScanner
- Rename as a global replace across all call sites before splitting
- The lists (`Mods`, `Cheats`, `GameModes`, `AllDiscovered`, `SelectedGameModeVariantId`) stay in `ModScanner` — they are registry state
- `ApplyStartupDisables()` moves to `ModLifecycle` — it only reads registry lists and config

### WMFPatch split boundary
- **ModMenu side** (`HostStartPagePatch`): `OnActivatePostfix`, `BeginPlaySessionPrefix`, all stepper/UI helpers, all static UI fields
- **Network side** (`NetworkPatch`): `JoinPlaySessionPrefix`, `OnPlayerJoinedPostfix`, `RPC_ErrorMessageAll_Prefix`, `OnReliableDataReceivedPrefix`, `OnShutdownPostfix`, `OnPlayerLeftPostfix`, `EventBeginLevelPostfix`, `LobbyOnSceneLoadDonePostfix`, `DisconnectIfNotAckedCoroutine`, `ModdedPlayers`, `HandshakePrefix`, stream type constants

### One OnReliableDataReceived prefix
The game throws `NotImplementedException` for unknown stream types. Two Harmony prefixes
on the same method would break the `return false` gating. `NetworkPatch` owns the single
prefix; it delegates to domain handlers:

```csharp
if (streamType == StreamTypeAck)     return HandshakeHandler.HandleAck(runner, player, data);
if (streamType == StreamTypeJoinMsg) return HandshakeHandler.HandleJoinMsg(data);
if (streamType == StreamTypeMux)     return WMFNetwork.TryDispatch(player, data);
return true;
```

### WMFNetwork
Static class initialized from `WMFMod.Awake()`. No MonoBehaviour needed —
the Harmony callbacks are static methods, all network calls go through `NetworkManager`
static instance.

Public API (mod authors use this):
```csharp
public static void Subscribe(string channel, Action<PlayerRef, byte[]> handler)
public static void Unsubscribe(string channel, Action<PlayerRef, byte[]> handler)
public static void Send(PlayerRef target, string channel, byte[] payload)
public static void SendToHost(string channel, byte[] payload)
public static void Broadcast(string channel, byte[] payload)   // host → all modded clients
```

Payload framing: `[1 byte: channel name length][N bytes: channel name UTF-8][remaining: data]`
Channel name limited to 255 UTF-8 bytes (enforced with ArgumentException).

**Subscribe/Unsubscribe lifecycle:** call `Subscribe` in `Enable()` and `Unsubscribe` in
`Disable()` — not in `Awake()`. `Unsubscribe` matches by delegate reference, so the handler
must be stored as a field, not an inline lambda:

```csharp
// correct — stored field, reference is stable
private readonly Action<PlayerRef, byte[]> _onSync = OnSyncReceived;

public void Enable()  => WMFNetwork.Subscribe("my-mod.sync", _onSync);
public void Disable() => WMFNetwork.Unsubscribe("my-mod.sync", _onSync);
```

### Harmony instance
All domains share the single `Harmony` instance created in `WMFMod.Awake()` and
passed into each `Apply(Harmony harmony)`. `UnpatchSelf()` cleans all domains.

### StartCoroutine in handshake
`DisconnectIfNotAckedCoroutine` needs a MonoBehaviour. Pass `WMFMod.Instance`
at call time — already how the code works; do not cache it statically in Network/.

---

## WMFMod.Awake() after refactor

```csharp
WMFConfig.Init(Config);
ModScanner.Scan();  // no longer — still called from MenuStartPagePatch.AppManagerInit
NetworkPatch.Apply(_harmony);
HostStartPagePatch.Apply(_harmony);
MenuStartPagePatch.Apply(_harmony);
MenuPausePagePatch.Apply(_harmony);
// ModsMenuOverlay and SoloStartPage: Apply called from MenuPausePagePatch/MenuStartPagePatch
```

---

## Out of scope (future)
- Rename WMF → WMF (separate plan)
- BepInEx preloader patcher for mod gating
- `Broadcast()` requires tracking modded clients list — implemented as part of this plan
  since `ModdedPlayers` already exists in the network domain
