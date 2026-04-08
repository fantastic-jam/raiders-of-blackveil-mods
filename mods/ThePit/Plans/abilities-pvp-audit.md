# ThePit – Champion Ability PvP Audit

## Architecture

Each ability that needs PvP changes gets:

1. **A Harmony patch class** (`*Patch.cs`) that registers the patches and stores any required `FieldInfo`/`MethodInfo` handles resolved once in `Apply()`.

2. **A static Pvp class** (in the same file) that contains the reimplemented method logic, receiving `__instance` as first parameter. Private methods from the original are copied and rewritten here.

3. **`PvpDetector`** — complete replacement for all game detection mechanisms (`ActorColliderDetector`, `Physics.Overlap*`, `Physics.Raycast`). It is **not a patch** — it is a standalone utility with `includes` and `excludes` filter parameters on every method. The Pvp classes call it instead of the original game detection.

**Pattern variants:**

| Pattern | When to use | Return value from prefix |
|---|---|---|
| **Postfix** | Detection is additive — original finds 0 enemies in arena, postfix adds champion hits on top | N/A (original still runs) |
| **Prefix + replace** (return false) | Detection must be swapped entirely, or side effects must not run (ally buffs) | `false` to skip original |

**No global `DoDetectionPrefix` / `DoDetectionPostfix`** — that patch is removed. Every ability that needs Player-layer detection is handled per-ability.

**Self-damage** is blocked globally by `TakeBasicDamagePrefix`.  
**Cross-champion heals** are blocked globally by `AddHealthPrefix`.

---

## PvpDetector

`mods/ThePit/Patch/Abilities/PvpDetector.cs`

Replaces ALL game detection. Always uses `PvpLayerMask = ChampionDamageLayerMask | Player`.

```
includes  — if non-null: only return StatsManagers in this list
excludes  — if non-null: skip StatsManagers in this list
```

```csharp
Overlap(Collider col, StatsManager[] includes, StatsManager[] excludes)
OverlapBox(center, halfExtents, rotation, includes, excludes)
OverlapSphere(center, radius, includes, excludes)
OverlapCapsule(p0, p1, radius, includes, excludes)
Raycast(origin, direction, maxDistance, includes, excludes) → StatsManager
```

Usage patterns:
- Attack abilities: `excludes: new[] { self }` — hit everyone except self
- Ally abilities: `includes: new[] { self }` — only affect caster
- Tongue: `Raycast(excludes: new[] { self })` — first champion or enemy hit

Shared helpers also on PvpDetector: `ToggleHasHit(ChampionAbility)`, `IsLastComboPhase(ComboAttackAbility)`, `AttackDir(Component, StatsManager)`.

---

## Detection mechanisms (reference)

| Mechanism | Player layer? | Fix strategy |
|---|---|---|
| `ActorColliderDetector.DoDetection()` with `AllTargetForChampions` | ✗ | Postfix: run `PvpDetector.Overlap(collider, excludes: self)` and apply damage |
| `Physics.OverlapBoxNonAlloc(ChampionDamageLayerMask)` | ✗ | Postfix: `PvpDetector.OverlapBox(same params, excludes: self)` |
| `NetworkEnemyBase.AllEnemies` iteration | ✗ | Prefix+replace: iterate `PlayerManager.GetPlayers()` instead |
| `PlayerManager.GetPlayers()` (ally buff) | ✓ all | Prefix+replace: `PvpDetector.Overlap(collider, includes: self)` |
| `ProjectileCaster` | ? | Verify Unity Editor layer mask |

---

## Shameleon

### Attack (`ShameleonAttackAbility`)
- Original: `_normalHitDetector.DoDetection()` / `_lastHitDetector.DoDetection()`
- Public colliders: `normalHitCollider`, `lastHitCollider`
- **Pattern: POSTFIX on `DoHit()`**
  - `PvpDetector.Overlap(col, excludes: new[]{self})`
  - Phase check via `PvpDetector.IsLastComboPhase(instance)`

