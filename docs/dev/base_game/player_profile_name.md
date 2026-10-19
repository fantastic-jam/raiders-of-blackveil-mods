# Player profile name — where the local display name comes from

Verified against the 2026-10-19 build.

## Source of `PlayerProfile.Name`

`BackendManager.Instance.LoggedInWomboPlayer.PlayerProfile` is a `WomboPlayer.WomboPlayerProfile` (public fields `Name`, `AvatarUrl`, `TagLine`, `Locale`, `Region`).

V2 login flow (current):

```
WomboPlayer.LoadOfflinePlayer(Guid playerIdV2)            // RR.Backend
  ├─ existing profile dir → JsonConvert.PopulateObject(...)  // loads saved profile
  │                       → FederatedLoginManager.Instance.ExtractPlayerProfileFromSelectedGateway(ref PlayerProfile)
  └─ new player (Steam)   → ... → ExtractPlayerProfileFromSelectedGateway(ref PlayerProfile)

FederatedLoginManager.ExtractPlayerProfileFromSelectedGateway(ref wpp)
  → GetIdentityProvider(SelectedLoginGateway).ExtractPlayerProfile(ref wpp)

SteamController.ExtractPlayerProfile(ref wpp)   // RR.Backend.Integration
  wpp.Name   = SteamFriends.GetPersonaName();
  wpp.Locale = SteamApps.GetCurrentGameLanguage();
```

So on Steam the name is **always** overwritten with the local Steam persona name on load. That
works whatever the Steam profile privacy setting is.

## Dead code: `WomboPlayer.InitializeFrom(IngressResponseGameSessionLogin)`

The v1 login path copied `LoginResponse.player.profile.name` from the backend into
`PlayerProfile.Name`. A private Steam profile made that name `<N/A>`. **Nothing in
Assembly-CSharp calls `InitializeFrom` any more.** A patch on it never fires. (This is why the
PlayerNameFix mod no longer has any effect.)

## Consumers

- `RR.Game.Player` → `UserName = BackendManager.Instance.LoggedInWomboPlayer.PlayerProfile.Name;` (networked name)
- `RR.UI.Pages.MenuStartPage` → login name label
- `RR.Backend.Session.V2_OfflineSessionManager` → session metadata

To override the local name, postfix `WomboPlayer.LoadOfflinePlayer`. It is async (`UniTask<bool>`), so a
plain postfix runs at the first `await`. Patching `SteamController.ExtractPlayerProfile` is simpler:

```csharp
[HarmonyPostfix, HarmonyPatch(typeof(SteamController), nameof(SteamController.ExtractPlayerProfile))]
private static void Postfix(ref WomboPlayer.WomboPlayerProfile wpp) => NameController.Override(ref wpp);
```
