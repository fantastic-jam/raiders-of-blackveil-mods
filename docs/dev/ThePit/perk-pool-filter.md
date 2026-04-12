# ThePit — Perk Pool Filter

> Design authority for this document: game-designer agent.
> Developer use: implement a `ThePitPerkFilter` that excludes perks with any trait in the **Dead** or **Broken** columns below. Flag perks with **Special Consideration** traits for manual review before including them.

The base game's perk library was designed for a PvE roguelite with rooms, shops, healing stations, elites, and death-carries-weight. ThePit is a single-arena PvP loop with instant revive and no economy. A large fraction of the perk pool is either permanently dormant or so warped in value that it distorts play in ways the player cannot predict or counterplay.

This document defines which perk traits are viable, which are dead weight, and which need manual vetting.

---

## 1. Viable Perk Triggers and Effect Types

These trigger categories have meaningful, sustained uptime in a PvP arena and produce legible feedback loops.

### Combat — Offensive triggers

**`OnHit`, `OnCritical`, `OnHitCausedDamage`**
The bread-and-butter triggers. Every combat action can fire them. High uptime, immediate feedback, scale naturally with player aggression. Good anchors for offensive builds.

**`OnHitWithAttack`, `OnHitWithDash`, `OnHitWithPower`, `OnHitWithUltimate`, `OnHitWithSpecial`, `OnHitWithDefensive`**
Ability-specific variants of `OnHit`. Reward players for choosing a build that favors one ability type. These create meaningful differentiation between champions and between perk choices — exactly what a PvP perk selection should offer.

**`OnCriticalWithAttack`, `OnCriticalWithAbility`**
Crit-synergy triggers. Valid as long as crit is achievable in PvP, which it is. Reward investing into crit-scaling builds.

**`OnHitWithPerk`**
Fires when a perk's own projectile or area hits. Valid for any champion using Pattern 4 (AreaCharacterSelector) perk coverage.

**`OnHit3WithDash/Power/Special/Defensive/Ultimate`, `OnHitCountWithPowerIs2/3`**
Combo-counter triggers. These require committing to a sequence, which is skill-expressive in PvP and creates legible "I'm building toward my combo" decisions. Good.

### Status-effect offensive triggers

**`OnHitWithBurn/Poison/Bleed/Curse/Bless/Shock/Fury/Root`**
Fires when a hit also carries a status effect onto the target. These are valid if the champion can actually apply that status. They create DoT build identity: "I want to keep Poison on my target and chain off it." High legibility because the player can see the status on the enemy.

**`OnShockCasted`, `OnChillCasted`, `OnFreezeCasted`, `OnBurnCasted`, `OnPoisonCasted`, `OnBleedCasted`, `OnStunCasted`, `OnCurseCasted`, `OnBlessCasted`, `OnRootCasted`, `OnFuryCasted`, `OnTauntCasted`**
Fire when the player successfully applies a status effect. These are combo-enablers: cast a CC, get a buff, press advantage. Uptime depends on how often the player applies that status; for common effects (Burn, Bleed, Root) uptime is high and healthy.

**`OnBurnExploded`, `OnCastedBurnExploded`**
Fire when a Burn expires in an explosion. Valid and interesting — creates a "ramp then detonate" pattern. Uptime depends on Burn application, which is fine.

**`OnKillWithShock`, `OnKillWithFury`**
Kill triggers gated on a specific status being active. Viable in PvP — kills happen, and gating them on a status condition makes the trigger feel earned without being unreachable.

### Kill triggers

**`OnKill`**
Direct PvP kill trigger. Functions correctly in ThePit. Good reward loop for aggressive players. Note: with instant revive the kill cycle is short, so kill-based stacking ramps quickly — see tuning note in section 3.

**`OnChainKill3`**
Three consecutive kills without dying. Viable in PvP, adds a high-skill snowball reward. Should be achievable in a match but not trivially common.

### Defensive / damage-taken triggers

**`OnDamageTaken`, `OnHealthDamageTaken`**
React-to-damage triggers. High uptime when under pressure. Create a reactive playstyle identity ("I want to get hit so my build activates"). Counterplay exists because the opponent controls when to trade.

**`OnZenBarrierDamageTaken`, `OnBarrierDamageTaken`**
Defensive-layer variants. Valid only if the player has Zen or Barrier active, which self-gates the uptime. These reward building into those layers.

