# 02 — Panneau de paramètres

---

Prérequis : [01-init.md](01-init.md)

Ajoute Player Emotes à la barre latérale de paramètres de WMF pour que les joueurs puissent configurer le raccourci d'emote sans quitter le jeu.

## Étape 1 : IModMenuProvider

`IModMenuProvider` est dans `ModRegistry.dll` — déjà référencé dans le tutoriel 01. Ajoute-le à ta déclaration de classe aux côtés de `IModRegistrant`.

```csharp
using ModRegistry;
using UnityEngine.UIElements;

[BepInPlugin(Id, Name, Version)]
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
public class PlayerEmotesMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {

    // Membres IModRegistrant (inchangés depuis le tutoriel 01)
    ...

    // IModMenuProvider
    public string MenuName => "Player Emotes";

    public void OpenMenu(VisualElement container, bool isInGameMenu) {
        _menu = new PlayerEmotesMenu(CfgEmoteKey, isInGameMenu);
        _menu.Build(container);
    }

    public void CloseMenu() {
        _menu?.Dispose();
        _menu = null;
    }

    // Retourne null si tu n'as pas de sous-sections.
    public (string Title, Action<VisualElement, bool> Build)[] SubMenus => null;
}
```

> **`SubMenus`** est un membre requis de `IModMenuProvider`. Il expose des sous-sections optionnelles et dépliables sous `MenuName`. Retourne `null` (ou un tableau vide) lorsqu'il n'est pas utilisé. Nécessite `using System;` pour `Action`.

> **`CloseMenu`** doit appeler `Dispose()` sur l'instance du menu pour qu'une opération de rebind active soit annulée proprement lorsque le joueur navigue ailleurs.

> **`UnityEngine.UIElementsModule`** doit être référencé dans ton `.csproj` — il n'est pas inclus par `Common.props` :
> ```xml
> <Reference Include="UnityEngine.UIElementsModule">
>   <HintPath>$(RaidersOfBlackveilRootPath)\RoB_Data\Managed\UnityEngine.UIElementsModule.dll</HintPath>
>   <Private>False</Private>
> </Reference>
> ```

## Étape 2 : Stocker le raccourci dans la config BepInEx

Lie l'entrée de config avant le bloc try/catch pour qu'elle soit toujours disponible, même si `Init()` s'interrompt.

```csharp
public static ConfigEntry<Key> CfgEmoteKey;
private PlayerEmotesMenu _menu;

private void Awake() {
    PublicLogger = Logger;
    CfgEmoteKey = Config.Bind("Controls", "EmoteKey", Key.T, "Key to send an emote.");
    try {
        // … Init, Enable, log …
    }
    catch (Exception ex) { PublicLogger.LogError(ex); }
}
```

> Utilise `Key.T` comme valeur par défaut — `Key.E` est déjà lié à l'interaction dans le jeu de base.

## Étape 3 : Construire l'interface des paramètres

Crée `UI/PlayerEmotesMenu.cs`. Utilise `OptionButton` et `LocLabel` de `RR.UI.Controls` — les mêmes composants de jeu utilisés par tous les autres panneaux de paramètres WMF. `Assembly-CSharp` (déjà dans `Common.props`) les fournit, aucune référence csproj supplémentaire n'est nécessaire.