### Tongue Leap (`ShameleonTongueLeapAbility`)
- Tongue targeting: `Physics.Raycast(tongueHitMask)` — Player layer not in mask
- Public field: `tongueHitMask` — can be temporarily expanded
- **Pattern: PREFIX+POSTFIX on `FixedUpdateNetwork()`**
  - Prefix: save original `tongueHitMask`, OR in `Player` layer
  - Postfix: restore `tongueHitMask`
  - Result: tongue physically connects to champions. Jump-behind fires via wall-jump code.
  - Stun: TODO (requires accessing private `HitInfo` struct internals)

### Shadow Strike (`ShameleonShadowStrikeAbility`)
- Original: `Physics.OverlapBoxNonAlloc(hitCollider1/2, ChampionDamageLayerMask)`
- Public colliders: `hitCollider1`, `hitCollider2`
- **Pattern: POSTFIX on `DoHit()`**
  - `PvpDetector.OverlapBox(col.transform.position, col.transform.localScale/2, col.transform.rotation, excludes: new[]{self})`

### Shadow Dance (`ShameleonShadowDanceAbility`)
- Original: `NetworkEnemyBase.AllEnemies` in `RefreshPossibleTargets()`
- Public fields: `areaRadius`, `numberOfAttacks`, `ImpactEffects`
- Private: `damagePerAttack` (FieldInfo needed), `_spawnedShadowCount` (FieldInfo needed)
- **Pattern: PREFIX+REPLACE on `LetsDance()`**
  - Static champion hit-count dictionary keyed by caster ActorID
  - Collect living champions in `areaRadius` except self
  - Round-robin distribution: pick target with fewest hits
  - Apply damage, increment hit count

### Enter the Shadow (`ShameleonEnterTheShadowAbility`)
- **Status: ✓ No change needed.**

---

## Blaze

### Attack (`BlazeAttackAbility`)
- **Action: Verify projectile layer mask in Unity Editor.**

### Blast Wave (`BlazeBlastWave`)
- Original: `NetworkEnemyBase.AllEnemies` in all three methods
- Private fields: `_collectAngle`, `_collectDistance`, `_collectFrames`, `_pushWidth`, `_pushDistance`, `_pushDelayFrames`, `_pushFrames`, `DamageAfterPush`, `_damageTicks`, `_actionStarterTick` (Networked), `_pushDirection` (Networked), `_pushPosition` (Networked), `pushPos`
- **Pattern: PREFIX+REPLACE on `CollectAndGrabEnemies()`, `PushAwayEnemies()`, `DamageEnemies()`**
  - Maintain static `Dictionary<int, Dictionary<StatsManager, int>> _champTicks` (per instance key)
  - `CollectAndGrabEnemies`: OverlapSphere in front cone → push champions forward
  - `PushAwayEnemies`: OverlapSphere in wave zone → push + schedule damage tick
  - `DamageEnemies`: process `_champTicks`, apply `TakeBasicDamage`

### Devastation (`BlazeDevastation`)
- **Action: SunStrikeArea patch (below) covers the area damage.**

### Special Area — Heat Aura (`BlazeSpecialArea`)
- Original: `UpdateAuraEffect()` buffs ALL champions in range with `CriticalStrikeChance`
- Public: `criticalChanceIncrementPCT`, `ActRadius`
- Private: `AlliesInside` (FieldInfo needed)
- Caster reference: try `areaCaster` on `AbilityArea` via `AccessTools.Field`; fallback = closest champion to area center
- **Pattern: PREFIX+REPLACE on `UpdateAuraEffect(bool)`**
  - Only allow caster into `_tempStatsList`
  - Remove buff from all non-casters in `AlliesInside`

