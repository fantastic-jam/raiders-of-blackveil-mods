## Raiders of Blackveil mods

- [BeginnersWelcome](mods/BeginnersWelcome/README.md)
- [CheatManager](mods/CheatManager/README.md)
- [DisableSkillsBar](mods/DisableSkillsBar/README.md)
- [HandyPurse](mods/HandyPurse/README.md)
- [JoinAnytime](mods/JoinAnytime/README.md)
- [OfflineMode](mods/OfflineMode/README.md)
- [PerfectDodge](mods/PerfectDodge/README.md)
- [PlayerNameFix](mods/PlayerNameFix/README.md)
- [RaiderRoughPatches](mods/RaiderRoughPatches/README.md)
- [RogueRun](mods/RogueRun/README.md)
- [ThePit](mods/ThePit/README.md)
- [WildguardModFramework](mods/WildguardModFramework/README.md)

## Libraries

- [ModRegistry](libs/ModRegistry/README.md) — optional `IModRegistrant` interface + `ModType` enum for mod integration

## Setup

Requires Node.js 22+ and .NET SDK 9+.

```bash
pnpm install
pnpm run setup        # prompts for game path, downloads BepInEx 5, copies game DLLs
```

`setup` writes `mods/UserPaths.props` with your local game path (gitignored) and downloads BepInEx 5 into `bepinex/` (gitignored).

To publish GitHub releases, create a `.env` from the template and add your token:

```bash
cp .env.template .env   # then edit .env and fill in GITHUB_TOKEN
```

## Build

```bash
pnpm run build        # dotnet build — all mods, Release config
```

## Deploy (local game)

```bash
pnpm run deploy -- --mod DisableSkillsBar
pnpm run deploy -- --all
```

Builds the solution and copies the DLL, `Assets/` folder, and config templates into the game's BepInEx plugin folder. Existing config files are preserved.

## Release

Two-step flow — one mod or lib at a time:

```bash
pnpm run pre-release -- --mod PerfectDodge          # bump version from CHANGELOG (no commit)
pnpm run pre-release -- --mod PerfectDodge --dry-run # preview version bump only

pnpm run release -- --mod PerfectDodge              # commit, tag, push, GitHub release
pnpm run release -- --mod PerfectDodge --dry-run    # preview full plan without modifying anything
pnpm run release -- --mod PerfectDodge --skip-push --skip-release  # local only

pnpm run pre-release -- --lib ModRegistry           # same flow for libs
pnpm run release -- --lib ModRegistry
```

`pre-release` calls `frelease --pkg <name>`, which promotes `## [Unreleased]` to a versioned heading in `CHANGELOG.md` (bump level = highest entry type: major > minor > patch) and writes the new version into the `Version` constant in source. Review `CHANGELOG.md` before running `release`.

`release` validates dirty files (only version file + CHANGELOG.md may be dirty), stages and commits both, builds, packages ZIP, creates git tag, pushes, and creates a GitHub release via `@octokit/rest`. Requires `GITHUB_TOKEN` in `.env` (see Setup).

Package only (no git/release):

```bash
pnpm run package -- --mod PerfectDodge
```

## Tag format

```
<ModName>-v<Version>     e.g.  PerfectDodge-v0.1.2
```

## ZIP structure

```
plugins/fantastic-jam-<ModName>/
  <ModName>.dll
  Assets/                 (if present — copied verbatim from build output)
  README.md               (if present)
```

## Commit message format

Enforced by Husky commit-msg hook:

```
fix|chore|new(<scope>): message

<scope>  →  Repo | All | <ModName>
```

Examples: `fix(PerfectDodge): handle null stats`, `chore(Repo): update deps`

## Nexus Mods publishing

Triggered automatically on GitHub release. Each mod's `mods/<ModName>/metadata.json` must contain `nexus_mod_id` and `nexus_file_group_id`. Omit the file to skip Nexus publishing for that mod.