```csharp
using BepInEx.Configuration;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu.Options;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace PlayerEmotes.UI {
    internal sealed class PlayerEmotesMenu {
        private readonly ConfigEntry<Key> _keyEntry;
        private readonly bool _isInGameMenu;
        private LocLabel _btnLabel;
        private InputActionRebindingExtensions.RebindingOperation _rebindOp;
        private InputAction _tempAction;

        internal PlayerEmotesMenu(ConfigEntry<Key> keyEntry, bool isInGameMenu) {
            _keyEntry = keyEntry;
            _isInGameMenu = isInGameMenu;
        }

        internal void Build(VisualElement container) {
            var row = new OptionButton();
            var lbl = row.Q<LocLabel>("Label");
            if (lbl != null) { lbl.CustomTransform = _ => "Emote Key"; lbl.Refresh(); }

            _btnLabel = row.Q<LocLabel>("Button");
            RefreshButtonLabel();

            if (!_isInGameMenu) {
                row.OnClickedEvent = StartRebind;
            } else {
                row.SetEnabled(false);
            }

            container.Add(row);
        }

        internal void Dispose() {
            if (_rebindOp != null) {
                _rebindOp.Cancel();
                // FinishRebind sera appelé via OnCancel
            }
        }

        private void RefreshButtonLabel() => SetButtonText(_keyEntry.Value.ToString());

        private void SetButtonText(string text) {
            if (_btnLabel == null) { return; }
            _btnLabel.CustomTransform = _ => text;
            _btnLabel.Refresh();
        }
    }
}
```

> **`OptionButton`** est un contrôle de jeu pré-stylisé avec un emplacement de libellé à gauche et un emplacement de bouton à droite. Requête les emplacements par nom : `"Label"` pour la description, `"Button"` pour la valeur courante.

> **`LocLabel.CustomTransform`** contourne la recherche de localisation et affiche une chaîne littérale. Appelle `Refresh()` après l'avoir défini pour redessiner.

> **`row.SetEnabled(false)`** grise la ligne lorsqu'elle est ouverte depuis l'écran de pause, au lieu d'ignorer silencieusement les clics. Cela correspond aux conventions de contrôle du jeu lui-même.

## Étape 4 : Implémenter la capture de touche

Ajoute `StartRebind` et `FinishRebind` dans `PlayerEmotesMenu`. Désactive `InputManager.UIActionMap` pendant l'écoute pour que l'UI n'intercepte pas la touche avant que l'opération de rebind ne puisse le faire.

```csharp
private void StartRebind() {
    if (_rebindOp != null) { return; }

    SetButtonText("...");

    if (InputManager.Instance != null) {
        InputManager.Instance.UIActionMap.Enabled = false;
    }

    _tempAction = new InputAction("PE_Rebind", InputActionType.Button);
    _tempAction.AddBinding("<Keyboard>/t");

    _rebindOp = _tempAction
        .PerformInteractiveRebinding(0)
        .WithControlsHavingToMatchPath("<Keyboard>")
        .WithCancelingThrough("<Keyboard>/escape")
        .WithControlsExcluding("<Keyboard>/escape")
        .WithControlsExcluding("<Keyboard>/enter")
        .OnMatchWaitForAnother(0.1f)
        .OnComplete(op => FinishRebind(op, complete: true))
        .OnCancel(op => FinishRebind(op, complete: false))
        .Start();
}

private void FinishRebind(InputActionRebindingExtensions.RebindingOperation op, bool complete) {
    if (complete && op.selectedControl is KeyControl kc) {
        _keyEntry.Value = kc.keyCode;
    }

    op.Dispose();
    _rebindOp = null;
    _tempAction?.Disable();
    _tempAction?.Dispose();
    _tempAction = null;

    if (InputManager.Instance != null) {
        InputManager.Instance.UIActionMap.Enabled = true;
    }

    RefreshButtonLabel();
}
```

> **Dispose `_tempAction`** après la fin du rebind. Le laisser actif fait fuiter une InputAction activée qui peut interférer avec les bindings suivants.

## Résultat

Ouvre **Mods → Player Emotes** depuis le menu principal. Clique sur le bouton à côté de **Emote Key** et appuie sur n'importe quelle touche — le bouton se met à jour immédiatement. La ligne est grisée lorsqu'elle est ouverte depuis l'écran de pause. Le paramètre est persisté sur disque automatiquement.

---

## Suivant

→ [03-localization.md](03-localization.md) — Localiser le libellé du panneau de paramètres
