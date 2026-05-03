# HandyPurse — Logic Reference

This document explains _why_ each system in HandyPurse exists and how the pieces
fit together. Read this before touching the bank, topup, or inventory patching code.

---

## What the mod does

HandyPurse raises the in-game currency stack caps above their vanilla limits. The
vanilla caps are small (e.g. 99 Scrap, 99 Black Coin). The mod replaces them with
configurable values (`ScrapCap`, `BlackCoinCap`, `CrystalCap`).

This sounds trivial, but the game enforces its caps in several places:

- `ItemDatabase.CreateItem` — clamps the amount passed in before the `NetworkItemDescriptor` is created.
- `GenericItemDescriptor.AmountMaximum` — the property consulted by UI and pickup merge logic.
- `InventorySyncedItems.MergeToInventory` — adds currency to existing stacks, respects the asset's `StackMaximum`.
- Cloud save serialization — serializes whatever `Amount` values are currently live.

Each of these points needs its own patch. The sections below explain each one.

---

## Cap elevation patches

### `CreateItemPrefix` + `CreateItemPostfix`

`ItemDatabase.CreateItem(assetId, amount)` is the factory for `NetworkItemDescriptor`
values. The game itself clamps `amount` to `asset.StackMaximum` here. The prefix
replaces that cap with the HandyPurse cap for managed currencies so newly spawned
drops carry the correct amount. The postfix then sets `StackMaximum` on the resulting
descriptor to the same value so the field is consistent.

Host-only (`IsServer` guard). Other players keep vanilla caps; HandyPurse only
elevates the local player's limits.

### `AmountMaximumPostfix`

`GenericItemDescriptor.AmountMaximum` is used by UI and merge logic. The postfix
replaces the return value for local-player-owned items. Stash items require reference
equality against the actual item list (a champion in the stash could belong to any
session); inventory items use the cheaper `BelongsTo(currentChampion)` check.

### `MergeToInventoryPrefix`

`InventorySyncedItems.MergeToInventory` handles auto-pickup stacking. It reads
`asset.StackMaximum` directly from the asset — not the `GenericItemDescriptor` property
patched above. The prefix replaces the entire merge loop using the HandyPurse cap for
the local player, and returns `false` (skip original) for managed currencies.
Other players fall through to the original (vanilla caps apply).

---

## Drop splitting

When a player drops a managed currency stack that exceeds the vanilla cap, the game
would create a single network item with an over-cap amount. Unmodded clients would
clamp it on pickup.

`InventoryOrchestrator.OnDropOwnedItem` (prefix on `Inventory.DropOwnedItemToGroundLocal`)
intercepts the drop, deletes the single over-cap item, and re-creates it as multiple
vanilla-sized stacks. Each stack is within the unmodded cap so any client can pick
it up safely.

---

## The cloud save problem

The game saves inventory to a cloud backend via `PlayerProfile.SavePlayerGameStates`.
The vault stores whatever `Amount` values are in the live `GenericItemDescriptor`
objects at the time of serialization. If those amounts exceed the vanilla cap, the
cloud receives them — and the next client that loads that save (a non-host player,
or the player themselves on a different machine without the mod) will have their
excess clamped by the game engine on load.

**HandyPurse cannot prevent the game engine from clamping on load on unmodded clients.
It can only ensure the cloud never stores amounts it cannot safely restore.**

---

## Topup files — how excess survives the cloud round-trip

### The problem in detail

The cloud stores clamped amounts. When the local player loads the same save on a
modded client, they would see only the clamped amounts — the excess is gone.

### The solution

HandyPurse patches `SavePlayerGameStates` with a **prefix + postfix bracket**:

1. **Prefix (`OnPlayerProfileSave`):** Before serialization, clamps all managed
   currencies to their vanilla caps on the live `GenericItemDescriptor` objects.
   Records the excess for each slot (as `TopupEntry` values). Writes a `TopupSave`
   file keyed by `PlayerGameState.TimeStamp`. Stores the original amounts for the
   postfix restore.

2. **Serialization runs** (inside the original method): sees the clamped amounts
   and sends them to the cloud.

3. **Postfix (`OnPlayerProfileSaveComplete`):** Restores the live `GenericItemDescriptor`
   objects to their original (over-cap) amounts. The player never sees their stacks
   drop.