### Sun Strike (`BlazeSunStrike`) → `SunStrikeArea`
- Original: `_hitDetector.DoDetection()` in `DamageCheck()`
- Public: `HitCollider` (CapsuleCollider), `Caster` (Networked NetworkCharacterBase), `Damage`
- **Pattern: POSTFIX on `DamageCheck()`**
  - `PvpDetector.Overlap(HitCollider, excludes: new[]{Caster.Stats})`

### Heating Up (`BlazeHeatingUp`)
- **Status: ✓ No change needed.**

---

## Beatrice

### Attack / Entangling Root
- **Action: Verify projectile layer mask in Unity Editor.**

### Fertile Soil (`BeatriceFertileSoilAbility`)
- **Status: ✓ Already handled** — `AddHealthPrefix` blocks cross-champion heals.

### Lotus Flower → `BeatriceSpecialObject`
- Original: `FlowerEffect()` calls `_hitDetector.OverlapSphere()` → `AddArmorPlate()` on all champions
- Public: `_charRef` (NetworkCharacterBase — the caster)
- **Pattern: PREFIX+REPLACE on `FlowerEffect()`**
  - Only call `_charRef.Stats.Protection?.AddArmorPlate()` — skip all other champions

### Man Eater Plant → `ManEaterPlantBrain`
- Original: `_hitDetector.DoDetection()` in `HitEnemiesInArch()`
- Public: `dealDamageCollider`, `dealDamageAngle`, `damage`, `ImpactEffects`, `_creator` (StatsManager)
- **Pattern: POSTFIX on `HitEnemiesInArch()`**
  - `PvpDetector.Overlap(dealDamageCollider, excludes: new[]{_creator})`
  - Apply same angle (dot product) filter as original

### Role Ability (`BeatriceRoleAbility`)
- **Status: ✓ No change needed.**

---

## Rhino

### Attack (`RhinoAttackAbility`)
- Original: `_normalHitDetector.DoDetection()` / `_lastHitDetector.DoDetection()`
- Public colliders: `normalHitCollider1`, `normalHitCollider2`, `lastHitCollider1`
- **Pattern: POSTFIX on `DoHit()`**
  - `PvpDetector.Overlap(col, excludes: new[]{self})`
  - Include push force for last hit

### Earthquake (`RhinoEarthquakeAbility`)
- Original: `Physics.OverlapBoxNonAlloc(ChampionDamageLayerMask)` in `FixedUpdateNetwork()` wave loop
- Public: `waveSpeed`, `waveDistance`, `waveStartWidth`, `waveFinishWidth`, `stunDuration`, `damagePerHit`, `ImpactEffects`
- Private (FieldInfo/PropertyGetter): `WaveLinearPosition` (Networked), `_waveStartPos`, `_waveDirection`, `_hittedEnemies`
- **Pattern: POSTFIX on `FixedUpdateNetwork()`**
  - Guard: `WaveLinearPosition > 0 && WaveLinearPosition <= waveDistance`
  - Compute same wave box as original (using public size params)
  - `PvpDetector.OverlapBox(center, halfExtents, orientation, excludes: new[]{self})`
  - Hit only `IsChampion` targets not in `_hittedEnemies`
  - Apply `TakeBasicDamage` + stun (no `IsEnemy` guard)

### Shields Up (`RhinoShieldsUpAbility`)
- Original: `_hitDetector.DoDetection()` in `HitEnemies(float absorbedDamage)`
- Public: `dealDamageCollider`, `ImpactEffects`
- Protected (FieldInfo): `baseDamage`, `absorbedDamageMultiplier`, `MaximumDamageOutputMultiplier`, `dealDamageAngle`
- **Pattern: POSTFIX on `HitEnemies(float absorbedDamage)`**
  - `PvpDetector.Overlap(dealDamageCollider, excludes: new[]{self})`
  - Apply same angle filter as original
  - Compute damage from reflected field values

