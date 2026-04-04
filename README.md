## Raiders of Blackveil mods

- [BeginnersWelcome](mods/BeginnersWelcome/README.md)
- [CheatManager](mods/CheatManager/README.md)
- [DisableSkillsBar](mods/DisableSkillsBar/README.md)
- [HandyPurse](mods/HandyPurse/README.md)
- [PerfectDodge](mods/PerfectDodge/README.md)

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

```bash
pnpm run release -- --mod PerfectDodge --bump patch       # auto-increment version
pnpm run release -- --mod PerfectDodge --version 1.2.3    # explicit version
pnpm run release -- --mod PerfectDodge --bump minor --dry-run   # preview only
pnpm run release -- --all --bump patch                    # release all mods
```

Full pipeline: bumps `Version` constant in source → builds → packages ZIP → `git commit` + tag → push → GitHub release + asset upload (via `@octokit/rest`). Requires `GITHUB_TOKEN` in `.env` (see Setup).

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