**`OnArmorPlateDestroyedByHit`, `OnArmorPlateDestroyedByPerkOrHit`**
Fires when armor is stripped. Good: turning a defensive loss into an offensive trigger creates interesting risk/reward tension. Valid as long as armor plates exist in ThePit (they do).

**`OnLowHealth`**
Classic comeback trigger. Valid — getting low happens in PvP and the threshold is legible to both the triggering player and the opponent watching HP bars.

**`OnImmuneNoDamage`**
Fires when a hit lands but deals zero damage (blocked fully). Valid for builds around Immunity or heavy mitigation. Niche but not broken.

### Healing triggers

**`OnHealed`**
Any self-heal fires this. Valid — healing perks, orb pickups, and regen all trigger it. Uptime is moderate and gated on actually healing, which is appropriate.

**`OnHealthGlobeCollected`**
Only fires if orbs are spawnable in the arena. Developer must confirm whether health orbs can drop in ThePit (e.g. from kill drops or perk effects). If they cannot spawn, this is dead weight — see section 2. If they can, it is valid.

**`OnTargetHealed`**
Fires when the player heals an ally. In a 2v2+ scenario this is valid. In a 1v1 it never fires. Borderline — flag for session size check.

### Player-state triggers

**`OnDied`**
Fires on death before the respawn. Valid. With instant revive the time window is extremely short, but `OnDied` itself fires once and reliably — buffs/effects applied at death will carry into the respawn. This is fine as long as the applied effect has a short enough duration not to feel like free on-death immunity.

### Ability-use triggers

**`OnAttackUsed`, `OnDashUsed`, `OnPowerUsed`, `OnDefensiveUsed`, `OnSpecialUsed`, `OnUltimateUsed`**
Fire on casting. Very high uptime for the player's primary ability type. These are strong and healthy — they reward building around a specific ability loop.

**`OnDashEnded`, `OnDashPassedEnemy`**
Dash-specific. `OnDashPassedEnemy` is skill-expressive: it requires threading the dash through an opponent. Good design for PvP.

**`OnAttackPhaseFinished`**
Fires when the attack animation completes. Valid for melee-heavy champions.

### Status received on self

**`OnShocked`, `OnChilled`, `OnFreezed`, `OnBurned`, `OnPoisoned`, `OnBleeded`, `OnStunned`, `OnRooted`, `OnSlowed`, `OnCursed`, `OnBlessed`, `OnEnraged`, `OnZenActivated`, `OnFuryAdded`, `OnArmorPlateGained`, `OnBarrierAdded`**
Fires when the player receives a status effect. These create reactive/resilience builds: "when I get debuffed, I respond." All valid in PvP because opponents actively apply these effects. Counterplay exists — don't apply the status.

### Status expired on self

**`OnChillExpired`, `OnFreezeExpired`, `OnShockExpired`, `OnBleedExpired`, `OnStunExpired`, `OnRootExpired`, `OnCurseExpired`, `OnBlessExpired`, `OnEnragedExpired`, `OnZenExpired`, `OnFuryExpired`, `OnBarrierStackExpired`**
Fire when the effect wears off. Valid for "ride the buff then cash it in" patterns. These are fair because the opponent can control application, and the player telegraphs the buff state visually.

### Misc triggers

**`OnAllAbilityInCooldown`**
Fires when every ability is simultaneously on cooldown. Rare but legible. Creates interesting "all-in, then reward for the empty window" tension. Valid.

**`OnOutOfDangerActivated`**
Fires after not taking damage for a set window. Valid — rewards kiting, spacing, or escaping a losing trade. Uptime in a fast PvP loop will be moderate, which keeps it balanced.

**`OnZenHeartBeat`**
Fires repeatedly while Zen is active. High uptime only when in Zen, which is gated on building into Zen. This is fine — it creates a clear identity ("sustain Zen, get consistent value").

**`OnMinionSpawned`**
Valid if the champion has a summon perk. Reward for building into the minion archetype. Uptime is build-dependent.

**`OnPushCasted`, `OnPullCasted`, `OnPullStarted`, `OnPullCastedExpired`, `OnPullEnded`**
Displacement triggers. Valid and interesting for champions with push/pull abilities. Low uptime keeps them balanced.

**`OnAttackSpeedChanged`**
Reactive to stat changes. Valid.

### Property modifiers (stat buffs)

All of the following are valid in ThePit with no caveats:

