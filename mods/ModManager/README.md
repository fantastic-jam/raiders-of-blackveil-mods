# ModManager

> **Deprecated.** ModManager has been superseded by [WMF](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework). Please migrate — ModManager will be removed in a future update.

---

ModManager is the previous version of the mod management runtime for Raiders of Blackveil. It is no longer actively developed. All features have been moved to **WMF** (Wildguard Mod Framework), which also adds a network channel API for mod-to-mod communication.

## Migration

1. Uninstall ModManager (`BepInEx/plugins/fantastic-jam-ModManager/`)
2. Install WMF from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework)
3. Update your mod's `[BepInDependency]` from `ModManagerMod.Id` to `WmfMod.Id`

Other mods that implement `IModRegistrant` via duck typing require no changes.
