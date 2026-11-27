# BepInEx IL2CPP for Raiders of Blackveil

Source and build steps for the
[BepInEx IL2CPP pack on Nexus Mods](https://www.nexusmods.com/raidersofblackveil/mods/19).

Raiders of Blackveil 0.6.5 moved to IL2CPP (Unity 6000.5, global-metadata v107). Stock BepInEx 6 does
not run on it yet, for two reasons:

- Its bundled Cpp2IL can't read metadata v107
  ([BepInEx#1395](https://github.com/BepInEx/BepInEx/issues/1395)). Newer Cpp2IL builds can read it,
  but they depend on AsmResolver 6, which conflicts with Il2CppInterop
  ([Il2CppInterop#295](https://github.com/BepInEx/Il2CppInterop/issues/295)).
- Unity 6000.5 reordered the native `Il2CppClass` struct, so Il2CppInterop 1.5.3 crashes when it
  injects classes.

This pack is the official **BepInEx 6.0.0-be.793** build with two DLLs rebuilt from upstream source
plus the patches in this folder.

## What differs from stock be.793

| File                                     | Change                                                                                                                                                                                                               |
| ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BepInEx/core/BepInEx.Unity.IL2CPP.dll`  | [`external-cpp2il.patch`](external-cpp2il.patch) on BepInEx `dbc82f0`. If `BepInEx/cpp2il/Cpp2IL.exe` exists, it runs that Cpp2IL in a separate process and loads its output. Config: `[IL2CPP] ExternalCpp2ILPath`. |
| `BepInEx/core/Il2CppInterop.Runtime.dll` | [`il2cppinterop-unity6000_5-class.patch`](il2cppinterop-unity6000_5-class.patch) on Il2CppInterop `v1.5.3`. Adds the Unity 6000.5+ `Il2CppClass` layout.                                                             |
| `BepInEx/cpp2il/` (new)                  | Unmodified Cpp2IL CI build: `development` branch @ `b5ad444`, GitHub Actions run [34694167702](https://github.com/SamboyCoding/Cpp2IL/actions/runs/34694167702), artifact `Cpp2IL-Netframework472-Windows`.          |

Every other file, including the bundled .NET runtime, `winhttp.dll` (Doorstop) and the remaining
`core/` DLLs, is the stock be.793 file from
[builds.bepinex.dev](https://builds.bepinex.dev/projects/bepinex_be/793/).

## Build

You need Windows, Git, the .NET SDK 8 or newer, Node.js 22+ with pnpm, and the GitHub CLI logged in
(`gh auth login`, needed to download the Cpp2IL CI artifact).

```bash
git clone https://github.com/fantastic-jam/raiders-of-blackveil-mods.git
cd raiders-of-blackveil-mods
pnpm install
pnpm run build:bepinex
```

The script is [`tools/build-bepinex.mts`](../build-bepinex.mts), with the pinned versions in
[`tools/lib/il2cpp.mts`](../lib/il2cpp.mts). It:

1. clones BepInEx @ `dbc82f0`, applies `external-cpp2il.patch` and builds `BepInEx.Unity.IL2CPP`;
2. clones Il2CppInterop @ `v1.5.3`, applies `il2cppinterop-unity6000_5-class.patch` and builds
   `Il2CppInterop.Runtime`;
3. downloads stock be.793, copies the two DLLs over it and adds the Cpp2IL build to `BepInEx/cpp2il/`.

The output goes to `dist/BepInEx-IL2CPP-RoB/` and `dist/BepInEx-IL2CPP-RoB.zip`.

## Credits and licenses

- [BepInEx](https://github.com/BepInEx/BepInEx), by the BepInEx team. LGPL-2.1.
- [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop), by the BepInEx team, originally
  Unhollower by knah. LGPL-3.0.
- [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL), by Samboy063. MIT.
- [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop), by NeighTools. LGPL-2.1.
- .NET runtime, by Microsoft and the .NET Foundation. MIT.
- The Unity 6000.5 `Il2CppClass` layout comes from
  [roytu/Il2CppDumper#5](https://github.com/roytu/Il2CppDumper/pull/5).

The patches in this folder are under the same license as the file they modify. They will be dropped
once upstream BepInEx and Il2CppInterop support Unity 6000.5.
