# HandyPurse — Tech Notes

Lessons learned, sharp edges, and decisions that are non-obvious from the code alone.
Read `docs/dev/HandyPurse/logic.md` first for the overall design.

---

## Never delete topup files proactively

**Rule: do not delete a topup file unless you have confirmed that the cloud already holds
a save whose timestamp supersedes it.**

The only source of truth for the cloud timestamp is the `LoadPlayerGameState` callback —
the timestamp on the `PlayerGameState` that comes back from the backend. Outside of that
callback we have no reliable way to know which save is on the cloud.

Consequences of getting this wrong:
- Delete the topup before the matching cloud save is confirmed → player permanently loses
  the excess that was in the file. It is not in the cloud (clamped) and no longer on disk.

Correct lifetime for topup files:
- **Created** by the save prefix when excess is detected.
- **Superseded** by the next successful save (a new file with a new timestamp is written;
  the old file naturally becomes unreachable because no future state will carry its timestamp).
- **Deposited to bank and deleted** only on the client path (`OnJoinedSession`), where we
  know we cannot restore via state authority — so we explicitly give up on the restore and
  move the excess to the bank.

The host-path `ApplyTopupToState` does **not** delete the file after applying it. This is
intentional: the player might reload, disconnect, or revert to the same save, and the topup
must be re-applied every time that clamped state comes back from the cloud.

---

## `LocalPlayer.ProfileUUID` can be empty at save and load callback time

`PlayerManager.LocalPlayer` is set when a player with `HasInputAuthority` is added to the
session. Its `ProfileUUID` is set separately, via `RPC_Handle_SetUserData_All`. These are
two different events; there is a window where `LocalPlayer` exists but its UUID is
`Guid.Empty`, and another window before `LocalPlayer` is set at all.

`SavePlayerGameStates` fires during this window (early init, between-run lobby transitions).
Same for the `LoadPlayerGameState` async callback — the HTTP round-trip can return before the
player UUID RPC has been processed.

### Save-side fix

If `LocalPlayer.ProfileUUID` is empty **and the states array has exactly one entry**, it is
unambiguously the local player's state (solo session or early init). Use it.

If there are multiple states and the UUID is empty, bail — we cannot identify which state is
ours, and writing the wrong topup data would be worse than writing nothing.

### Load-side fix

In the wrapped callback, if `LocalPlayer.ProfileUUID` is empty, fall back to checking
whether a topup file exists for the incoming `state.TimeStamp`. Topup files are only ever
written for the local player, so a timestamp hit implies it is our state. This is the correct
fallback: if we wrote a topup for this exact save, we must be the same player loading it.

---

## Topup file internal `Timestamp` field vs. filename

The `TopupSave.Timestamp` field serialized inside the JSON may not always match the filename
(`topup-{timestamp}.json`). This happens when the game presents a `PlayerGameState` with a
stale or unexpected `TimeStamp` value at prefix time.

The filename is what matters for lookup (`FindTopupSave` constructs the path from
`state.TimeStamp` and opens that file directly). The internal `Timestamp` field is purely
informational and is never used in lookup or application logic. A mismatch is cosmetically
wrong but functionally harmless.

---

## `DataContractJsonSerializer` requires `UseSimpleDictionaryFormat`

Not specific to topup files, but worth keeping here: when deserializing flat `{"key":"value"}`
JSON objects (e.g. localization files), `DataContractJsonSerializer` requires
`UseSimpleDictionaryFormat = true`. Without it the serializer expects `[{Key, Value}]` pairs
and silently produces an empty result. Topup and bank files use typed `[DataContract]` classes,
not dictionaries, so this does not apply to them — but it will bite you if you add a new
dictionary-backed file.
