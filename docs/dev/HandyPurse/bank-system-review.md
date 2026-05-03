# HandyPurse Bank/Topup System — Code Review

Ten independent agents (two passes of five) reviewed the bank and topup system across five dimensions. Results compiled 2026-05-03.

**Files reviewed:**
- `mods/HandyPurse/Bank/BankOrchestrator.cs`
- `mods/HandyPurse/Bank/BankLogic.cs`
- `mods/HandyPurse/Bank/PurseBank.cs`
- `mods/HandyPurse/Patch/HandyPursePatch.cs`
- `tests/HandyPurse.Tests/BankLogicTests.cs`
- `tests/HandyPurse.Tests/PurseBankTests.cs`

---

## Logic & Correctness Review

### 1. Client-path topup files never deleted after deposit — HIGH (bank duplication)

The **host path** correctly retains topup files across loads. The cloud save stores clamped amounts, so every load of the same save — including reloads, disconnects/reconnects, and save reverts — needs the topup re-applied. Deleting on first load would break those cases. Cleanup of stale files is deferred to a future purge task (by design).

The **client path is a real bug.** `OnJoinedSession()` (lines 112–137) deposits topup excess to the bank but never calls `PurseBank.DeleteTopupSave()`. `LobbyHUDPage.OnActivate` fires on every lobby visit. Each subsequent visit as a client runs `OnJoinedSession` again, reads the same files, and deposits the same excess a second time. Each lobby visit as a client multiplies bank balances.

**Fix:** In `OnJoinedSession`, call `PurseBank.DeleteTopupSave(save.Timestamp)` for each file after a successful deposit.

### 2. Hash invariant asserted in comments but never enforced — MEDIUM (latent wrong-slot restore)

`BankLogic.cs` line 63–65 states: "If the hash matched, the slot layout is guaranteed to be identical to the save — no error path needed." But `FindTopupSave` looks up files by `Timestamp` alone. `TopupSave` has no `SaveHash` field, and `ApplyTopupToState` performs no hash comparison. The hash computed in `ComputeExcess` is discarded with `_` at every call site.

If the game ever reorders the `Items` list between save and load (game update, champion slot insertion), `ApplyTopup` will add excess to a different item with no error. The bounds check (`idx >= slots.Count`) only catches out-of-range — not a valid index pointing to a different currency.

### 3. Silent loss when `SlotIndex` is invalid — MEDIUM

`BankLogic.ApplyTopup()` (lines 70–71) silently skips entries whose `SlotIndex` is null or out-of-bounds with `continue`. The caller (`ApplyCompartmentTopup`) cannot detect a partial application. Excess is discarded with no notification and no bank deposit fallback.

**Fix:** Return the count of unresolved entries; deposit them to bank in `ApplyCompartmentTopup`.

### 4. `_saveRestore` window if postfix is skipped — MEDIUM

If an exception is thrown inside the original `SavePlayerGameStates` body (between prefix and postfix), the live `GenericItemDescriptor.Amount` fields remain at their clamped values for the rest of the session. The player sees their inventory drop to vanilla cap with no explanation. The disk state is correct (topup file was written); the live state is wrong until the next reload.

The field is cleared at the top of the next `OnPlayerProfileSave` call (line 21), so stale data does not carry across sessions, but the current session's inventory is broken.

### 5. `LocalPlayer.ProfileUUID` unavailable on early saves — MEDIUM

`OnPlayerProfileSave()` (lines 26–30) logs a warning and returns if `ProfileUUID` is empty. There is no retry. If the save fires during an initialization window before the local player is fully loaded, the excess is clamped into the cloud save permanently with no topup file written.

### 6. Multi-wrap on `LoadPlayerGameState` retries — LOW (latent)

`BackendManager.LoadPlayerGameState` is a coroutine that retries up to `retryAttempts` times. Each retry call passes through the `LoadPlayerGameStatePrefix` again, wrapping the already-wrapped callback in a new lambda. On a 5-retry path, `ApplyTopupToState` is in a chain of 5 nested closures. In practice, `callback(uuid, state)` only fires on success so `ApplyTopupToState` is called once — but the nested lambda chain is unnecessary overhead and would become a bug if the file is deleted on first call and the retry fires again.

