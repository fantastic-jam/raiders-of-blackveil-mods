# Player Emotes

Crée un mod qui permet aux joueurs d'envoyer des réactions en texte flottant au-dessus de leur personnage — visibles par tous les joueurs de la session. Chaque tutoriel ajoute une couche supplémentaire par rapport au précédent.

## Tutoriels

| # | Fichier | Ce que tu vas apprendre |
|---|---|---|
| 1 | [01-init.md](01-init.md) | Créer un plugin BepInEx et l'enregistrer auprès de WMF via IModRegistrant |
| 2 | [02-settings-panel.md](02-settings-panel.md) | Ajouter un panneau de paramètres en jeu avec un raccourci clavier configurable |
| 3 | [03-localization.md](03-localization.md) | Localiser le libellé du panneau de paramètres |
| 4 | [04-local-labels.md](04-local-labels.md) | Afficher un libellé flottant au-dessus du joueur local lors d'un appui de touche |
| 5 | [05-networking.md](05-networking.md) | Diffuser l'emote à tous les joueurs de la session |

## Prérequis

- BepInEx 5 installé dans le dossier du jeu
- Wildguard Mod Framework installé
- Un projet C# ciblant `netstandard2.1` avec des références à `BepInEx.dll`, `ModRegistry.dll` et `WildguardModFramework.dll`

## Ce que tu vas construire

Un système d'emotes entièrement en réseau. À la fin du tutoriel 5, n'importe quel joueur peut appuyer sur une touche et tous les joueurs de la session voient un libellé flottant au-dessus de la tête de ce joueur.
