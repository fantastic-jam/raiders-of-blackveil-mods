# CLAUDE.md

## What this repo is

C# BepInEx 5 mods for the game **Raiders of Blackveil**. Each mod is a separate project in one solution under `mods/`. Target: `netstandard2.1`, C# 10, Release only — there is no Debug config, no tests.

## Commands

```bash
pnpm run build                                        # build all mods
pnpm run deploy -- --mod [ModName]                    # build + copy to local game
pnpm run release -- --mod [ModName] --version 1.2.3  # full release pipeline
pnpm run release -- --mod [ModName] --bump patch      # auto-increment patch/minor/major
pnpm run release -- --mod [ModName] --version 1.2.3 --dry-run   # preview only
pnpm run release -- --mod [ModName] --version 1.2.3 --skip-push --skip-release  # local only
pnpm run setup                                        # dev environment setup
```

## Game source

`game-src/` is ILSpy-decompiled game source — read it to understand APIs, types, namespaces. Do not edit it. Key namespaces:

- `RR` — top-level: `AppManager`, `BackendManager`
- `RR.Game` — gameplay: `Health`, `DashAbility`, `ChampionAbility`, `StatsManager`
- `RR.Game.Character`, `RR.Game.Damage`, `RR.Game.Stats` — sub-namespaces
- `RR.Config` — `PlayerSettings` (language, settings)
- `RR.UI.Components` — `LocStringExt` (game's localization helper)

When in doubt about a type's namespace, grep `game-src/` before guessing.

## Repo structure

```
mods/
  raiders-of-blackveil-mods.sln
  Common.props          # shared assembly references
  UserPaths.props       # machine-local game path (gitignored), written by setup
  UserPaths.props.template
  metadata.json         # repo-level: nexus_game_domain
  [ModName]/
    [ModName].csproj
    ...
game-src/               # ILSpy-decompiled game source (gitignored)
game-lib/               # shared game DLLs (gitignored)
libs/                   # shared C# libraries (not mods — no BepInEx plugin class)
  ModRegistry/          # IModRegistrant interface + ModType enum
tools/                  # Node/TypeScript build tooling
  build.mts / deploy.mts / package.mts / release.mts / setup.mts
  validate-commit-msg.mts
  lib/mod.mts / git.mts / github.mts / zip.mts
  tsconfig.json
  eslint.config.mts
```

## Mod structure

```
mods/[ModName]/
├── [ModName]Mod.cs          # BepInEx plugin — holds Id, Name, Version constant, Awake()
├── Patch/[ModName]Patch.cs  # Harmony patches
├── Assets/                  # Copied verbatim to the plugin folder on deploy/release
│   └── Localization/        # Translation files: [ModName].[lang].json
├── Config/                  # Optional .cfg templates (not overwritten on deploy)
├── metadata.json            # nexus_mod_id + nexus_file_group_id
└── [ModName].csproj         # SDK-style, imports Common.props + UserPaths.props
```

`Assets/` — any files under `Assets/` are declared as `Content` in the csproj and copied to the build output. The tooling copies the entire `Assets/` folder to the plugin directory on deploy and release. Use it for any runtime files (localization, sprites, data files, etc.).

`Common.props` — all shared assembly references. Add mod-specific refs directly in the mod's `.csproj`.

`UserPaths.props` — machine-local game path, gitignored, written by `pnpm run setup`.

## Harmony patches

- Resolve reflection fields once in `Apply()`, warn and bail if not found
- `AccessTools.Field` / `AccessTools.Method` for private member access
- Use `prefix` to intercept/block, `postfix` to observe/modify after

## Localization

Translation files live at `Assets/Localization/[ModName].[lang].json` (e.g. `PerfectDodge.en.json`). Format: flat `{"key": "value"}` JSON.

The localization class scans the plugin's `Assets/Localization/` directory at startup and loads all matching files. English is the fallback. Locale code comes from `AppManager.Instance?.PlayerSettings?.Gen_Language`.

`DataContractJsonSerializer` requires `UseSimpleDictionaryFormat = true` to parse flat JSON objects — without it, it expects `[{Key, Value}]` pairs and silently fails.

## Releases

`tools/release.mts` flow: bump `Version` constant → build → package ZIP → `git commit` + tag → push → GitHub release via `@octokit/rest`.

- Tag format: `[ModName]-v[Version]`
- ZIP structure: `plugins/fantastic-jam-[ModName]/`
- Changelog is built from `git log --grep=[ModName]` since the previous mod tag — **commit messages must contain the mod name** for changelog filtering to work
- Use `--dry-run` to preview the full plan including the generated changelog before touching anything

## Adding a mod

Look at create_mod.md in docs folder
