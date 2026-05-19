# OfflineMode — Ghoulag Update

## Decision: REMOVE this mod

The Ghoulag Update added official native Offline Mode to Raiders of Blackveil. This makes the OfflineMode mod redundant. All BackendManager methods it patched are also gone from the new game build:
- `StartAsyncValidateGameRelease` — gone
- `BuildCustomFusionAppSetting` — moved into Fusion integration layer  
- `StartPlaySession` — renamed/replaced
- `BackendManager.State` property setter — gone

The mod was already dead in the new version (all reflection handles returning null, all patches inactive). Keeping it causes log noise and confusion.

## Steps
1. Remove `OfflineMode` project from the solution:
   - In `mods/raiders-of-blackveil-mods.sln`, find and remove the two lines for OfflineMode:
     ```
     Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "OfflineMode", "OfflineMode\OfflineMode.csproj", "{D5F3A8C1-2E97-4B0F-C6D8-9A1B3E5F7D29}"
     EndProject
     ```
   - Also remove any `GlobalSection` entries referencing that GUID.
2. Delete the entire `mods/OfflineMode/` directory.
3. Run `pnpm run build` to confirm the solution builds without it.
4. Commit: use `git rm -r mods/OfflineMode/` then `git add mods/raiders-of-blackveil-mods.sln` and commit with a conventional message scoped to OfflineMode.  
   Note: `fcommit` / `fchange` target packages by folder name — since the folder is deleted, do the commit manually:
   ```
   git commit -m "removed(OfflineMode): retire mod — game added native offline mode in Ghoulag Update"
   ```
5. Open PR to `feat/ghoulag-update`