### Summary

| Issue | Severity |
|-------|----------|
| Client-path topup files never deleted after bank deposit | HIGH |
| Hash invariant claimed but unenforced (wrong-slot restore) | MEDIUM |
| Silent loss on invalid `SlotIndex` | MEDIUM |
| Live amounts stay clamped if postfix skipped | MEDIUM |
| No UUID retry on early save | MEDIUM |
| Multi-wrap on retries | LOW |

---

## Harmony Patching Review

### 1. Patch point and serialization timing — CORRECT

`PlayerProfile.SavePlayerGameStates` (game-src line 55) is the right hook. `JsonConvert.SerializeObject(requestSave)` is line 57 — it runs synchronously after the prefix, capturing the already-clamped amounts. The postfix restores live amounts after serialization has finished. The timing is correct for both the network path and the `Dev_PlayerGameStateStorageOnHost` branch (both paths operate on the same live `GenericItemDescriptor` object references, so the clamp/restore bracket applies equally).

### 2. `requestSave` parameter binding — CORRECT

The prefix declares `IngressMessagePlayerSaveGameStates requestSave`. Harmony 2 binds by name; the game method names this parameter `requestSave` (PlayerProfile.cs line 55). Binding is valid. The `requestSave?.data?.player_game_states` null-chain is harmless defensive coding.

### 3. Combined null-check for two reflection handles — RULE VIOLATION

`HandyPursePatch.cs` lines 66–74:

```csharp
_itemsArrayField = AccessTools.Field(typeof(InventorySyncedItems), "_itemsArray");
_syncedItemsField = AccessTools.Field(typeof(Inventory), "_syncedItems");
if (_itemsArrayField == null || _syncedItemsField == null) {
    HandyPurseMod.PublicLogger.LogWarning("...Could not find _itemsArray or _syncedItems...");
}
```

`harmony-patching.md` explicitly prohibits combining two handles into one `||` guard. If one resolves and the other does not, the combined warning fires once with no indication of which field is missing, and `MergeToInventoryPrefix` is not applied at all. Both handles need independent null-checks with targeted log messages.

### 4. Patch method visibility — RULE VIOLATION

`AmountMaximumPostfix`, `CreateItemPrefix`, `CreateItemPostfix`, and `MergeToInventoryPrefix` are declared `public static`. Per codebase rules, patch methods must be `private static`.

### 5. Four patch methods with logic bodies — RULE VIOLATION

Same four methods carry business logic rather than being one-liners:
- `AmountMaximumPostfix` (lines 158–171): stash ownership resolution, champion type comparison, cap lookup
- `CreateItemPrefix` (lines 184–204): asset lookup, `useStackLimit` guard, cap application
- `CreateItemPostfix` (lines 207–224): descriptor mutation
- `MergeToInventoryPrefix` (lines 228–272): 45 lines containing the complete merge algorithm

All four violate `patch-extraction.md`. The logic belongs in an `InventoryOrchestrator`.

### 6. `WrapLoadCallback` ref wiring — CORRECT

`ref Action<Guid, PlayerGameState> callback` in a prefix is the documented Harmony pattern for delegate replacement. The closure captures `original` before reassignment. `original?.Invoke()` is null-safe. The wiring from patch to orchestrator passes `ref` through correctly per `patch-extraction.md` lines 53–63.

### 7. `[HarmonyPriority]` missing on critical patches — LATENT

The save-clamp pair (`PlayerProfileSavePlayerGameStatesPrefix`/`Postfix`) and `MergeToInventoryPrefix` (which returns `false` to suppress the original) have no `[HarmonyPriority]` decoration. In a multi-mod environment, execution order is non-deterministic. If another mod also patches these methods, amount reads or merge behavior could be wrong. `MergeToInventoryPrefix` suppressing the original is especially sensitive — another mod's patch may never run if HandyPurse fires first.

### Summary

