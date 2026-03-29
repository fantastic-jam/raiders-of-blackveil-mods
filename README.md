## Raiders of Blackveil mods

* [DisableSkillsBar](DisableSkillsBar/README.md)
* [HandyPurse](HandyPurse/README.md)

## How to compile

* Make sure that you have Raiders of Blackveil installed with [BepInEx](https://github.com/BepInEx/BepInEx/)
* Copy the `UserPaths.props.template` file to `UserPaths.props`
* Edit the `RaidersOfBlackveilRootPath` in `UserPaths.props`

## Per-mod releases (local script)

Release one mod locally with:

* `./release-mod.ps1 -ModName "HandyPurse" -Version "1.0.0"`
* `release-mod.bat -ModName HandyPurse -Version 1.0.0`

Build package only with:

* `./.github/scripts/package-mod.ps1 -ModName "HandyPurse"`
* `package-mod.bat -ModName HandyPurse`

`package-mod` builds and packages the selected mod using the current `Version` value already present in the mod source.

`release-mod` is a one-shot release command intended to run from a clean `main` branch checkout. It updates the mod source version, builds and packages from that updated source, commits the version bump if needed, creates the local git tag, pushes branch and tag, creates the GitHub release, and uploads the ZIP asset.

Tag format:

* `<ModName>-v<Version>`

Examples:

* `HandyPurse-v1.0.0`
* `DisableSkillsBar-v0.4.1`

The uploaded ZIP follows a game-root-ready structure used by many Nexus/BepInEx mods:

* `BepInEx/plugins/fantastic-jam-<ModName>/<ModName>.dll`
* `BepInEx/config/*.cfg` (included when the mod has tracked config files)

Version behavior:

* `package-mod` uses whatever version is already in the mod source for both compilation and ZIP naming.
* `release-mod` writes the requested version into the mod source before building the package. If packaging fails, the source file is restored before the script exits.
