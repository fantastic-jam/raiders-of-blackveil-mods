# 05 — Réseau

---

Prérequis : [04-local-labels.md](04-local-labels.md)

Envoie l'emote sur la session pour que tous les joueurs voient le libellé au-dessus du personnage de l'émetteur.

## Comment fonctionne le réseau WMF

WMF fournit un multiplexeur de canaux fiable sur la session Fusion. Les mods s'abonnent à des canaux de chaînes nommés et échangent des charges utiles brutes en octets.

| Méthode | Direction |
|---|---|
| `WmfNetwork.SendToHost(channel, payload)` | Client → hôte |
| `WmfNetwork.Broadcast(channel, payload)` | Hôte → tous les clients WMF |
| `WmfNetwork.Send(target, channel, payload)` | Hôte → un client |

Le pattern pour un emote diffusé : le client envoie à l'hôte → l'hôte diffuse à tout le monde → tous les clients affichent le libellé.

## Étape 1 : S'abonner et se désabonner

Stocke le délégué gestionnaire comme champ pour que `Unsubscribe` supprime exactement la même instance. Connecte-le dans `Enable()`/`Disable()` dans `PlayerEmotesMod.cs`.

Ajoute le champ aux côtés des autres champs statiques :

```csharp
private static Action<PlayerRef, byte[]> _onEmote;
```

Mets à jour `Enable()` et `Disable()` :

```csharp
public void Enable() {
    Disabled = false;
    _onEmote = EmoteController.OnEmoteReceived;
    WmfNetwork.Subscribe("player-emotes.emote", _onEmote);
    PlayerEmotesPatch.Patch(_harmony);
    // … bloc #if DEV_HOTRELOAD inchangé …
}

public void Disable() {
    Disabled = true;
    WmfNetwork.Unsubscribe("player-emotes.emote", _onEmote);
    PlayerEmotesPatch.Unpatch();
    // … bloc #if DEV_HOTRELOAD inchangé …
}
```

> **Nommage des canaux** : utilise un format namespacé (`ton-mod.canal`) pour éviter les collisions avec d'autres mods.

## Étape 2 : Envoyer lors d'un appui de touche

Remplace `TriggerLocal()` dans `EmoteController.cs` par une version réseau-compatible. Le client qui appuie envoie l'ID d'emote et son propre `PlayerId` à l'hôte :

```csharp
internal static void TriggerNetwork() {
    var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
    var payload  = new[] { (byte)localRef.PlayerId, (byte)0 };   // byte 1 = emote id
    WmfNetwork.SendToHost("player-emotes.emote", payload);
}
```

Mets à jour `EmoteInput.OnUpdate()` pour appeler `TriggerNetwork()` à la place de `TriggerLocal()` :

```csharp
internal static void OnUpdate() {
    if (Keyboard.current[PlayerEmotesMod.CfgEmoteKey.Value].wasPressedThisFrame)
        EmoteController.TriggerNetwork();
}
```

> La méthode s'appelle `OnUpdate()`, pas `Update()` — voir le tutoriel 04 pour la raison.

## Étape 3 : Gérer le message reçu

Ajoute `OnEmoteReceived` dans `EmoteController.cs`. Le paramètre `sender` est le `PlayerRef` Fusion de celui qui a envoyé ce message (le client qui émet lors qu'on est sur l'hôte ; l'hôte lors qu'on est client). La charge utile transporte le `PlayerId` du joueur émetteur pour que toutes les machines sachent quelle tête labelliser.

```csharp
internal static void OnEmoteReceived(PlayerRef sender, byte[] payload) {
    if (payload.Length < 2) { return; }

    var runner = PlayerManager.Instance?.Runner;
    if (runner == null) { return; }

    // L'hôte rediffuse pour que chaque client le reçoive
    if (runner.IsServer)
        WmfNetwork.Broadcast("player-emotes.emote", payload);

    // Toutes les machines (y compris l'hôte) affichent le libellé
    ShowForPlayer(payload[0], payload[1] == 0 ? "Hi!" : "?");
}
```

> **Pas d'emoji** — la police du jeu ne rend pas les emojis. Utilise des chaînes en texte simple. Voir le tutoriel 04.

## Étape 4 : Résoudre la position de l'émetteur

Récupère le joueur par `PlayerId` (encodé en `payload[0]`) et spawne le libellé avec `EffectText` + `UIManager`. `PlayableChampion` est un `NetworkBehaviour` — caste-le en `MonoBehaviour` pour accéder à `gameObject`.

```csharp
private static void ShowForPlayer(byte senderId, string text) {
    var player   = PlayerManager.Instance?.GetPlayers()?.Find(p => (byte)p.PlayerId == senderId);
    var champion = player?.PlayableChampion as MonoBehaviour;
    if (champion == null) { return; }

    var label = EffectText.Allocate(text, 48f, Color.white);
    UIManager.Instance.RegisterOverlayElement(label, champion.gameObject, Vector3.up * 2f, Vector2.zero);
}
```

> Utilise `GetPlayers().Find(p => ...)` pour rechercher par `PlayerId` plutôt que de reconstruire un `PlayerRef`. `PlayerRef.FromEncoded` existe dans Fusion mais `GetPlayers()` est plus simple et évite de dépendre des détails internes d'encodage.

Le `EmoteController.cs` complet :

```csharp
using Fusion;
using RR;
using RR.UI.Controls.HUD.Overlay;
using RR.UI.UISystem;
using UnityEngine;
using WildguardModFramework.Network;

namespace PlayerEmotes {
    internal static class EmoteController {
        private const string Channel = "player-emotes.emote";

        internal static void TriggerNetwork() {
            var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
            var payload  = new[] { (byte)localRef.PlayerId, (byte)0 };
            WmfNetwork.SendToHost(Channel, payload);
        }

        internal static void OnEmoteReceived(PlayerRef sender, byte[] payload) {
            if (payload.Length < 2) { return; }

            var runner = PlayerManager.Instance?.Runner;
            if (runner == null) { return; }

            if (runner.IsServer)
                WmfNetwork.Broadcast(Channel, payload);

            ShowForPlayer(payload[0], payload[1] == 0 ? "Hi!" : "?");
        }

        private static void ShowForPlayer(byte senderId, string text) {
            var player   = PlayerManager.Instance?.GetPlayers()?.Find(p => (byte)p.PlayerId == senderId);
            var champion = player?.PlayableChampion as MonoBehaviour;
            if (champion == null) { return; }

            var label = EffectText.Allocate(text, 48f, Color.white);
            UIManager.Instance.RegisterOverlayElement(label, champion.gameObject, Vector3.up * 2f, Vector2.zero);
        }
    }
}
```

## Résultat

N'importe quel joueur appuie sur la touche d'emote. Tous les clients WMF connectés voient "Hi!" flotter et s'estomper au-dessus de la tête de ce joueur.

---

## Étendre le mod

| Fonctionnalité | Ce qu'il faut modifier |
|---|---|
| Plus d'emotes | Étends l'octet `emoteId` ; ajoute un sélecteur dans le panneau de paramètres |
| Cooldown | Trace `Time.time` du dernier envoi ; rejette `TriggerNetwork()` si c'est trop tôt |
| Écho dans le chat | Appelle `ServerChat.SendMessage()` en parallèle de `TriggerNetwork()` pour aussi poster l'emote dans le chat |
