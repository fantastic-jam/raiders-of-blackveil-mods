# ThePit — Ability PvP Coverage

## Core principle: every ability is duplicated

ThePit cannot modify game ability classes directly. Instead, every ability that can deal or apply effects has a **parallel PvP implementation** that runs alongside the original. The game's detection misses champions (Player layer excluded from all ability masks), so ThePit intercepts after the original fires and applies the PvP-aware version.

**Adding a new champion = auditing every ability it has and picking the right pattern below.**

---

## Pattern decision table

| Ability type | Pattern | When |
|---|---|---|
| Collider-based attack (`ActorColliderDetector`) | **Sidecar** | `DoHit` uses `_normalHitDetector.DoDetection()` |
| Projectile-based attack (`ProjectileCaster`) | **Mask expansion** | Ability has a `_projectileCaster` field |
| Single-fire area / special | **Direct postfix** | Fires once, has a `HitCollider` or explicit overlap |
| Perk area (`AreaCharacterSelector`) | **AreaCharacterSelector patch** | Ability spawns an area that uses `AreaCharacterSelector` |
| Minion (`NetworkNPCBase`) | **Minion patch** | NPC that selects its own target |

---

## Pattern 1 — Sidecar (`PvpActorColliderDetector` + `Pvp[X]Ability`)

For abilities whose `DoHit` calls `ActorColliderDetector.DoDetection()`. The game's detector never includes the Player layer, so the sidecar creates its own `PvpActorColliderDetector` (which always uses `PvpDetector.PvpLayerMask`) and replays detection after the original fires.

**Files involved:** `[X]AttackPatch.cs`, `Pvp[X]Ability.cs`, `PvpActorColliderDetector.cs`

See `docs/mod_best_practices.md` §13 for the full sidecar implementation guide.

**Current abilities using this pattern:**

| Ability class | Patch | Sidecar |
|---|---|---|
| `RhinoAttackAbility` | `RhinoAttackPatch` | `PvpRhinoAttackAbility` |

---

## Pattern 2 — Mask expansion (`ProjectileCaster`)

For abilities that fire projectiles via `ProjectileCaster`. The game sets the caster's layer masks in the Unity Editor without the Player layer. The fix expands `_layerMask`, `_layerMaskCharacters`, and `_layerMaskDamage` to include `Player`, and disables `_excludeCasterLayer` (which otherwise strips the whole Player layer from detection when the champion root is on that layer).

Self-damage is blocked globally by `ThePitPatch.TakeBasicDamagePrefix`, not per-caster.

**Lifecycle:**
- `SpawnedPostfix`: save originals, expand masks (only if arena is active).
- `ExpandAllCasters()`: called from `MatchController.StartArena()` for abilities already spawned before arena entry.
- `ResetAllCasters()`: called from `ThePitState.ResetMatchState()` — restores originals and clears the save dict.

**All ProjectileCaster patch classes are structurally identical.** Copy `BlazeAttackPatch` when adding a new one; only the ability type and field name change. The duplication is intentional — a shared base would complicate the reflection field storage without a meaningful benefit.

**Current abilities using this pattern:**

| Ability class | Patch | Field |
|---|---|---|
| `BlazeAttackAbility` | `BlazeAttackPatch` | `_projectileCaster`, `_projectileCasterLastShot` |
| `BeatriceAttackAbility` | `BeatriceAttackPatch` | `_projectileCaster` |
| `BeatriceEntanglingRootAbility` | `BeatriceEntanglingRootsPatch` | `_projectileCaster` |
| `BeatriceLotusFlowerAbility` | `BeatriceLotusFlowerPatch` | `_projectileCaster` |

---

## Pattern 3 — Direct postfix (`PvpDetector.Overlap`)

For abilities that fire once (not per-tick), have a single `HitCollider` or explicit overlap call, and where detection can be replicated in a simple postfix. Call `PvpDetector.Overlap(collider, excludes: new[] { self })` and apply damage directly.

Use a per-instance `HashSet<NetworkId>` guard when the postfixed method can fire more than once per cast (e.g. `SunStrikeArea.DamageCheck`).

**Current abilities using this pattern:**

| Ability / class | Patch |
|---|---|
| `SunStrikeArea` | `SunStrikeAreaPatch` |
| `BlazeBlastWave` | `BlazeBlastWavePatch` |
| `BlazeSpecialArea` | `BlazeSpecialAreaPatch` |
| `ManEaterPlantBrain` | `ManEaterPlantBrainPatch` |
| `ShameleonShadowDance` | `ShameleonShadowDancePatch` |
| `ShameleonTongueLeap` | `ShameleonTongueLeapPatch` |
| `ShameleonShadowStrike` | `ShameleonShadowStrikePatch` |
| `BeatriceSpecialObject` | `BeatriceSpecialObjectPatch` |

---

## Pattern 4 — AreaCharacterSelector patch

For perks and area effects routed through `AreaCharacterSelector.SelectCandidates`. A single patch covers all uses (BlazeDevastation, FertileSoil, Daggers, Axes, Health regen, etc.).

- **Enemy mode** (`_actOnEnemies = true`): calls `CheckCandidate` for each living opponent champion.
- **Ally mode** (`_actOnEnemies = false`): strips non-owner champions from `_candidates`.

No new patch needed when a new perk uses `AreaCharacterSelector` — the existing `AreaCharacterSelectorPatch` covers it automatically.

**File:** `AreaCharacterSelectorPatch.cs`

---

## Pattern 5 — Minion targeting

Minions use `NetworkNPCBase.SelectEnemyTarget` which only scans `AllEnemies`. Two postfixes:

- `SelectEnemyTargetPostfix`: if no enemy found, scan for the nearest living champion within `AttackDistance` from `MinionOwner`.
- `GeneralAttackConditionsOkPostfix`: mirror check so the attack actually fires.

**Files:** `ChampionMinionPatch.cs`, `ManEaterPlantBrainPatch.cs`

---

## Champion coverage matrix

| Champion | Normal attack | Special | Ultimate | Other |
|---|---|---|---|---|
| Rhino | Sidecar (Pattern 1) | — | — | Earthquake, ShieldsUp, Spin, Stampede: Pattern 3 |
| Blaze | Mask expansion (Pattern 2) | BlazeBlastWave: Pattern 3 | BlazeSpecialArea: Pattern 3 | — |
| Beatrice | Mask expansion (Pattern 2) | EntanglingRoots: Pattern 2 + ApplyRoot self-root guard | LotusFlower: Pattern 2 | SpecialObject: Pattern 3; WitheredSeedBrain: Pattern 2+5 |
| Shameleon | ShameleonAttack: Pattern 3 | ShadowDance: Pattern 3 | TongueLeap: Pattern 3 | ShadowStrike: Pattern 3 |
| ManEaterPlant | ManEaterPlantBrain: Pattern 3+5 | — | — | — |
| SunStrike | — | SunStrikeArea: Pattern 3 | — | — |
| *(perks)* | — | — | — | All via Pattern 4 |
