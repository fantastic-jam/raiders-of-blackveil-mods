# 04 — Libellés locaux

---

Prérequis : [03-localization.md](03-localization.md)

Détecte l'appui de la touche d'emote et affiche un libellé texte flottant au-dessus du joueur local. Pas de réseau encore — seul le joueur qui appuie voit le libellé.

## Étape 1 : Connecter la détection d'appui de touche dans le patch HUD

Un `MonoBehaviour.Update()` autonome sur un objet `DontDestroyOnLoad` ne reçoit pas l'input de façon fiable dans ce jeu. Le pattern correct (identique à BeginnersWelcome) consiste à interroger le clavier dans un postfix Harmony sur `BaseHUDPage.OnUpdate` — une mise à jour gérée par le jeu qui s'exécute chaque frame tant qu'une page HUD est active (c'est-à-dire pendant une run).

Crée `EmoteInput.cs` comme une **classe statique** (pas de `MonoBehaviour`) :

```csharp
using UnityEngine.InputSystem;

namespace PlayerEmotes {
    internal static class EmoteInput {
        internal static void OnUpdate() {
            if (Keyboard.current[PlayerEmotesMod.CfgEmoteKey.Value].wasPressedThisFrame)
                EmoteController.TriggerLocal();
        }
    }
}
```

Ensuite, connecte-le dans `PlayerEmotesPatch.cs`. Résous `BaseHUDPage.OnUpdate` dans `Init()`, patche-le dans `Patch()`, et appelle `EmoteInput.OnUpdate()` depuis le postfix :

```csharp
using System.Reflection;
using HarmonyLib;
using RR.UI.Pages;

namespace PlayerEmotes.Patch {
    internal static class PlayerEmotesPatch {
        private static Harmony _harmony;
        private static bool _patched;
        private static MethodInfo _hudOnUpdate;

        internal static bool Init() {
            _hudOnUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (_hudOnUpdate == null) {
                PlayerEmotesMod.PublicLogger.LogWarning("PlayerEmotes: BaseHUDPage.OnUpdate not found — emote key unavailable.");
            }
            return true;
        }

        internal static void Patch(Harmony harmony) {
            if (_patched) { return; }
            _harmony = harmony;
            if (_hudOnUpdate != null) {
                _harmony.Patch(_hudOnUpdate, postfix: new HarmonyMethod(typeof(PlayerEmotesPatch), nameof(OnHUDUpdatePostfix)));
            }
            PlayerEmotesMod.PublicLogger.LogInfo("PlayerEmotes patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            _patched = false;
        }

        private static void OnHUDUpdatePostfix() {
            EmoteInput.OnUpdate();
        }
    }
}
```

> Aucune garde `Disabled` n'est nécessaire dans le postfix — `Unpatch()` supprime entièrement le hook lorsque le mod est désactivé.

> Ne crée **pas** un `new GameObject` ni n'appelle `AddComponent<EmoteInput>()` dans `Awake()`. L'approche MonoBehaviour ne fonctionne pas ici ; le patch est suffisant.

## Étape 2 : Spawner le libellé flottant

Crée `EmoteController.cs`. Le jeu dispose déjà d'un système de texte flottant poolé (`EffectText`) utilisé pour les chiffres de dégâts. Utilise-le directement — pas de Canvas personnalisé, pas de coroutine nécessaire.

- `EffectText.Allocate(text, size, color)` récupère un libellé poolé, définit le texte et le style, et démarre sa propre animation de flottement et d'estompage (~0,6 s).
- `UIManager.Instance.RegisterOverlayElement(element, gameObject, offset3D, offset2D)` ancre le libellé à un `GameObject` dans l'espace monde pour qu'il suive le personnage lors de ses déplacements.
- `LocalPlayer.PlayableChampion` est l'interface du champion ; caste-la en `MonoBehaviour` pour obtenir son `gameObject`.

```csharp
using RR;
using RR.UI.Controls.HUD.Overlay;
using RR.UI.UISystem;
using UnityEngine;

namespace PlayerEmotes {
    internal static class EmoteController {
        internal static void TriggerLocal() {
            var champion = PlayerManager.Instance?.LocalPlayer?.PlayableChampion as MonoBehaviour;
            if (champion == null) { return; }

            var label = EffectText.Allocate("Hi!", 48f, Color.white);
            UIManager.Instance.RegisterOverlayElement(label, champion.gameObject, Vector3.up * 2f, Vector2.zero);
        }
    }
}
```

## Résultat

Appuie sur la touche d'emote pendant une run. "Hi!" flotte vers le haut au-dessus du joueur local et s'estompe en environ une demi-seconde. Aucun autre joueur ne le voit encore.

---

## Suivant

→ [05-networking.md](05-networking.md) — Diffuser l'emote à tous les joueurs de la session
