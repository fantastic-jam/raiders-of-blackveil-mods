# Perk Effects Reference

> **Note:** Individual perk names and their specific configured values are stored as Unity ScriptableObjects and cannot be enumerated from decompiled source. This document covers the complete perk effect **taxonomy** — every trigger event, property modifier, and status effect the system can use. Use this to reason about which perk *types* are relevant for ThePit.

---

## Trigger Events (`CharacterEvent`)

These are the events that activate a perk's functionality.

### Combat — Offensive
| Event | Value | Notes |
|---|---|---|
| `OnHit` | 1 | Any hit lands |
| `OnCritical` | 2 | Any crit lands |
| `OnHitCausedDamage` | 5 | Hit that actually dealt damage |
| `OnHitWithAttack` | 20 | Basic attack hit |
| `OnHitWithDash` | 21 | Dash hit |
| `OnHitWithPower` | 22 | Power ability hit |
| `OnHitWithUltimate` | 23 | Ultimate hit |
| `OnHitWithSpecial` | 24 | Special hit |
| `OnHitWithDefensive` | 25 | Defensive ability hit |
| `OnCriticalWithAttack` | 30 | Crit with basic attack |
| `OnCriticalWithAbility` | 31 | Crit with any ability |
| `OnHitWithPerk` | 34 | Hit from a perk projectile |
| `OnHitCountWithPowerIs3` | 36 | 3rd power hit (combo) |
| `OnHitCountWithPowerIs2` | 37 | 2nd power hit (combo) |
| `OnHit3WithDash` | 140 | 3-hit combo with dash |
| `OnHit3WithPower` | 141 | 3-hit combo with power |
| `OnHit3WithSpecial` | 142 | 3-hit combo with special |
| `OnHit3WithDefensive` | 143 | 3-hit combo with defensive |
| `OnHit3WithUltimate` | 144 | 3-hit combo with ultimate |

### Combat — Status Effect Applied (Offensive)
| Event | Value |
|---|---|
| `OnHitWithBurn` | 120 |
| `OnHitWithPoison` | 121 |
| `OnHitWithBleed` | 122 |
| `OnHitWithCurse` | 123 |
| `OnHitWithBless` | 124 |
| `OnHitWithShock` | 125 |
| `OnHitWithFury` | 126 |
| `OnHitWithRoot` | 127 |
| `OnShockCasted` | 100 |
| `OnChillCasted` | 101 |
| `OnFreezeCasted` | 102 |
| `OnBurnCasted` | 103 |
| `OnPoisonCasted` | 104 |
| `OnBleedCasted` | 105 |
| `OnStunCasted` | 106 |
| `OnCurseCasted` | 107 |
| `OnBlessCasted` | 109 |
| `OnRootCasted` | 110 |
| `OnFuryCasted` | 115 |
| `OnTauntCasted` | 116 |
| `OnKillWithShock` | 150 |
| `OnKillWithFury` | 153 |
| `OnBurnExploded` | 151 |
| `OnCastedBurnExploded` | 152 |

### Combat — Kill
| Event | Value | ThePit relevance |
|---|---|---|
| `OnKill` | 40 | ✅ PvP kill |
| `OnChainKill3` | 41 | ✅ 3-kill streak |
| `OnEnemyDied` | 42 | ❌ Enemy-only event, not triggered by player kills |

### Combat — Defensive / Damage Taken
| Event | Value |
|---|---|
| `OnDamageTaken` | 50 |
| `OnHealthDamageTaken` | 51 |
| `OnZenBarrierDamageTaken` | 52 |
| `OnProtectedDamageTaken` | 53 |
| `OnArmorPlateDestroyedByHit` | 54 |
| `OnBarrierDamageTaken` | 55 |
| `OnArmorPlateDestroyedByPerkOrHit` | 56 |
| `OnLowHealth` | 60 |
| `OnImmuneNoDamage` | 4 |

### Healing
| Event | Value | ThePit relevance |
|---|---|---|
| `OnHealed` | 61 | ✅ Any self-heal |
| `OnHealthGlobeCollected` | 62 | ✅ Orb pickup |
| `OnTargetHealed` | 63 | ✅ Healing an ally |

### Player State
| Event | Value | ThePit relevance |
|---|---|---|
| `OnDied` | 70 | ✅ On death |
| `OnRevived` | 71 | ⚠️ Weak — ThePit has instant revive |
| `OnKnockedOut` | 72 | ⚠️ Weak — instant revive means very brief window |
| `OnKnockedOutLost` | 73 | ⚠️ Weak — instant revive |
| `OnBeforeKnockedOut` | 64 | ⚠️ Weak — instant revive |
| `OnAllPlayerDied` | 74 | ❌ Irrelevant |
| `OnGoingToLobby` | 75 | ❌ Irrelevant |

