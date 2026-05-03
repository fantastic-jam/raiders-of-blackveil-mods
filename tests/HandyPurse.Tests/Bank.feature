Feature: HandyPurse bank / topup

  HandyPurse hooks PlayerProfile.SavePlayerGameStates (synchronous, not a coroutine).
  The prefix clamps managed currencies to vanilla caps before JsonConvert serialises
  the request; the postfix restores original amounts so the live inventory is unaffected.
  The cloud therefore ALWAYS stores clamped (vanilla-cap) amounts.

  A per-save topup file named topup-{date}-{sanitizedHash}.json records the excess.
  The saveHash is ComputeHash of all clamped managed slots across all compartments —
  the same hash the cloud will return on load.  On load, if the hashes match the file
  is found and excess is restored; if the inventory layout changed, excess goes to the
  local bank for manual recovery.


  # ── Save / ProcessSave ──────────────────────────────────────────────────

  Scenario: Player with over-cap coins saves — topup file written with excess
    Given the player has 9999 Scrap (vanilla cap 3000)
    When  PlayerProfile.SavePlayerGameStates fires
    Then  a topup-{date}-{hash}.json file is written
    And   the file records Excess = 6999 for Scrap
    And   the file's SaveHash equals ComputeHash( [Scrap: 3000] ) (post-clamp)
    And   the cloud receives 3000 Scrap (clamped)
    And   the live inventory is restored to 9999 Scrap after the postfix

  Scenario: Player with under-cap coins saves — no topup written
    Given the player has 1500 Scrap (vanilla cap 3000)
    When  PlayerProfile.SavePlayerGameStates fires
    Then  no topup file is written

  Scenario: Player had excess last session but spent down below cap before saving
    Given a topup file exists from a previous session
    And   the player now has 2000 Scrap (below cap)
    When  PlayerProfile.SavePlayerGameStates fires
    Then  no new topup file is written for this save
    And   the old topup file is not deleted (it stays for potential load recovery)

  Scenario: Save hook ignores other players — only local player topup is written
    Given a two-player session where only the local player has over-cap Scrap
    When  PlayerProfile.SavePlayerGameStates fires with both PlayerGameState entries
    Then  only the local player's topup file is written
    And   the remote player's amounts in the save payload are unchanged

  Scenario: LocalPlayer UUID not yet resolved when save fires
    Given PlayerManager.LocalPlayer is null when SavePlayerGameStates fires
    Then  a warning is logged
    And   no topup file is written
    And   amounts are not clamped


  # ── Load / ApplyTopup ───────────────────────────────────────────────────

  Scenario: Normal round-trip — cloud returns clamped amounts, saveHash matches
    Given a topup file exists with SaveHash = hash([Scrap: 3000]) and Excess = 6999
    When  the backend loads the local player's state with Scrap = 3000
    Then  loadHash matches the topup file's SaveHash
    And   ApplyTopup restores Scrap to 9999 in the loaded state
    And   no coins are deposited to bank
    And   the topup file is deleted

  Scenario: Inventory layout changed since last save — excess goes to bank
    Given a topup file records Excess = 6999 for Scrap at SlotIndex = 0
    And   the loaded state has Scrap at a different slot index (layout changed)
    When  ApplyTopup runs
    Then  ApplyTopup returns LayoutChanged
    And   6999 Scrap is deposited to bank.json
    And   a "layout changed" popup is queued
    And   the topup file is deleted

  Scenario: Validation load is skipped — topup is not consumed
    Given a valid topup file exists
    When  LoadPlayerGameState fires with initiatedByClient = true
    Then  the callback is NOT wrapped
    And   the topup file is unchanged

  Scenario: No topup on file — load proceeds without side-effects
    Given no topup files exist
    When  the backend loads the local player's state
    Then  no bank deposit occurs
    And   the player's loaded amounts are unchanged

  Scenario: Save revert — older cloud save matches an older topup file
    Given the player saved twice: first with Scrap=9999, then with Scrap=5000
    And   two topup files exist: hash([Scrap:3000]) and hash([Scrap:3000]) with different dates
    When  the cloud returns the reverted save (Scrap = 3000 from first save)
    Then  the topup file matching hash([Scrap:3000]) is found and applied
    And   Scrap is restored to 9999

  Scenario: LocalPlayer UUID not yet resolved when load callback fires
    Given PlayerManager.LocalPlayer is null when the load callback executes
    Then  ApplyTopup is NOT called for that state
    And   the topup file is not modified


  # ── Client join (not server) ─────────────────────────────────────────────

  Scenario: Player joins as client with pending topup — topup deposited to bank
    Given the player has a topup file with Excess = 6999 Scrap
    And   the player joins a session as a non-host (IsServer = false)
    When  LobbyHUDPage.OnActivate fires
    Then  6999 Scrap is deposited to bank.json
    And   a "clamping environment" popup is queued
    And   the topup file is deleted

  Scenario: Player joins as host with pending topup — topup NOT moved at lobby activate
    Given the player has a topup file with Excess = 6999 Scrap
    And   the player is the host (IsServer = true)
    When  LobbyHUDPage.OnActivate fires
    Then  OnJoinedSession returns immediately (topup handled by WrapLoadCallback)
    And   the topup file is unchanged at this point


  # ── Bank deposits ────────────────────────────────────────────────────────

  Scenario: Second layout-changed event accumulates in bank rather than overwriting
    Given bank.json already has 3000 Scrap banked from a previous mismatch
    And   a new layout-changed deposit adds 6999 Scrap
    When  TryDeposit runs
    Then  bank.json shows 9999 Scrap total
