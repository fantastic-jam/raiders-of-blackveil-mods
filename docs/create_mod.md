# Creating a new mod

## 1. Create the project folder

```
mods/[ModName]/
```

## 2. Create the csproj

`mods/[ModName]/[ModName].csproj` — SDK-style, must import `Common.props` and `UserPaths.props`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <Import Project="..\UserPaths.props" Condition="Exists('..\UserPaths.props')" />
  <Import Project="..\Common.props" />

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>[ModName]</AssemblyName>
    <RootNamespace>[ModName]</RootNamespace>
    <LangVersion>10</LangVersion>
    <Optimize>true</Optimize>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>

</Project>
```

Add mod-specific DLL references (e.g. `Fusion.Runtime`) directly here, not in `Common.props`.

## 3. Add to the solution

In `mods/raiders-of-blackveil-mods.sln`, add a project entry and its configuration block.

Pick a fresh GUID for the project — it must be used consistently in both places and in `AssemblyInfo.cs`.

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "[ModName]", "[ModName]\[ModName].csproj", "{YOUR-GUID}"
EndProject
```

And in `GlobalSection(ProjectConfigurationPlatforms)`:

```
{YOUR-GUID}.Release|Any CPU.ActiveCfg = Release|Any CPU
{YOUR-GUID}.Release|Any CPU.Build.0 = Release|Any CPU
```

## 4. Create the plugin entry point

`mods/[ModName]/[ModName]Mod.cs`:

```csharp
using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using [ModName].Patch;

namespace [ModName] {
    [BepInPlugin(Id, Name, Version)]
    public class [ModName]Mod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.[modname]";
        public const string Name = "[ModName]";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private void Awake() {
            PublicLogger = Logger;

            try {
                [ModName]Patch.Apply(new Harmony(Id));
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
```

`Version` must be a `public const string` — the release tooling reads it from the compiled assembly.

## 5. Create the Harmony patch

`mods/[ModName]/Patch/[ModName]Patch.cs`:

```csharp
using HarmonyLib;

namespace [ModName].Patch {
    public static class [ModName]Patch {
        public static void Apply(Harmony harmony) {
            var method = AccessTools.Method(typeof(SomeClass), "SomeMethod");
            if (method == null) {
                [ModName]Mod.PublicLogger.LogWarning("[ModName]: Could not find SomeClass.SomeMethod — patch inactive.");
                return;
            }
            harmony.Patch(method,
                prefix: new HarmonyMethod(AccessTools.Method(typeof([ModName]Patch), nameof(SomeMethodPrefix))));

            [ModName]Mod.PublicLogger.LogInfo("[ModName] patch applied.");
        }

        static bool SomeMethodPrefix(...) {
            // return false to skip original, true to run it
            return true;
        }
    }
}
```

Rules:
- Resolve all reflection handles once in `Apply()`, warn and bail if not found
- Use `AccessTools.Field` / `AccessTools.Method` for private member access
- Use `prefix` to intercept/block, `postfix` to observe/modify after

## 6. Create AssemblyInfo

`mods/[ModName]/Properties/AssemblyInfo.cs` — use the same GUID as the solution entry:

```csharp
using System.Reflection;
using System.Runtime.InteropServices;
using [ModName];

[assembly: AssemblyTitle([ModName]Mod.Name)]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany([ModName]Mod.Author)]
[assembly: AssemblyProduct([ModName]Mod.Name)]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("YOUR-GUID")]
[assembly: AssemblyVersion([ModName]Mod.Version)]
[assembly: AssemblyFileVersion([ModName]Mod.Version)]
```

## 7. Create metadata.json

`mods/[ModName]/metadata.json` — omit the Nexus fields to skip publishing:

```json
{
  "nexus_mod_id": 123,
  "nexus_file_group_id": 456
}
```

Or just `{}` if you don't have a Nexus page yet.

## 8. Create the README

`mods/[ModName]/README.md` — user-facing documentation (GitHub). Template:

```markdown
# [ModName]

One-line description.

---

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2246780/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)

---

## Installation

### 1. Install BepInEx

Skip this step if BepInEx is already installed.

1. Download **BepInEx 5** from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) — pick the `BepInEx_win_x64` build.
2. Extract the contents into your game folder (the one containing `RoB.exe`).
3. Launch the game once and close it — BepInEx will initialize its folder structure.

### 2. Install the mod

1. Download `[ModName]-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=[ModName]).
2. Extract the ZIP directly into your game folder.
3. Launch the game.
```

## 9. Create the Nexus description

`mods/[ModName]/nexus-description.txt` — BB-code formatted for the Nexus Mods page. Mirror the README content in BB-code syntax. See existing mods for the standard structure (title, description, requirements, installation).

## 10. Optional: Assets

Runtime files (localization, sprites, data) go under `mods/[ModName]/Assets/`. Declare them as `Content` in the csproj:

```xml
<ItemGroup>
  <Content Include="Assets\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

The deploy/release tooling copies the entire `Assets/` folder to the plugin directory verbatim.

## 11. Build and deploy

```bash
pnpm run deploy -- --mod [ModName]   # build + copy to local game for testing
pnpm run release -- --mod [ModName] --bump patch   # release when ready
```

Use `--dry-run` to preview the release plan (changelog, version bump, tag) before touching anything.

`deploy.mts` and `package.mts` both auto-discover mods via `listMods()` (reads `mods/` subdirectories) — no changes needed in those scripts when adding a new mod.

## Commit message format

The commit-msg hook enforces:

```
fix|chore|new(scope): message
```

Valid scopes:
- `Repo` — repo-wide changes (tooling, docs, config)
- `All` — changes affecting all mods at once
- `[ModName]` — any folder that exists under `mods/` is automatically valid

The hook reads the `mods/` directory at runtime, so your new mod's name is a valid scope as soon as the folder exists. Commit messages must include the mod name for the release changelog to pick them up correctly.