### Ability Usage
| Event | Value |
|---|---|
| `OnAttackUsed` | 80 |
| `OnDashUsed` | 81 |
| `OnPowerUsed` | 82 |
| `OnDefensiveUsed` | 83 |
| `OnSpecialUsed` | 84 |
| `OnUltimateUsed` | 85 |
| `OnDashEnded` | 90 |
| `OnAttackPhaseFinished` | 91 |
| `OnDashPassedEnemy` | 92 |

### Status Effect Received (on self)
| Event | Value |
|---|---|
| `OnShocked` | 200 |
| `OnChilled` | 201 |
| `OnFreezed` | 202 |
| `OnBurned` | 203 |
| `OnPoisoned` | 204 |
| `OnBleeded` | 205 |
| `OnStunned` | 206 |
| `OnRooted` | 207 |
| `OnSlowed` | 208 |
| `OnCursed` | 209 |
| `OnBlessed` | 210 |
| `OnEnraged` | 211 |
| `OnZenActivated` | 212 |
| `OnGrabbed` | 213 |
| `OnFuryAdded` | 217 |
| `OnArmorPlateGained` | 219 |
| `OnBarrierAdded` | 221 |

### Status Effect Expired (on self)
All `OnXxxExpired` events (250–266): `OnChillExpired`, `OnFreezeExpired`, `OnShockExpired`, `OnBleedExpired`, `OnStunExpired`, `OnRootExpired`, `OnCurseExpired`, `OnBlessExpired`, `OnEnragedExpired`, `OnZenExpired`, `OnFuryExpired`, `OnBarrierStackExpired`

### Scene / Room Events
| Event | Value | ThePit relevance |
|---|---|---|
| `OnClearArena` | 300 | ❌ Fires when all enemies die — not applicable in PvP arena |
| `OnEnterArena` | 301 | ⚠️ Fires once on entering the arena room — very limited in a 1-arena game |
| `OnEnterHealingRoom` | 302 | ❌ No healing rooms in ThePit |
| `OnEnterShop` | 303 | ❌ No shop in ThePit |
| `OnSceneClosing` | 304 | ❌ Irrelevant |
| `OnSceneStarting` | 305 | ⚠️ Once at run start — very limited |

### Misc
| Event | Value | ThePit relevance |
|---|---|---|
| `OnEnemySpawned` | 240 | ❌ No enemy spawns in pure PvP |
| `OnMinionSpawned` | 114 | ✅ If player has summon perk |
| `OnAllAbilityInCooldown` | 321 | ✅ All abilities on CD |
| `OnAttackSpeedChanged` | 320 | ✅ Reactive |
| `OnOutOfDangerActivated` | 323 | ✅ After not taking damage for a window |
| `OnZenHeartBeat` | 322 | ✅ Periodic while in Zen |
| `OnPushCasted` | 108 | ✅ |
| `OnPullCasted` | 112 | ✅ |
| `OnPullStarted` | 113 | ✅ |
| `OnPullCastedExpired` | 180 | ✅ |
| `OnPullEnded` | 181 | ✅ |

---

## Property Modifiers (`Property`)

Permanent or timed stat changes a perk can apply to a character.

### Damage
| Property | Notes |
|---|---|
| `BasicDamageMultiplierPCT` | Global basic damage % |
| `OnHitDamageMultiplierPCT` | Per-hit damage % |
| `OnHitDamageAddonFlat` | Flat damage per hit |
| `CriticalStrikeChance` | Crit % |
| `CriticalStrikeDamage` | Crit damage % |
| `CriticalChanceAddonForSameTarget` | Crit stacking vs same target |
| `OnHitCriticalChanceIncrement` | Crit on hit |
| `OnHitCriticalChanceMultiplier` | Crit mult on hit |
| `OnHitCriticalDamageAddonPCT` | Crit dmg on hit |
| `OnHitAbilityPowerIncrement` | Ability power on hit |
| `OnHitAttackDamageStatIncrement` | Attack damage stat on hit |
| `MagicPower` / `PhysicalPower` | Raw power stats |
| `MagicPowerAddon` / `MagicPowerMultiplierPCT` | Magic modifiers |
| `PhysicalPowerMultiplierPCT` | Physical modifiers |

### Ability-Specific Damage
| Property | Notes |
|---|---|
| `OffensiveDamageAddonPCT` | Power ability damage |
| `SpecialDamageAddonPCT` | Special ability damage |
| `UltimateDamageAddonPCT` | Ultimate damage |