On next load, HandyPurse patches `BackendManager.LoadPlayerGameState`. The prefix
wraps the callback: when the cloud returns the (clamped) `PlayerGameState`, the mod
looks up the `TopupSave` by `TimeStamp`, finds the matching file, and adds the excess
back into the loaded state before the game initializes the inventory. The player's
full stack is restored as if the save never clamped anything.

### Why `TimeStamp` is the key

`PlayerGameState.TimeStamp` is set by the game before save and returned unchanged by
the cloud on load. It is stable across the cloud round-trip, making it a reliable O(1)
lookup key. One `topup-{timestamp}.json` file is written per save event.

### Topup file lifetime

Topup files are **not deleted after a successful host-path restore**. The cloud save
always stores clamped amounts, so every future load of the same save (reload after
death, disconnect/reconnect, revert) needs the topup re-applied. Purging stale files
is a separate concern deferred to a future cleanup pass.

---

## The bank — why it exists

When the local player joins as a **client** (not the host), they do not have
`HasStateAuthority` over their own inventory objects. The `LoadPlayerGameState` hook
only fires on the host path — the client cannot inject items into the network objects
on someone else's server.

If the player has pending topup files and joins as a client, the excess would simply
be lost when the game initializes the inventory from the clamped cloud state.

**The bank is the client-side safety net.** When the lobby HUD activates and the
player is not the host, `BankOrchestrator.OnJoinedSession` runs:

1. Reads all pending topup files from disk.
2. Sums the excess per currency.
3. Deposits the totals into `bank.json` (a separate, persistent accumulator).
4. Shows a popup informing the player that funds were banked.

The bank balance is accessible from the mod menu. The player can drop the bank to
the floor the next time they are the host (solo or hosting a session) — the stored
amounts are spawned as vanilla-sized pickup stacks at their feet for them to collect.
The bank is cleared after a successful drop.

### Bank vs. topup — two different files

| File | Content | Purpose |
|---|---|---|
| `topup-{timestamp}.json` | Per-save excess, keyed by slot index | Re-applied on every host-path load of that save |
| `bank.json` | Running totals per currency | Withdrawable manually when the player has state authority |

They are independent. A topup file being present does not mean the bank has anything,
and vice versa.

---

## Data flow summary

```
[run ends, inventory over cap]
         │
         ▼
SavePlayerGameStates prefix
  ├─ clamp live amounts to vanilla
  ├─ write topup-{ts}.json (excess per slot)
  └─ store originals for postfix restore
         │
[serialization → cloud]
         │
         ▼
SavePlayerGameStates postfix
  └─ restore live amounts (player sees full stacks)

─────────────────────────────────────────────────────────

[player loads save as HOST]
         │
         ▼
LoadPlayerGameState prefix wraps callback
         │
[cloud returns clamped PlayerGameState]
         │
         ▼
wrapped callback: find topup-{ts}.json
  └─ apply excess back into state slots
         │
[game initializes inventory from restored state]
  → player has full stacks

─────────────────────────────────────────────────────────

[player joins as CLIENT (no state authority)]
         │
         ▼
LobbyHUDPage.OnActivate
  └─ OnJoinedSession:
       ├─ read all topup files
       ├─ deposit excess to bank.json
       └─ show popup ("funds banked")
         │
[bank balance droppable to floor when player is next host]
```

---

## Managed currencies

`BankLogic.IsManagedCurrency` defines the set: `BlackCoin (30)`, `BlackBlood (31)`,
`Glitter (32)`, `Scrap (40)`. Only these types are clamped on save, restored on load,
or deposited to the bank. All other item types pass through untouched.

The int literals mirror the `RR.Game.Items.ItemType` enum values. `HandyPursePatch.ResolveCap`
uses the enum directly; `BankLogic` uses the raw ints because it operates on
`ItemSlot` structs that have already been lifted off the live game objects (to keep
the pure logic testable without game engine references).

---

## Compartments

Inventory in Raiders of Blackveil is split into a "Common" compartment and one
compartment per champion type (the `InventoryChampionData` list). The topup system
processes each compartment independently and stores the results under a string key
(`"Common"` or the champion type name). This ensures slot indices are compartment-local
and do not drift if compartment counts change between save and load.