### Stampede (`RhinoStampedeAbility`)
- Original: `_grabHitDetector.DoDetection()` in `DetectEnemiesToGrab()`
- Public: `grabCollider`, `damagePerHit`, `ImpactEffects`
- Private (FieldInfo): `_chargeDirection`, `_grabbedEnemies`, `_grabbedThanThrowedEnemies`
- Private (MethodInfo): `GrabActor(StatsManager)`
- **Pattern: POSTFIX on `DetectEnemiesToGrab()`**
  - `PvpDetector.Overlap(grabCollider, excludes: new[]{self})`
  - For champion hits: damage, then `GrabActor` via reflection if not protected

### Spin (`RhinoSpinAbility`)
- Original: `_hitDetector.DoDetection()` in `DoHit()`
- Public: `hitCollider` (CapsuleCollider), `SpinImpactEffect`
- Private (FieldInfo): `damagePerCycle`, `_totalHitList`
- **Pattern: POSTFIX on `DoHit()`**
  - `PvpDetector.Overlap(hitCollider, excludes: new[]{self})`
  - Get damage float from reflected `damagePerCycle` field
  - Maintain `_totalHitList` for event triggers

---

## Summary table

| Ability | Pattern | Key collider / detection |
|---|---|---|
| Shameleon Attack | Postfix DoHit | `normalHitCollider` / `lastHitCollider` |
| Shameleon Tongue (expand mask) | Prefix+Postfix FUN | `tongueHitMask` OR Player layer |
| Shameleon Shadow Strike | Postfix DoHit | `hitCollider1` / `hitCollider2` OverlapBox |
| Shameleon Shadow Dance | Prefix+Replace LetsDance | PlayerManager.GetPlayers() |
| Shameleon Enter Shadow | ✓ none | — |
| Blaze Attack | verify projectile | — |
| Blaze Blast Wave | Prefix+Replace ×3 | OverlapSphere + _champTicks |
| Blaze Devastation | covered by SunStrikeArea | — |
| Blaze Special Area | Prefix+Replace UpdateAuraEffect | includes: caster only |
| Blaze Sun Strike | Postfix DamageCheck | `HitCollider` |
| Blaze Heating Up | ✓ none | — |
| Beatrice Attack | verify projectile | — |
| Beatrice Entangling Root | verify projectile | — |
| Beatrice Fertile Soil | ✓ AddHealthPrefix | — |
| Beatrice Lotus Flower | Prefix+Replace FlowerEffect | `_charRef.Stats` only |
| Beatrice Man Eater Plant | Postfix HitEnemiesInArch | `dealDamageCollider` |
| Beatrice Role Ability | ✓ none | — |
| Rhino Attack | Postfix DoHit | `normalHitCollider1/2`, `lastHitCollider1` |
| Rhino Earthquake | Postfix FUN wave | `PvpDetector.OverlapBox` computed |
| Rhino Shields Up | Postfix HitEnemies | `dealDamageCollider` |
| Rhino Stampede | Postfix DetectEnemiesToGrab | `grabCollider` |
| Rhino Spin | Postfix DoHit | `hitCollider` |

---

## File layout

```
mods/ThePit/Patch/Abilities/
  PvpDetector.cs                   — universal detection replacement
  ShameleonAttackPatch.cs
  ShameleonShadowStrikePatch.cs
  ShameleonShadowDancePatch.cs
  ShameleonTongueLeapPatch.cs
  BlazeBlastWavePatch.cs
  BlazeSpecialAreaPatch.cs
  BeatriceSpecialObjectPatch.cs
  ManEaterPlantBrainPatch.cs
  SunStrikeAreaPatch.cs
  RhinoAttackPatch.cs
  RhinoEarthquakePatch.cs
  RhinoShieldsUpPatch.cs
  RhinoStampedePatch.cs
  RhinoSpinPatch.cs
```

Each `*Patch.cs` contains the patch class (with `Apply(Harmony harmony)`) and the Pvp static class in the same file.  
`ThePitPatch.Apply()` calls each sub-patch's `Apply(harmony)` and removes the old global DoDetection patch.