### Cooldown Reduction
| Property | Notes |
|---|---|
| `OffensiveCooldownReduction` / `OffensiveActualCooldownDecrement` / `OffensiveActualCooldownReset` | Power CD |
| `SpecialCooldownReduction` / `SpecialActualCooldownDecrement` / `SpecialActualCooldownReset` | Special CD |
| `UltimateCooldownReduction` / `UltimateActualCooldownDecrement` / `UltimateActualCooldownReset` | Ult CD |
| `DefensiveCooldownReduction` / `DefensiveActualCooldownDecrement` / `DefensiveActualCooldownReset` | Defensive CD |
| `AllActualCooldownDecrement` / `AllCooldownReduction` | All abilities |
| `DashCooldownReduction` / `DashActualCooldownDecrement` / `DashActualCooldownReset` | Dash CD |
| `*Charge` / `*CooldownChargeRatioIncrement` | Charge-based CDR |

### Movement
| Property | Notes |
|---|---|
| `MovementSpeed` | Move speed |
| `AttackSpeed` | Attack speed |
| `TemporaryAttackSpeed` | Temporary attack speed |
| `TurnSpeed` | Turn speed |
| `DashCharge` | Extra dash charges |
| `DodgeChance` | Dodge % |

### Defense & Health
| Property | Notes |
|---|---|
| `MaxHealthPercentage` / `MaxHealthFlat` | Max HP |
| `ReceivedDamageReduction` | Damage mitigation |
| `ReceivedDamageIncrement` | Take more damage (debuff) |
| `ArmorPlatesCountChange` | Armor plate count |
| `ArmorDamageReductionIncPCT` | Armor plate strength |
| `ProtectionMultiplierPCT` | General protection |
| `Immune` | Immunity |
| `Invincibility` | Invincible frames |
| `Invisibility` | Stealth |

### Healing
| Property | Notes |
|---|---|
| `HealingPowerAddon` | Flat healing power |
| `OnHealHealingMultiplier` | Healing multiplier |
| `CriticalHealing` | Healing crits |
| `HealingInjuryReductionPCT` | Reduce injury penalty |
| `HealthOrbAddon` / `HealthOrbDropChance` | Orb drops |

### Status Effect Modifiers
Each major DoT/CC/buff has associated property modifiers:
- **Burn**: `BurnDamage`, `BurnDurationPCT`, `BurnImmunity`, `BurnUpgradeUnlocked`
- **Bleed**: `BleedDamagePCT`, `BleedImmunity`, `BleedMovementDamagePCT`, `BleedSpeedReduction`, `BleedUpgradeUnlocked`
- **Poison**: `PoisonDamage`, `PoisonUpgradeUnlocked`
- **Shock**: `ShockDamage`, `ShockUpgradeUnlocked`
- **Root**: `RootDamageAddonPCT`, `RootDurationPCT`
- **Chill/Freeze**: `ChillFreezeChanceAddon`, `ChillDurationPCT`, `ChillFrostDamageAddon`, `ChillUpgradeUnlocked`
- **Curse**: `CurseDamageAddonPCT`, `CurseUpgradeUnlocked`
- **Bless**: `BlessNumEmpoweredAttacks`, `BlessedAttackDamageIncrement`, `BlessUpgradeUnlocked`, `BlessCausesCritical`
- **Fury**: `FuryBoostedAttacksIncrement`, `FuryAttackDamageIncrement`, `FuryAttackSpeedIncrement`
- **Barrier**: `BarrierDurationIncrement`, `BarrierAmountPCT`
- **Zen**: `ZenDurationIncrementFlat`, `ZenDurationIncrementPCT`, `ZenDodgeChanceMultiplier`, `ZenAttackSpeedIncrementPCT`, `ZenDodgeStackMaxInc`
- **Enrage**: `EnragedDepleteTimeMultiplier`, `EnragedDamageIncrementPCT`, `EnragedStackMaxIncrement`, `EnragedUpgradeUnlocked`
- **Taunt**: `TauntEnabled`, `TauntDurationIncrement`, `TauntStackMaxIncrement`, `TauntedIncreasedDamageIncrement`, `TauntedFaerieFireUnlocked`
- **Summon**: `SummonMinionsDamagePCT`, `SummonChanceIncrementPCT`, `SummonUnlockRanged`, `SummonUnlockFrostMage`, `SummonMinionTimeoutDisabled`

### Progression (Weak/Irrelevant in ThePit)
| Property | ThePit relevance |
|---|---|
| `Gold` / `GoldBonusPCT` | ❌ No gold spending in arena |
| `LootLuckPCT` | ❌ No loot drops in PvP |
| `PerkLuckPCT` / `PerkEpicChanceInc` / `PerkLegendaryChanceInc` / `PerkMythicChanceInc` | ⚠️ Only useful during perk selection rounds |
| `CurrentHealth` / `CurrentTrueHealth` | ✅ Heal-on-trigger |

---

## Applicable Status Effects (`StatusEffect`)

These are the effects a perk can *apply* to a target.

