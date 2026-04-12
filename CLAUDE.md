# CLAUDE.md

## What this repo is

C# BepInEx 5 mods for the game **Raiders of Blackveil**. Each mod is a separate project in one solution under `mods/`. Target: `netstandard2.1`, C# 10, Release only — there is no Debug config, no tests.

## Commands

```bash
pnpm run build                                        # build all mods
pnpm run deploy -- --mod [ModName]                    # build + copy to local game
pnpm run pre-release -- --mod [ModName] --bump patch  # bump version + write CHANGELOG.md (no commit)
pnpm run pre-release -- --mod [ModName] --version 1.2.3
pnpm run release -- --mod [ModName]                   # commit version+changelog, tag, push, GH release
pnpm run release -- --mod [ModName] --dry-run         # preview release without modifying anything
pnpm run release -- --mod [ModName] --skip-push --skip-release  # local only
pnpm run setup                                        # dev environment setup
```

## Documentation

`docs/` contains design and developer documentation. **Read the relevant doc before diving into `game-src/`.** Docs capture intent, call chains, and mod patterns that are not obvious from decompiled code alone.

| Topic | File |
|---|---|
| Lobby plan table — full E-interact → run-start flow | [`docs/dev/base_game/lobby_plan_table.md`](docs/dev/base_game/lobby_plan_table.md) |
| Run structure, rooms, level progression | [`docs/dev/base_game/run_structure.md`](docs/dev/base_game/run_structure.md) |
| Key game classes quick-reference | [`docs/dev/base_game/key_classes.md`](docs/dev/base_game/key_classes.md) |
| Fusion networking facts | [`docs/dev/base_game/fusion_networking.md`](docs/dev/base_game/fusion_networking.md) |

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

## Rules

These are enforced on every task — no exceptions:

1. **Patch methods are one-liners.** All logic goes in a Controller / Proxify / Orchestrator class. See [`docs/dev/patterns/patch-extraction.md`](docs/dev/patterns/patch-extraction.md).
2. **Reflection handles resolved once in `Apply()`, stored as `private static` fields.** Never inline `AccessTools.Field/Method` inside a patch method. See [`docs/dev/patterns/harmony-patching.md`](docs/dev/patterns/harmony-patching.md).
3. **Per-instance patch state uses `ConditionalWeakTable<TBehaviour, TProxy>`.** Never a static dictionary keyed by `NetworkId`. See [`docs/dev/patterns/proxify.md`](docs/dev/patterns/proxify.md).
4. **`IsServer` guard before any damage/state write, at collection level, not per-item.** See [`docs/dev/patterns/networking.md`](docs/dev/patterns/networking.md).
5. **After any C# edit: `pnpm run lint:cs:fix` then `pnpm run build`.**
6. **Never `git commit`, `pnpm run deploy`, or `pnpm run release` unless the user explicitly asks.** Do not stage, commit, or deploy as a side-effect of completing a task.
7. **Before any implementation task, read the relevant pattern docs first.** For any C# mod work: read `docs/dev/patterns/` (harmony-patching.md, patch-extraction.md, proxify.md, networking.md, state.md). For ThePit work: also read the ThePit-specific docs in `docs/dev/ThePit/`. Only go to `game-src/` after understanding the patterns.

## Harmony patches

- `AccessTools.Field` / `AccessTools.Method` for private member access
- Use `prefix` to intercept/block, `postfix` to observe/modify after
- Warn and bail per reflection handle — independent null-checks, never a combined gate

## Code style

- All C# must respect `.editorconfig` rules (indent 4 spaces, braces K&R style, explicit access modifiers, etc.)
- After any C# edit run `pnpm run lint:cs:fix` to auto-fix formatting, then verify with `pnpm run build`

## Patterns reference

Focused pattern docs (with code examples):

| Topic | File |
|---|---|
| Harmony patching mechanics, `Apply()`, naming, coroutines | [`docs/dev/patterns/harmony-patching.md`](docs/dev/patterns/harmony-patching.md) |
| Patch extraction — Controllers, Orchestrators, one-liner rule | [`docs/dev/patterns/patch-extraction.md`](docs/dev/patterns/patch-extraction.md) |
| Proxify pattern — `ConditionalWeakTable`, `PvpActorColliderDetector` | [`docs/dev/patterns/proxify.md`](docs/dev/patterns/proxify.md) |
| Networking — `IsServer`, Fusion host mode, `PlayerManager` | [`docs/dev/patterns/networking.md`](docs/dev/patterns/networking.md) |
| State management, `IModRegistrant`, `libs/` boundary | [`docs/dev/patterns/state.md`](docs/dev/patterns/state.md) |

ThePit-specific:

| Topic | File |
|---|---|
| Ability PvP coverage — which pattern to use per ability type | [`docs/dev/ThePit/abilities.md`](docs/dev/ThePit/abilities.md) |
| Arena systems — respawn, timer, grace period | [`docs/dev/ThePit/arena-systems.md`](docs/dev/ThePit/arena-systems.md) |
| Terminology — Beta/Draft/Moba variant naming | [`docs/dev/ThePit/terminology.md`](docs/dev/ThePit/terminology.md) |

## Localization

Translation files live at `Assets/Localization/[ModName].[lang].json` (e.g. `PerfectDodge.en.json`). Format: flat `{"key": "value"}` JSON.

The localization class scans the plugin's `Assets/Localization/` directory at startup and loads all matching files. English is the fallback. Locale code comes from `AppManager.Instance?.PlayerSettings?.Gen_Language`.

`DataContractJsonSerializer` requires `UseSimpleDictionaryFormat = true` to parse flat JSON objects — without it, it expects `[{Key, Value}]` pairs and silently fails.

## Releases

Two-step flow:

1. `pre-release` — bumps `Version` constant in `[ModName]Mod.cs`, writes `mods/[ModName]/CHANGELOG.md` from git log. **No commit.** Review and edit `CHANGELOG.md` before proceeding.
2. `release` — validates dirty files (only version file + CHANGELOG.md may be dirty), stages and commits both, packages ZIP, creates git tag, pushes, creates GitHub release via `@octokit/rest`.

- Tag format: `[ModName]-v[Version]`
- ZIP structure: `plugins/fantastic-jam-[ModName]/`
- Changelog source: `mods/[ModName]/CHANGELOG.md` if present, otherwise auto-generated from `git log --grep=[ModName]`
- **Commit messages must contain the mod name** for changelog filtering to work
- Use `--dry-run` on `release` to preview the full plan without modifying anything

## Adding a mod

Look at [`docs/dev/create_mod.md`](docs/dev/create_mod.md)