- All damage modifiers: `BasicDamageMultiplierPCT`, `OnHitDamageMultiplierPCT`, `OnHitDamageAddonFlat`, `CriticalStrikeChance`, `CriticalStrikeDamage`, and all ability-specific damage addons.
- All cooldown reduction properties.
- `MovementSpeed`, `AttackSpeed`, `TurnSpeed`, `DashCharge`, `DodgeChance`.
- `MaxHealthPercentage`, `MaxHealthFlat`, `ReceivedDamageReduction`, `ArmorPlatesCountChange`, `ArmorDamageReductionIncPCT`, `ProtectionMultiplierPCT`.
- All status effect modifiers (Burn damage, Bleed modifiers, Shock, Poison, Root, Chill/Freeze, Curse, Bless, Fury, Barrier, Zen, Enrage, Taunt, Summon properties).
- Healing power properties (`HealingPowerAddon`, `OnHealHealingMultiplier`, `CriticalHealing`, `HealingInjuryReductionPCT`).
- `CurrentHealth` / `CurrentTrueHealth` (used as heal-on-trigger).

Dynamic scaling factors: `AttackCount`, `KillCount`, `ChainKillCount`, `StackCount`, `HealthPercent` — all valid.

### Applicable status effects

All of the following are valid:
- Damage: `BasicDamage`, `PunchWithDelay`, `CriticalHitWithDelay`, `NextAbilityIncreasedDamage`, `NextBasicAttackIncreasedDamage`.
- DoT: `Burn`, `Bleed`, `DamageWithBleed`, `Poison`, `Shock`, `ChainShock`, `BurnDoubleExplosion`, `SpawnWitheredSeed`.
- CC: `Stun`, `Root`, `Slow`, `Chill`, `Freeze`, `ChillOrPoison`, `ChillOrBurn`, `HookToMe`, `PushAway`.
- Buffs: `Bless`, `Fury`, `Barrier`, `Zen`, `ArmorPlate`, `FullArmorPlates`, `Enraged`, `Taunt`.
- Debuffs: `Curse`, `SAP`, `RemoveOneArmorPlate`.
- Healing: `HealingFlat`, `HealingPercentage`, `TrueHealingFlat`, `TrueHealingPercentage`.
- Utility: `Cleanse`.

---

## 2. Dead/Useless Perk Triggers and Why

These triggers or properties either **never fire** in ThePit, **fire so rarely they are functionally dead**, or **have no target to act on**. Perks whose primary trigger or primary effect falls into this list should be excluded from the ThePit perk pool.

### Never fire — environmental rooms that don't exist

| Trigger / Property | Reason |
|---|---|
| `OnEnterHealingRoom` (302) | No healing rooms in ThePit |
| `OnEnterShop` (303) | No shop in ThePit |
| `OnSceneClosing` (304) | Scene does not close between rounds |
| `StatusEffect.HealingRoomHealFlat` | No healing rooms |
| `StatusEffect.HealingRoomHealPercentage` | No healing rooms |

### Never fire — enemies that don't exist

| Trigger / Property | Reason |
|---|---|
| `OnEnemyDied` (42) | This event is enemy-only and is **not raised** when a player champion dies. PvP kills fire `OnKill`, not `OnEnemyDied`. A perk triggered by this will never activate. |
| `OnEnemySpawned` (240) | No enemy spawns in the arena |
| Condition: `IsElite` | Always false — no elites |
| Condition: `IsBoss` | Always false — no bosses |
| Scaling: `EliteKillCount` | Always 0 — no elites to kill |

### Never fire — arena structure

| Trigger | Reason |
|---|---|
| `OnClearArena` (300) | This fires when all enemies in a room are killed. In a PvP arena there are no enemy NPCs, so the arena is never "cleared" in the game's sense. This event never raises. |
| `OnAllPlayerDied` (74) | No game-over state in ThePit — the match continues after all players fall. This event likely never raises in the expected way. |
| `OnGoingToLobby` (75) | ThePit does not use the normal lobby-return flow between rounds. This event is irrelevant. |

### Near-zero uptime — once-per-run events

| Trigger | Reason |
|---|---|
| `OnEnterArena` (301) | Fires once when entering the arena room at the start of a session. In a multi-round format the arena is entered once, not once per round. A perk that grants a buff "on entering the arena" gives ~1 tick of value for the entire session. Not worth a perk slot. |
| `OnSceneStarting` (305) | Same problem — fires once at scene load. Functionally a one-shot that the player cannot meaningfully choose around. |