| Finding | Verdict |
|---------|---------|
| Patch point and serialization timing | CORRECT |
| `requestSave` parameter binding | CORRECT |
| Combined `\|\|` null-check for two handles | RULE VIOLATION |
| Patch method visibility (`public` → `private`) | RULE VIOLATION |
| Four patch methods with logic bodies | RULE VIOLATION |
| `ref callback` wiring | CORRECT |
| Missing `[HarmonyPriority]` | LATENT RISK |

---

## Robustness & Error Handling Review

### 1. `SaveBank` destroys the bank file on crash — CRITICAL

`SaveBank()` (lines 194–197) calls `File.Create(BankPath)` which truncates the file immediately, then serializes into it. If the process crashes — or if the serializer throws — between truncation and flush, `bank.json` is left at zero bytes or partially written. On next start, `LoadBank` catches the parse exception and silently returns an empty `BankData`. **All banked currency is zeroed with no user-visible indication.**

**Fix:** Write to `bank.json.tmp`, then `File.Move` over `bank.json`. `Move` is atomic on NTFS on the same volume.

### 2. `WriteTopupSave` failure is invisible to the caller — CRITICAL

`WriteTopupSave` returns `void`. If the write fails, `Error(...)` is logged internally, but the caller (`BankOrchestrator.OnPlayerProfileSave`, line 67) has no way to detect the failure. The code proceeds: `_saveRestore` is set, the postfix restores live amounts, and the game continues normally — but no topup file was written. On next load, excess is permanently lost.

The `Error` delegate defaults to a no-op (`internal static Action<string> Error = _ => { }`) and is only wired in `HandyPurseMod.Awake`. If the delegate is ever not wired, the failure is truly silent.

**Fix:** Return `bool` from `WriteTopupSave`. If `false`, log a prominent in-game warning.

### 3. `NullReferenceException` if `Entries` deserializes as null — HIGH

`BankOrchestrator.ApplyCompartmentTopup()` line 179:
```csharp
if (compartment == null || compartment.Entries.Count == 0) { return; }
```
`DataContractJsonSerializer` uses `FormatterServices.GetUninitializedObject` — it bypasses constructors and field initializers. If the `Entries` field is absent from a JSON file (e.g., a file written by an older version), `Entries` will be null after deserialization and `.Count` will throw a `NullReferenceException`.

**Fix:** Add `compartment.Entries == null` to the guard, or use `[OnDeserializing]` to initialize lists.

### 4. Non-atomic bank write — HIGH

Same root as #1. `File.Create` truncates before write. A crash mid-write corrupts the file. See fix in #1.

### 5. `SaveBank` called unconditionally from `TryDeposit` — HIGH

`TryDeposit` always calls `SaveBank(bank)` even when the deposit list is empty. Calling `TryDeposit([])` creates a `bank.json` from nothing and writes a file with the current (possibly empty) bank contents. This is an unexpected side effect.

### 6. Per-file parse failures swallowed without filename context — MEDIUM

`GetAllTopupSaves()` line 110: `catch { }` — a corrupt topup file is silently skipped with no log entry. There is no way to diagnose which file failed.

**Fix:** `catch (Exception ex) { Warn($"HandyPurse: skipping corrupt topup file {file} — {ex.Message}"); }`

### 7. Double-deposit if lobby activates twice and deposit fails — MEDIUM

In `OnJoinedSession`, topup files are deposited but never deleted. If `TryDeposit` returns `false` and the player returns to the lobby (re-activating `LobbyHUDPage`), `OnJoinedSession` runs again on the same files and attempts another deposit. No failure notification is shown in either case.

### 8. `AppDomain.CurrentDomain.BaseDirectory` fallback is wrong — LOW

`PurseBank.DataDir` fallback uses `AppDomain.CurrentDomain.BaseDirectory`, which in BepInEx resolves to the game's root executable directory — not the plugin folder. The comment says "sibling of the executing assembly", which is incorrect. The fallback is effectively unreachable (overridden at startup via `OverrideDataDir`) but misleading.

### 9. Mutable `Warn`/`Error` no-op defaults — MEDIUM

`PurseBank.Warn` and `PurseBank.Error` default to `_ => { }`. If the delegate wiring in `HandyPurseMod.Awake` ever fails or is removed, all errors are silently swallowed. `BankOrchestrator` already calls `HandyPurseMod.PublicLogger` directly — `PurseBank` should do the same.