### Damage
| Effect | Notes |
|---|---|
| `BasicDamage` | Instant damage |
| `PunchWithDelay` | Delayed melee hit |
| `CriticalHitWithDelay` | Delayed crit hit |
| `NextAbilityIncreasedDamage` | Empower next ability |
| `NextBasicAttackIncreasedDamage` | Empower next attack |
| `InstantKill` | One-shot |

### DoT
| Effect | Notes |
|---|---|
| `Burn` | Fire DoT |
| `Bleed` | Bleed DoT |
| `DamageWithBleed` | Hit + apply bleed |
| `Poison` | Poison DoT |
| `Shock` | Shock DoT |
| `ChainShock` | Chain lightning |
| `BurnDoubleExplosion` | Double burn explosion |
| `SpawnWitheredSeed` | Druid DoT seedling |

### CC
| Effect | Notes |
|---|---|
| `Stun` | Hard CC |
| `Root` | No movement |
| `Slow` | Reduced speed |
| `Chill` | Slowed + freeze chance |
| `Freeze` | Full CC |
| `ChillOrPoison` | Applies one of two |
| `ChillOrBurn` | Applies one of two |
| `HookToMe` | Pull to caster |
| `PushAway` | Knockback |

### Buffs
| Effect | Notes |
|---|---|
| `Bless` | Empowered attacks |
| `Fury` | Attack speed + damage |
| `Barrier` | Absorb shield |
| `Zen` | Dodge window |
| `ArmorPlate` | Add armor plate |
| `FullArmorPlates` | Restore all plates |
| `Enraged` | Damage ramp-up |
| `Taunt` | Force target |

### Debuffs
| Effect | Notes |
|---|---|
| `Curse` | Amplify damage taken |
| `SAP` | Reduce attack speed/power |
| `RemoveOneArmorPlate` | Strip armor |
| `BurnOrHealFlat` | Conditional burn or heal |

### Healing
| Effect | Notes | ThePit relevance |
|---|---|---|
| `HealingFlat` | Flat HP heal | ✅ |
| `HealingPercentage` | % HP heal | ✅ |
| `TrueHealingFlat` | Unmitigated flat heal | ✅ |
| `TrueHealingPercentage` | Unmitigated % heal | ✅ |
| `HealingRoomHealFlat` | Room heal flat | ❌ No healing rooms |
| `HealingRoomHealPercentage` | Room heal % | ❌ No healing rooms |

### Utility
| Effect | Notes | ThePit relevance |
|---|---|---|
| `Cleanse` | Remove debuffs | ✅ |
| `ReviveMe` | Trigger instant revive | ⚠️ Already instant in ThePit — this perk is moot |
| `ImmuneVFX` | Visual only | — |

---

## Conditions (Perk Gates)

These conditions can gate when a perk effect fires:

**Target conditions:** `IsElite` ❌, `IsBoss` ❌, `IsLowHealth`, `IsNearby`, `IsFarAway`  
**Status conditions:** `HasBleed`, `HasPoison`, `HasBurn`, `HasShock`, `HasChill`, `HasCurse`, `HasBless`, `HasFury`, `HasZen`  
**Self conditions:** `IsLowHealth`, `HasMaxHealth`, `IsInjured`, `WasRecentlyHit`, `WasRecentlyHealed`  
**Proximity:** `AllyNearby`, `MinionNearby`

> `IsElite` and `IsBoss` conditions are **always false** in ThePit — any perk conditioned on these will never activate.

---

## Dynamic Scaling

Perks can scale their effect by:
- `AttackCount` — increases per hit
- `KillCount` — increases per kill ✅
- `EliteKillCount` — ❌ always 0 in ThePit
- `ChainKillCount` — ✅ kill streaks
- `StackCount` — buff stack count
- `PropertyValue` — current stat value
- `HealthPercent` — current HP %

---

## ThePit Summary: Red Flags

| Perk trait | Why it's weak in ThePit |
|---|---|
| Trigger: `OnClearArena` | PvP arena is never "cleared" of enemies |
| Trigger: `OnEnterHealingRoom` | No healing rooms |
| Trigger: `OnEnterShop` | No shop |
| Trigger: `OnEnterArena` / `OnSceneStarting` | Fires once at the start — trivial uptime |
| Trigger: `OnRevived` / `OnKnockedOut` | Instant revive = near-zero window to exploit |
| Trigger: `OnEnemyDied` | Not triggered by player kills |
| Trigger: `OnEnemySpawned` | No enemy spawns |
| Condition: `IsElite` / `IsBoss` | Always false, perk never fires |
| `EliteKillCount` scaling | Always 0 |
| `StatusEffect.ReviveMe` | Already instant revive |
| `StatusEffect.HealingRoomHealFlat/Pct` | No healing rooms |
| `Property.Gold` / `LootLuckPCT` | No economy in arena |