### Mechanically moot

| Trigger / Effect | Reason |
|---|---|
| `OnRevived` (71) | ThePit uses near-instant revive. The window between knockdown and revival is too short for any meaningful trigger window. A perk designed around "after being revived" assumes a meaningful revival event (ally picks you up, healing station, etc.) — that event doesn't exist in ThePit in a usable form. |
| `OnKnockedOut` (72) | Same logic. The knockdown state is effectively an animation frame, not a sustained state. Perks designed to fire on knockdown and persist assume downed-state gameplay. |
| `OnKnockedOutLost` (73) | Same. |
| `OnBeforeKnockedOut` (64) | Fires just before knockdown. The effect may apply but there is no sustained downed state to leverage it. Borderline useful only if the effect is a heal or defensive burst applied at the last moment — evaluate per perk. |
| `StatusEffect.ReviveMe` | ThePit already handles revive instantly. This effect is moot. |

### Economy — no gold or loot system in arena

| Property | Reason |
|---|---|
| `Gold` / `GoldBonusPCT` | No gold currency in the arena |
| `LootLuckPCT` | No loot drops in PvP combat |
| `HealthOrbDropChance` / `HealthOrbAddon` | Only useful if orbs drop from kills or arena events. If ThePit does not spawn orbs, these are dead. **Confirm with dev before excluding.** |

### Perk-selection-only value

| Property | Reason |
|---|---|
| `PerkLuckPCT`, `PerkEpicChanceInc`, `PerkLegendaryChanceInc`, `PerkMythicChanceInc` | These only affect the perk selection draw. In PvP the number of perk selection rounds is small and predictable. A perk that does nothing except improve future perk draws has near-zero gameplay impact and takes up a slot that could have been a combat perk. Exclude or at minimum mark as lowest-priority in the draw. |

---

## 3. Special Considerations — Borderline Perks Needing Manual Review

These trigger/effect types are mechanically valid in ThePit but may create balance problems, feel-bad moments, or systemic issues specific to PvP. They should not be auto-excluded but should be reviewed individually before inclusion.

### Hard CC chains (`Stun`, `Freeze`, `Root` applied by perks on top of ability CC)

**Problem:** In PvE, CC duration is tuned around enemy AI resuming attacks. Enemies don't feel a 3-second stun the same way a player does. In PvP, if a player can chain a Stun from an ability with a Freeze from a perk with a Root from another perk, the opponent loses agency entirely for a sequence they cannot react to.

**Recommendation:** Allow CC-applying perks but cap the effective perk-applied CC stack. Specifically, review any perk that applies Stun or Freeze with durations longer than ~1 second, or any perk that re-applies CC on a short cooldown. `Root` is softer (movement only) and is lower priority for this review.

### Healing perks in PvP

**Problem:** PvE healing is balanced around encounter pacing where players fight, rest, and fight again. In PvP, healing that outruns the opponent's damage output means fights never resolve. A flat-heal-on-hit perk that heals 15 HP per basic attack might be fine in PvE but oppressive in a sustained 1v1.

**Three sub-categories to review separately:**
- `HealingFlat` / `HealingPercentage` on offensive triggers (`OnHit`, `OnKill`) — likely too strong, especially at high attack speed.
- `TrueHealingFlat` / `TrueHealingPercentage` — true healing bypasses damage reduction. Evaluate per perk; the flat amounts may be fine, but percentage-based true healing can be unbalanceable.
- Healing tied to defensive triggers (`OnDamageTaken`) — these are more acceptable because they are reactive and opponent-controlled, but evaluate the heal amount relative to the damage that triggered it.

**Recommendation:** Do not auto-exclude healing perks. Instead, flag any perk where the heal trigger is `OnHit` or `OnAttackUsed` (i.e. the player controls the uptime) for manual value review. Healing tied to `OnDamageTaken` or `OnLowHealth` is lower risk.

### `OnKill` stacking builds

**Problem:** With instant revive, the kill cycle in ThePit is short. A perk that stacks permanently on each kill (`KillCount` scaling) can ramp to large values over a long session in a way it would not in a PvE run, where kill counts reset between rooms and the run ends.

**Recommendation:** Perks that scale from `KillCount` with no ceiling should be reviewed. Either the perk naturally has a low delta-per-kill that stays reasonable, or it needs a cap. Perks scaled on `ChainKillCount` (3-kill streaks) are safer because they require uninterrupted performance.

