# Release Workflow

Practical reference for the release-manager agent. Read this before running any release step.

---

## Overview

Releases follow a strict two-step sequence:

1. **`pre-release`** — promotes `[Unreleased]` in CHANGELOG.md, bumps version in source. No commit.
2. **`release`** — stages + commits those two files, packages ZIP, tags, pushes, creates GitHub release.

Everything else (implementation changes, tooling changes, pnpm-lock.yaml) must be committed **before** step 1.

---

## Tools quick reference

### `fchange`

Prepends a changelog entry to `mods/[ModName]/CHANGELOG.md` under the `[Unreleased]` section.

```
fchange <type> "message" --pkg <ModName>
```

Types (Keep a Changelog): `added`, `changed`, `deprecated`, `removed`, `fixed`, `security`

These types are ONLY for `fchange`. Do not use them with `fcommit`.

### `fcommit`

Creates a conventional commit scoped to the mod.

```
fcommit <type> "message" --pkg <ModName>
```

Types (conventional commits): `feat`, `fix`, `chore`, `docs`, `refactor`, `ci`

These types are ONLY for `fcommit`. Do not use them with `fchange`.

### `frelease`

Called internally by `pre-release`. Do not run directly.

```
frelease --pkg <ModName> --dry-run   # safe preview: shows what version would be released
frelease --pkg <ModName>             # promotes [Unreleased] + writes version — use via pre-release only
frelease --pkg <ModName> changelog   # prints release notes for the current version — called by release internally
```

**Never run `frelease --pkg <ModName>` directly.** It modifies CHANGELOG.md without updating the version file via the pre-release validation path, leaving state inconsistent.

---

## Version bumping rules (handled by `frelease`)

`frelease` determines the version bump from the highest-impact `fchange` type present under `[Unreleased]`:

| fchange types present | Bump |
|---|---|
| `added` or `changed` | minor |
| `fixed`, `deprecated`, `removed`, `security` only | patch |

To override, pass `--version x.y.z` or `--bump patch|minor|major` to `pre-release`.

---

## Where versions live

| Project kind | Version file | Pattern matched |
|---|---|---|
| Mod | `mods/[ModName]/[ModName]Mod.cs` | `Version = "x.y.z"` constant |
| Lib | `libs/[LibName]/Properties/AssemblyInfo.cs` | `AssemblyVersion("x.y.z")` |

`pre-release` writes the new version to this file. `release` reads it back.

---

## Release artifacts

- **ZIP asset**: `dist/[ModName]-[Version].zip`
- **ZIP internal structure**: `plugins/fantastic-jam-[ModName]/`
- **Git tag format**: `[ModName]-v[Version]` (annotated tag)
- **Commit message**: `chore([ModName]): release v[Version]`
- **Release notes source**: `mods/[ModName]/CHANGELOG.md` — extracted by `frelease --pkg <ModName> changelog`

---

## Step-by-step release procedure

### 1. Commit all implementation changes

