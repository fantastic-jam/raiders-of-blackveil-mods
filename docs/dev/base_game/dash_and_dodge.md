# Dash ability and dodge resolution

Verified against the 2026-10-19 build.

## DashAbility state machine

`RR.Game.Character.DashAbility : ChampionAbilityWithCooldown`. `MainState` maps the internal state:

| InternalState | MainStateValues |
|---|---|
| WindUp | Cast |
| Dashing | InAction |
| FollowThrough | FollowThrough |
| CancelFrames | Cancel |
| other | Idle |

`protected override void MainStateChanged(MainStateValues prevState)` is the transition hook. Every per-champion
subclass calls `base.MainStateChanged(prevState)`: Angelle, Beatrice, Blaze, Foxley, Rhino, Shameleon
(Angelle and Foxley were added in this build). A Harmony patch on `DashAbility.MainStateChanged`
therefore fires for every champion.

## Refunding charges

`ChampionAbilityWithCooldown` has `private bool _resetAllCooldown` and the public setter
`ResetCooldownWhenPossible()`. In `FixedUpdateNetwork` (state authority only), the flag is consumed once
`MainState` is `Idle` or `Cancel`. It restores **all** charges (`Charge_Actual = ChargeCount`) and clears the timer.
Setting it on a client does nothing.

## Dodge in `Health.TakeBasicDamage`

Signature (unchanged): `public bool TakeBasicDamage(DamageDescriptor dmgDesc, Vector3 damageDirection, DamageDescription damageDescription, UserAction attackAction, StatsManager attacker)`.
`DamageDescriptor` is a struct.

On a native dodge (chance, or front-shield immunity), the game:
```csharp
AddDamageData(0f, StatusEffect.BasicDamage, in attackerID, false, true); // "Dodge!" popup on all clients
_stats.Events.TriggerEvent(CharacterEvent.OnDodge, new TriggerParams(attacker));
damageDescription.MitigateDamage(dmt, damageValue);
WTuneServer.Instance.RecordDamageDealt(...);
return false;
```
The dodge-chance stat is now read through `DodgeChancePCT` (was `NetworkedDodgeChancePCT`).
`Health._stats` (private `StatsManager`) is unchanged.