### `InstantKill` effect

**Problem:** Mechanically valid — it works in PvP. But a perk that can one-shot a player champion regardless of HP is an extreme feel-bad moment even if it is statistically rare. Players cannot counterplay "I died to a perk proc."

**Recommendation:** Strongly consider excluding all perks that apply `StatusEffect.InstantKill` to player targets from the ThePit perk pool. If kept, it should only be as a conditional effect gated on a stringent condition (e.g. enemy is at <5% HP — effectively a finisher), not as a random-chance proc.

### `Taunt` applied by perks

**Problem:** `Taunt` forces the target to attack the caster. In PvE this is used to control enemy aggro. Applied to a player champion, it removes the opponent's targeting choice for the duration. This is a non-obvious form of player control loss that may feel confusing without UI clarity.

**Recommendation:** Allow only if there is clear visual feedback to the taunted player that their targeting is overridden. Without that feedback it is invisible CC, which is illegible and unfun.

### `Invisibility` and `Immune` properties

**`Invisibility`:** Stealth in a PvP context creates periods where the opponent has no information and cannot act. Evaluate on duration — a short invisibility as a dash effect is fine, extended or refreshable invisibility is problematic.

**`Immune`:** True immunity (no damage taken) as a perk property is acceptable as a short burst tied to a trigger (e.g. 1-second immunity on `OnLowHealth`). Immunity that can be kept up indefinitely via stacking or refreshing is a blocker.

### `OnRevived` and `OnBeforeKnockedOut` — edge case to confirm

The instant revive window may not be zero in all cases — it depends on whether the revive animation has any duration. If any delay exists, `OnBeforeKnockedOut` can fire and apply an effect that carries into the revived state. This is not necessarily broken, but it means the perk value arrives in an unexpected way (the trigger is "about to be knocked out" but the benefit is felt after revival). Confirm the exact revive timing with the dev before including any perk that specifically uses this trigger for survivability.

### `OnTargetHealed` in small lobbies

This trigger only has uptime if the player is actively healing allies. In a 1v1 (2 players), the only possible ally healing is from area-of-effect heals that accidentally hit yourself when healing — which is an unlikely case. This trigger's value scales with player count. Flag for exclusion in 1v1 mode if ThePit ever supports mode selection.

---

## Quick-reference exclusion list for implementation

The following triggers and properties can be programmatically excluded from the ThePit perk draw without manual review. Any perk whose **primary** trigger or **primary** effect matches these should be filtered out.

**Triggers to exclude:**
- `OnEnterHealingRoom` (302)
- `OnEnterShop` (303)
- `OnSceneClosing` (304)
- `OnEnemyDied` (42)
- `OnEnemySpawned` (240)
- `OnClearArena` (300)
- `OnAllPlayerDied` (74)
- `OnGoingToLobby` (75)
- `OnEnterArena` (301) — unless the perk grants a permanent passive; one-shot buffs are dead weight
- `OnSceneStarting` (305) — same caveat as above
- `OnRevived` (71), `OnKnockedOut` (72), `OnKnockedOutLost` (73) — unless the effect is a permanent stat, not a temporary window-dependent buff

**Conditions to exclude:**
- `IsElite`
- `IsBoss`

**Scaling factors to exclude:**
- `EliteKillCount`

**Effects to exclude:**
- `StatusEffect.ReviveMe`
- `StatusEffect.HealingRoomHealFlat`
- `StatusEffect.HealingRoomHealPercentage`

**Properties to exclude:**
- `Gold`, `GoldBonusPCT`
- `LootLuckPCT`
- `PerkLuckPCT`, `PerkEpicChanceInc`, `PerkLegendaryChanceInc`, `PerkMythicChanceInc`
- `HealthOrbDropChance`, `HealthOrbAddon` — **confirm first whether orbs can spawn in ThePit**

**Perks to flag for manual review (do not auto-exclude):**
- Primary effect: `StatusEffect.InstantKill`
- Primary effect: `Stun` or `Freeze` with duration > 1s, or on a short cooldown
- Primary effect: `Taunt` applied to enemy player
- Primary effect: `Invisibility` with duration > ~0.5s or refreshable
- Primary effect: healing on an offensive trigger (`OnHit`, `OnAttackUsed`, `OnKill`)
- Scaling: `KillCount` without a stated cap
- Property: `Immune` or `Invincibility` that can be sustained