All dirty files (C#, assets, tooling, pnpm-lock.yaml) must be committed before pre-release. Use `fcommit`, not `git commit`.

```
fcommit feat "add bank drain on join" --pkg HandyPurse
```

Do NOT commit `CHANGELOG.md` or the version file — those are owned by pre-release.

### 2. Record changelog entries

Each meaningful change should already have an `fchange` entry. If anything is missing, add it now:

```
fchange added "Added bank that drains excess purse stacks on join" --pkg HandyPurse
fchange fixed "Fixed crash when leaving mid-run with a full purse" --pkg HandyPurse
```

### 3. Verify nothing unexpected is dirty

```
git status
```

The only acceptable dirty file at this point is `mods/[ModName]/CHANGELOG.md` (if you just ran `fchange`). The version file must be clean — if it is dirty, revert it before proceeding.

### 4. Run pre-release

```
pnpm run pre-release -- --mod <ModName>
```

What this does internally:

1. Runs `frelease --pkg <ModName>`, which:
   - Promotes the `## [Unreleased]` heading to `## [x.y.z] — YYYY-MM-DD`
   - Determines the version bump from `fchange` types
   - Prints the new version string
2. Validates the version string matches `x.y.z`
3. Checks that neither the local nor remote tag `[ModName]-vX.Y.Z` already exists
4. Reads the current version from the source file, writes the new version

After this step, exactly two files are dirty:
- `mods/[ModName]/CHANGELOG.md`
- `mods/[ModName]/[ModName]Mod.cs` (or the lib's AssemblyInfo.cs)

### 5. Preview what will be released (optional but recommended)

```
pnpm run release -- --mod <ModName> --dry-run
```

This prints the full release plan — version, tag, branch, asset path, steps, and release notes — without modifying anything. Read it carefully.

You can also preview the version bump without running pre-release:

```
pnpm run pre-release -- --mod <ModName> --dry-run
```

### 6. Validate CHANGELOG.md

Open `mods/[ModName]/CHANGELOG.md` and confirm:
- The version heading is correct
- The entries are accurate and player-facing
- There is no leftover `[Unreleased]` section with stale content

Do not edit the file manually. If an entry is wrong, there is no safe fix short of reverting and re-running with corrected `fchange` entries.

### 7. Run release

```
pnpm run release -- --mod <ModName>
```

Requires `GITHUB_TOKEN` in `.env` (copy from `.env.template`).

What this does:

1. Validates you are on `main`
2. Validates dirty files — **only** the version file and CHANGELOG.md for the mod being released may be dirty; anything else blocks the release
3. Reads the version from the source file
4. Checks local and remote tags do not already exist
5. Checks CHANGELOG.md exists
6. Runs `frelease --pkg <ModName> changelog` to extract release notes
7. Runs `package.mts` to build and ZIP the mod
8. `git add` version file + CHANGELOG.md
9. `git commit -m "chore([ModName]): release v[Version]"`
10. `git tag -a [ModName]-v[Version] -m [ModName]-v[Version]`
11. `git push origin main`
12. `git push origin [ModName]-v[Version]`
13. Creates GitHub release with the ZIP as the upload asset

### 8. Verify

Check GitHub releases and confirm the tag, title, notes, and asset are correct.

---

## Flags reference

| Flag | Applies to | Effect |
|---|---|---|
| `--dry-run` | `pre-release`, `release` | Preview only, no writes |
| `--skip-push` | `release` | Skip `git push` (also forces `--skip-release`) |
| `--skip-release` | `release` | Skip GitHub release creation |
| `--bump patch\|minor\|major` | `pre-release` | Override auto-detected bump |
| `--version x.y.z` | `pre-release` | Pin exact version instead of bumping |
| `--all` | both | Run for every mod and lib in the repo |
| `--lib <name>` | both | Target a lib instead of a mod |

`--skip-push` and `--skip-release` together are the equivalent of a local-only release:

```
pnpm run release -- --mod <ModName> --skip-push --skip-release
```

---

## Dirty file rules

The release script calls `git status --porcelain` and rejects any path not in the allowed set. The allowed set is:

- `mods/[ModName]/[ModName]Mod.cs` (the version file)
- `mods/[ModName]/CHANGELOG.md`

If any other file is dirty the release exits immediately with an error. Commit or stash everything else first.

---

## Common mistakes and how to avoid them

| Mistake | Consequence | Fix |
|---|---|---|
| Running `frelease` directly | CHANGELOG.md promoted but version file unchanged | Revert CHANGELOG.md with `git checkout`, then use `pre-release` |
| Manually editing CHANGELOG.md | Format may break `frelease` parsing | Use `fchange` only |
| Manually bumping the version | `pre-release` will bump again from the wrong baseline | Revert version file, then run `pre-release` |
| Dirty files at release time | Release blocked | Commit or stash, then re-run release |
| Not on `main` | Release blocked | Checkout main |
| Missing GITHUB_TOKEN | Release blocked | Copy `.env.template` to `.env`, fill in token |
| Using `fchange` types in `fcommit` | Commit message rejected by validate-commit-msg hook | Use conventional commit types: feat, fix, chore, docs, refactor, ci |
| Using `fcommit` types in `fchange` | Bump logic may not classify the entry correctly | Use Keep a Changelog types: added, changed, fixed, etc. |

---

## Releasing a lib

Same flow, using `--lib` instead of `--mod`:

```
pnpm run pre-release -- --lib <LibName>
pnpm run release -- --lib <LibName>
```

Version file for libs is `libs/[LibName]/Properties/AssemblyInfo.cs`. Both `AssemblyVersion` and `AssemblyFileVersion` are updated.

---

## Releasing everything at once

```
pnpm run pre-release -- --all
pnpm run release -- --all
```

This iterates all non-deprecated mods and all libs. Each must have an `[Unreleased]` section with at least one `fchange` entry, or `frelease` will have nothing to promote.
