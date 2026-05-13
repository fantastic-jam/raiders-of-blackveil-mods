# 03 — Localisation

---

Prérequis : [02-settings-panel.md](02-settings-panel.md)

Remplace les chaînes anglaises codées en dur dans le panneau de paramètres par des lookups `TranslationService` pour que le mod puisse être traduit sans recompiler.

## Comment ça fonctionne

`TranslationService` (dans `WildguardModFramework.Translation`) charge des fichiers JSON plats depuis le dossier `Assets/Localization/` du plugin au démarrage. Tu récupères un délégué `T` qui résout les clés en chaînes — avec repli sur `"en"` puis sur la clé elle-même si aucune traduction n'est trouvée.

Ordre de résolution : **langue courante → `"en"` → clé littérale**.

## Étape 1 : Créer le fichier de traduction anglais

Crée `Assets/Localization/PlayerEmotes.en.json` :

```json
{
  "mod.description": "Send floating text reactions visible to all players.",
  "menu.emote_key": "Emote Key"
}
```

Nommage des fichiers : `{ModName}.{lang}.json` — `PlayerEmotes.en.json` pour l'anglais, `PlayerEmotes.fr.json` pour le français, etc. Le code de locale correspond à `AppManager.Instance.PlayerSettings.Gen_Language`.

## Étape 2 : Déclarer les assets dans le csproj

Ajoute un groupe d'éléments `Content` pour que les fichiers soient copiés dans la sortie de build et inclus dans le ZIP de release :

```xml
<ItemGroup>
  <Content Include="Assets\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

## Étape 3 : Initialiser le délégué de traduction

Dans `PlayerEmotesMod.cs`, ajoute un champ statique `t` et appelle `TranslationService.For()` tôt dans `Awake()` — avant le try/catch, pour que `t` soit toujours utilisable.

```csharp
using WildguardModFramework.Translation;

internal static T t;

private void Awake() {
    PublicLogger = Logger;
    t = TranslationService.For(Name, Info.Location);
    CfgEmoteKey = Config.Bind("Controls", "EmoteKey", Key.T, "Key to send an emote.");
    try {
        // … Init, Enable, log …
    }
    catch (Exception ex) { PublicLogger.LogError(ex); }
}
```

> **`Info.Location`** est le chemin complet vers la DLL de ton plugin, fourni par BepInEx. `TranslationService` en déduit `Assets/Localization/`.

> **`T`** est un type délégué : `delegate string T(string key, params (string Key, object Value)[] args)`. Les args sont des espaces réservés nommés optionnels — `t("key", ("player", name))` remplace `{player}` dans la chaîne de valeur.

## Étape 4 : Remplacer les chaînes codées en dur

Dans `PlayerEmotesMod.cs`, remplace la description littérale :

```csharp
public string GetModDescription() => t("mod.description");
```

Dans `UI/PlayerEmotesMenu.cs`, remplace le littéral `CustomTransform` :

```csharp
internal void Build(VisualElement container) {
    var row = new OptionButton();
    var lbl = row.Q<LocLabel>("Label");
    if (lbl != null) {
        lbl.CustomTransform = _ => PlayerEmotesMod.t("menu.emote_key");
        lbl.Refresh();
    }
    // … reste inchangé …
}
```

## Résultat

La description du mod et le libellé des paramètres sont maintenant pilotés par `PlayerEmotes.en.json`. Pour ajouter le français, crée `Assets/Localization/PlayerEmotes.fr.json` avec les mêmes clés — aucun changement de code nécessaire.

---

## Suivant

→ [04-local-labels.md](04-local-labels.md) — Afficher un libellé flottant au-dessus du joueur local lors d'un appui de touche