### Summary

| Issue | Severity |
|-------|----------|
| `SaveBank` truncates before write, destroys file on crash | CRITICAL |
| `WriteTopupSave` failure invisible to caller | CRITICAL |
| `NullReferenceException` on null `Entries` after deserialization | HIGH |
| Non-atomic bank write | HIGH |
| `TryDeposit([])` creates bank file as side effect | HIGH |
| Per-file failures swallowed without filename | MEDIUM |
| Double-deposit on repeated lobby visits after failed deposit | MEDIUM |
| Mutable `Warn`/`Error` no-op defaults | MEDIUM |
| `BaseDirectory` fallback resolves to wrong path | LOW |

---

## Architecture & Code Quality Review

### 1. Four patch methods with logic bodies — RULE VIOLATION (highest priority)

See Harmony review. `MergeToInventoryPrefix` alone is 45 lines of merge logic inside a patch method. All four must be extracted to an `InventoryOrchestrator`.

### 2. `ComputeHash` and the hash return value are dead code

`BankOrchestrator.CollectAndClampCompartment` (line 202) discards the hash with `var (entries, _) = BankLogic.ComputeExcess(...)`. That is the only call site. `ComputeHash` (lines 78–94) is therefore dead — it runs on every save that has excess (allocating a sorted list, a `StringBuilder`, concatenating strings) and the result is thrown away.

The stale documentation compounds this: the `<remarks>` block on `ComputeExcess` still describes hash-based file naming. The comment on `PurseBank.cs` line 56 still says "named by hash". The comment in `BankLogic.ApplyTopup` lines 63–65 says "if the hash matched, the slot layout is guaranteed" — but there is no hash check anywhere in the current code. These comments all refer to the pre-timestamp design and should be removed along with `ComputeHash`.

**Options:** Either remove `ComputeHash` and the hash tuple member entirely, or add a `SaveHash` field to `TopupSave`, store it on write, and verify it on load.

### 3. `IsServer` / `IsHost` defined twice

`BankOrchestrator.cs` line 16: `private static bool IsServer`. `HandyPursePatch.cs` line 30: `private static bool IsHost`. Identical expressions, different names. One should be the canonical definition.

### 4. Two parallel deferred-popup systems

`BankOrchestrator.PendingPopupText` is one popup accumulator. `HandyPursePatch` has its own `PendingBreakingChangePopup` / `PendingVersionMismatchPopup` booleans. Both represent "show a modal on next lobby activation." `MenuStartPageOnActivatePostfix` (HandyPursePatch.cs lines 119–137) manually sequences all three with early-return guards. This grows linearly with each new popup type added.

### 5. Currency type constants duplicated

`BankLogic` defines `TypeBlackCoin = 30`, `TypeBlackBlood = 31`, `TypeGlitter = 32`, `TypeScrap = 40` as raw int literals (lines 14–17). `HandyPursePatch.ResolveCap` uses `ItemType.Scrap`, `ItemType.BlackCoin`, etc. from the game enum directly. If the game ever renumbers the enum, `BankLogic`'s constants silently diverge. The two representations of the same currency set should share a single source.

### 6. `FindBankEntry` is `internal` with one internal caller

`FindBankEntry` is only called from `TryDeposit` inside `PurseBank`. It should be `private`.

### 7. `TypeBlackCoin` etc. are `internal const` with no external callers

Same issue — `BankLogic`'s type constants have no callers outside `BankLogic`. They should be `private const`.

### 8. `CollectAndClampCompartment` adds empty entry for null items — dead output

Lines 196–198: when `items` is null, a `(key, empty list)` pair is added to `allEntries`. This pair never contributes to `hasExcess` and is filtered out before the `TopupSave` is constructed. The early return can simply `return` without adding anything.

### 9. `TopupEntry.SlotIndex` nullable without documentation

`SlotIndex` is `int?` and always set at construction (`SlotIndex = i`). The nullability appears to handle absent-field deserialization from older file versions — but this is not documented. Either add a comment explaining the deserialization reason, or change to `int` if backward compatibility is not a concern.

### Priority order for fixes

