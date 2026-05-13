# 01 — Mise en place du projet et enregistrement WMF

---

Crée un plugin BepInEx, référence ModRegistry.dll, et fais-le apparaître dans la liste "Mods" de WMF avec un bouton activer/désactiver fonctionnel.

## Pourquoi IModRegistrant plutôt que le duck typing

WMF propose deux façons d'enregistrer un mod :

- **Duck typing** — ajoute `GetModType()`, `GetModName()`, etc. comme membres publics ordinaires. Pas de référence à ModRegistry, pas de `[BepInDependency]` sur WMF. Le mod se charge même si WMF n'est pas installé.
- **`IModRegistrant`** — implémenter l'interface fournie par `ModRegistry.dll`, déclarer `[BepInDependency]` sur WMF. BepInEx impose l'ordre de chargement et refuse de charger ton mod si WMF est absent.

Player Emotes appellera directement les APIs WMF (panneau de paramètres dans le tutoriel 02, réseau dans les tutoriels suivants). Dès l'instant où tu références `WildguardModFramework.dll` à la compilation, tu as besoin que WMF soit présent à l'exécution — la garantie WMF-optionnel du duck typing ne tient plus. Utiliser `IModRegistrant` avec `[BepInDependency]` rend cette dépendance forte explicite et laisse BepInEx l'appliquer proprement.

**Règle générale :** utilise le duck typing pour les mods qui n'appellent jamais une API WMF. Utilise `IModRegistrant` pour tout ce qui le fait.

## Étape 1 : Créer la classe plugin

Ajoute une classe qui étend `BaseUnityPlugin`. L'attribut `[BepInPlugin]` identifie ton mod auprès de BepInEx.

```csharp
using BepInEx;

namespace PlayerEmotes {
    [BepInPlugin(Id, Name, Version)]
    public class PlayerEmotesMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.player-emotes";
        public const string Name    = "PlayerEmotes";
        public const string Version = "0.1.0";
        public const string Author  = "christphe";
    }
}
```

## Étape 2 : Déclarer la dépendance WMF et référencer ModRegistry

Ajoute `[BepInDependency]` pour que BepInEx garantisse que WMF se charge en premier et refuse de charger ton mod si WMF est absent.

```csharp
[BepInPlugin(Id, Name, Version)]
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
public class PlayerEmotesMod : BaseUnityPlugin {
    ...
}
```

`ModRegistry.dll` est livré comme projet source dans ce dépôt (`libs/ModRegistry/`). Référence-le via `ProjectReference` dans ton `.csproj`, aux côtés d'une `ProjectReference` vers WildguardModFramework (nécessaire pour les tutoriels suivants) :

```xml
<ItemGroup>
  <ProjectReference Include="..\..\libs\ModRegistry\ModRegistry.csproj" />
  <ProjectReference Include="..\WildguardModFramework\WildguardModFramework.csproj">
    <Private>False</Private>
  </ProjectReference>
</ItemGroup>
```

> `<Private>False</Private>` sur la référence WMF empêche la copie de sa DLL dans ta sortie — WMF est déjà présent dans le dossier plugins à l'exécution.

## Étape 3 : Implémenter IModRegistrant

`IModRegistrant` (dans `ModRegistry.dll`) est l'interface que WMF utilise pour la découverte et le basculement des mods.

La classe Patch suit le découpage **Init/Patch/Unpatch** : les handles de réflexion sont résolus une fois dans `Init()`, les patches appliqués dans `Patch()`, et défaits dans `Unpatch()`. Consulte `docs/dev/patterns/wmf-duck-typing.md` pour le pattern complet.

```csharp
using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using PlayerEmotes.Patch;

namespace PlayerEmotes {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class PlayerEmotesMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.player-emotes";
        public const string Name    = "PlayerEmotes";
        public const string Version = "0.1.0";
        public const string Author  = "christphe";

        public static ManualLogSource PublicLogger;
        private readonly Harmony _harmony = new Harmony(Id);

        public string GetModType()        => nameof(ModType.Cosmetics);
        public string GetModName()        => Name;
        public string GetModDescription() => "Send floating text reactions visible to all players.";
        public bool   IsClientRequired    => false;
        public bool   Disabled            { get; private set; }

        public void Enable() {
            Disabled = false;
            PlayerEmotesPatch.Patch(_harmony);
        }

        public void Disable() {
            Disabled = true;
            PlayerEmotesPatch.Unpatch();
        }

        private void Awake() {
            PublicLogger = Logger;
            try {
                if (!PlayerEmotesPatch.Init()) {
                    PublicLogger.LogError($"{Name}: init failed — mod disabled.");
                    return;
                }
                Enable();
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
```

> **`GetModType()`** contrôle quel toggle hôte gouverne ton mod. `nameof(ModType.Cosmetics)` apparaît sous **Allow Cosmetics**. Autres valeurs acceptées : `ModType.Mod`, `ModType.Cheat`, `ModType.Utility`, `ModType.GameMode`.

> **`Enable()` et `Disable()`** sont appelés par WMF au démarrage selon la configuration sauvegardée du joueur, et à nouveau chaque fois que l'hôte modifie les permissions des mods en cours de session. `Awake()` doit uniquement lier la config et appeler `Init()` — ne patche pas là.

> **`Disabled`** appartient à la classe plugin, pas à la classe patch. La classe patch n'a pas de champ `Disabled` car elle n'en a pas besoin — les méthodes patch ne s'exécutent pas lorsqu'elles sont dépatchées.

Et la classe patch squelette (`Patch/PlayerEmotesPatch.cs`) :

```csharp
using HarmonyLib;

namespace PlayerEmotes.Patch {
    internal static class PlayerEmotesPatch {
        private static Harmony _harmony;
        private static bool _patched;

        internal static bool Init() {
            return true;
        }

        internal static void Patch(Harmony harmony) {
            if (_patched) { return; }
            _harmony = harmony;
            PlayerEmotesMod.PublicLogger.LogInfo("PlayerEmotes patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            _patched = false;
        }
    }
}
```

## Résultat

Lance le jeu. Ouvre le bouton **Mods** dans le menu principal ou l'écran de pause. Player Emotes apparaît dans la liste avec un bouton activer/désactiver fonctionnel.

---

## Suivant

→ [02-settings-panel.md](02-settings-panel.md) — Ajouter un panneau de paramètres en jeu avec un raccourci clavier configurable