1. Extract logic from four oversized patch methods → `InventoryOrchestrator`
2. Remove or use `ComputeHash` (dead computation on every save)
3. Consolidate `IsServer`/`IsHost` to one definition
4. Replace mutable `Warn`/`Error` with direct logger calls
5. Make `FindBankEntry` and the type constants `private`
6. Document or change nullable `SlotIndex`

---

## Test Coverage Review

### 1. `BankOrchestrator` entirely untested

`OnPlayerProfileSave`, `OnPlayerProfileSaveComplete`, `ApplyTopupToState`, `OnJoinedSession`, `AppendPendingPopup`, `ShowPendingPopup` have zero tests. Several are reachable without game-engine mocks via `PurseBank.OverrideDataDir` and the static `Disabled` / `IsServer` guards. High-value missing tests:

- Save with over-cap currency → topup file written
- Save with under-cap currency → no file written
- Postfix restores live amounts after save
- `OnJoinedSession` with pending files deposits to bank and (if fixed) deletes files
- `AppendPendingPopup` accumulates with `\n\n` separator; `ShowPendingPopup` clears the text

### 2. `ApplyTopup` with null or out-of-bounds `SlotIndex` — untested

The `entry.SlotIndex ?? -1` and `idx >= slots.Count` guards exist but are never exercised. Both defensive paths silently discard excess. Missing:

- `ApplyTopup_NullSlotIndex_EntrySkipped`
- `ApplyTopup_OutOfBoundsSlotIndex_EntrySkipped`

### 3. No test for `OnJoinedSession` double-deposit bug

There is no test that calls `OnJoinedSession` twice with the same files on disk and verifies bank balances are not doubled. Missing:

- `OnJoinedSession_CalledTwice_NoDuplicateDeposit`

### 4. `GetAllTopupSaves` with a corrupt file — untested

The per-file `catch { }` is never exercised. Missing:

- `GetAllTopupSaves_CorruptFile_SkippedAndValidOnesReturned` — write one valid file and one with garbage bytes; assert count is 1 and the valid entry is returned.

### 5. `WriteTopupSave` with same timestamp — untested

`File.Create` overwrites on same filename. Missing:

- `WriteTopupSave_SameTimestamp_SecondOverwrites`

### 6. `DeleteTopupSave` on non-existent file — untested

The `File.Exists` guard covers the no-throw case but there is no test for it. Missing:

- `DeleteTopupSave_FileDoesNotExist_NoException`

### 7. `TryDeposit` with empty list — untested

Calling `TryDeposit([])` creates `bank.json` as a side effect. Missing:

- `TryDeposit_EmptyList_BankFileUnchanged`

### 8. `BlackBlood` currency not tested

`BankLogic` defines `TypeBlackBlood = 31` but no test uses it.

### 9. `ComputeHash` on empty slot list — untested

No test pins the contract that `ComputeHash([])` returns `""`.

### 10. `FindBankEntry` only indirectly tested

The not-found path and the empty-bank path have no direct tests.

### 11. Weak assertions in existing tests

- `ComputeExcess_MixedSlots_OnlyAboveCapStripped` discards the hash with `_` and never asserts it is non-empty despite having two above-cap currencies.
- `WriteAndFindTopupSave_RoundTrip` does not assert the file exists on disk between write and read — a silent `WriteTopupSave` failure would still fail the test via `Assert.NotNull`, but the failure mode would be obscure.

### Summary: missing tests by risk level

| Risk | Missing Test |
|------|-------------|
| CRITICAL | `OnJoinedSession` called twice → bank not doubled |
| CRITICAL | `ApplyTopup` with invalid `SlotIndex` |
| HIGH | `BankOrchestrator` save-hook integration |
| HIGH | `OnJoinedSession` deposit integration |
| HIGH | `GetAllTopupSaves` with one corrupt file |
| MEDIUM | `BlackBlood` in all currency tests |
| MEDIUM | `WriteTopupSave` same-timestamp overwrite |
| MEDIUM | `TryDeposit` with empty list |
| LOW | `DeleteTopupSave` on missing file |
| LOW | `ComputeHash([])` returns empty string |
| LOW | `FindBankEntry` direct coverage |
