# ThePit — Level Events

**Status:** Draft
**Mode:** Perk Draft (FFA arena)
**Author note:** Events are mid-match interruptions that add variety and pressure without overturning a lead. All events are host-triggered and server-authoritative.

---

## Design Goals

1. **Break positional comfort.** Players who find a safe corner or a consistent loop get disrupted.
2. **Create dramatic moments, not coin flips.** No event should randomly kill the leader and hand the match to someone else. Every event has counterplay.
3. **Short, legible, resolved.** Each event has a clear start, a clear end, and leaves the arena in a clean state afterward.
4. **Lore-coherent.** The Pit is a sanctioned proving ground inside a Blackveil facility. Events should feel like the arena management throwing in complications — not random acts of god.

---

## Trigger System

Events fire on a **random interval timer**: every **60–90 seconds** the host rolls a weighted event table and fires one event. No event fires during:
- The 20-second grace period
- An active respawn invincibility window (the event *starts* on schedule, but the newly-spawned player still gets their invincibility)
- The final 30 seconds of the match (let the closing fights breathe)

At most **one event is active at a time.** If an event is still resolving when the next roll fires, the roll is skipped and retried 15 seconds later.

**Tuning handles:**
- `EventIntervalMin` — default 60s
- `EventIntervalMax` — default 90s
- `EventCooldownAfterSkip` — default 15s
- Per-event `Weight` — controls relative frequency; set a weight to 0 to disable an event

---

## Event 1 — Blackveil Strike Team

**Category:** Specified — Enemy Spawn

**1-line description:** A Blackveil security response team drops into the arena and hunts all players.

### Lore framing
The Pit's management occasionally releases corporate goons to keep the fighters on their toes. This is, officially, "environmental enrichment."

### Trigger
Random interval roll. Can fire at any point in the match after the grace period ends.

### Duration / Resolution
- Enemies spawn and remain active until **killed or until 45 seconds elapses**, whichever comes first.
- At 45 seconds, any surviving Strike Team members despawn (they retreat on a horn signal).
- After resolution, a 20-second cooldown is enforced before the next event can roll (separate from the base interval).

### Enemy Types
Use existing Blackveil enemy types already present in the Meat Factory. Do not use boss-tier enemies.

Preferred types (in order of preference):
1. **Snipers** — high single-target damage at range, force players out of open areas
2. **Bandit-type melee enemies** — aggressive chasers, good for pressuring a static fighter
3. **Mortar-modifier enemies** — area denial, repositioning pressure

Avoid: bosses, minibosses, any enemy with a scripted intro sequence, enemies with health-regeneration modifiers (Regenerators would make the 45s despawn guarantee feel random).

### Count
- **2 players in match:** 2 enemies
- **3 players in match:** 3 enemies

Rationale: one enemy per player. Each fighter has to manage their own threat while dealing with opponents. A 3v1 on any single enemy trivialises the event; equal count forces genuine splitting of attention.

### Spawn Location
Spawn at the **arena perimeter** — the outer edge of the playable space, not near any current player position. Pick spawn points that are at least 8 units from the nearest player at the moment of spawn. If no such point exists (all players bunched), spawn at the pre-defined perimeter anchors anyway.

Do not spawn behind players (the "teleport into your back" feel is disorienting, not dramatic).

### Targeting Behavior
**Random target assignment at spawn.** Each Strike Team member is assigned a random living player and pursues them as primary target. If their assigned target dies, they re-roll to a living player. This is intentionally random rather than "attack the leader" — punishing the leader specifically would feel like a rubber-band system, which this mode is not trying to be.

### Kill Credit
- A player who kills a Strike Team member receives **no kill score** (it's not a PvP kill).
- However, killing a Strike Team member gives a **small stat drop** near the enemy's death position (e.g., a temporary speed boost pickup or a small health orb). This rewards engaging the event rather than ignoring it.
- If a player is killed by a Strike Team member, the death counts as a **normal death** (respawn applies, no kill is awarded to anyone).

### Player Counterplay
- Dodge and reposition — Strike Team enemies are not tracking missiles, they path normally
- Use other players as indirect shields (let the enemy fixate on a distracted opponent)
- Kill the enemy quickly to claim the stat drop before opponents do
- Ignore the enemy and risk eating damage while fighting a player — valid but costly

### Balance Risks
- **Risk: enemy kills the leader from full health, hands a point to no one.** Mitigation: keep enemy damage low enough that a healthy player can survive one engagement. Strike Team should feel like additional pressure, not execution.
- **Risk: two players team up to trivialise the enemies, then resume fighting.** This is acceptable — temporary informal alliances are a feature of 3-player FFA, not a bug.
- **Risk: enemies interfere with an active duel in ways that feel unfair.** Mitigation: the 45-second hard despawn caps the chaos window. Players who hate the event just need to survive 45 seconds.

---

## Event 2 — Pit Ordinance

**Category:** Specified — Bombs

**1-line description:** The arena management seeds the floor with explosive charges. Move or be blown up.

### Lore framing
Standard Pit maintenance equipment. Totally safe. Liability waived upon entry.

### Trigger
Random interval roll.

### Duration / Resolution
- **Warning phase:** 3 seconds. Charges appear on the ground as **clearly visible glowing markers** (use the existing `TrapExplosion` hazard type that already exists in the arena scene). No damage during warning.
- **Detonation:** charges explode simultaneously at the end of the warning phase.
- **Cleanup:** all markers and blast effects are gone within 1 second of detonation. The event is fully resolved.
- Total event duration: approximately 4–5 seconds.

### Placement
**Fixed pattern, not random.** Use a pre-defined set of 5–7 blast positions distributed across the arena floor. Divide the arena into a rough grid and place one charge per grid cell, offset slightly by small random scatter (±0.5 units) to avoid feeling perfectly mechanical. The pattern should always leave **at least 30% of the floor safe** — players should never have nowhere to stand.

Do not place charges on top of player spawn points or on the floor door landmark.

### Blast Parameters
- **Warning indicator diameter:** 3 units (visible blast radius preview)
- **Actual blast radius:** 2.5 units
- **Damage:** 30–40% of current player max HP per hit. Not enough to one-shot a healthy player; enough to one-shot a player already at low health.
- **Knockback:** moderate — enough to interrupt a combo, not enough to throw a player into a wall.
- **Damage type:** treated as **environmental damage**, not PvP damage. No kill credit is awarded for a bomb kill. If a bomb kills a player, that player simply dies and respawns normally.

### Can Charges Be Triggered Early?
No. The charges are purely timer-driven. Players cannot interact with them. This keeps the event clean and legible — "glowing thing on floor = move your feet."

### Visual / Audio Cues
- On appearance: a distinct audio cue (not the same sound as trap activation) plays arena-wide so all players are immediately aware
- Warning phase: charges pulse in color (e.g., white to red) over the 3 seconds — a clear ramp signal
- 1 second before detonation: charges flash rapidly (rapid pulse) — the final "move NOW" signal
- Detonation: standard explosion VFX and sound
- The color-to-flash progression must be readable at a glance during active combat; do not rely on a HUD notification alone

### Player Counterplay
- Watch the floor and move off marked positions
- Use the 3-second window to reposition advantageously (e.g., dodge behind a bomb cluster to deter pursuit)
- Use the detonation to bait a chasing player into a blast zone
- If fighting in close range, disengage briefly during the warning phase

### Balance Risks
- **Risk: a cornered low-HP player gets deleted by a bomb and the kill is wasted.** Mitigation: no-kill-credit rule means the leading player is not punished, but is not rewarded either. Neutral event for scoring.
- **Risk: charge placement overlaps with the only viable position during a duel.** Mitigation: the fixed-pattern guarantee that 30% of floor is always safe. Document the pattern at implementation time.
- **Risk: 3-second warning is too short for low-awareness players.** The audio cue is the primary signal, not the visual. If playtesting shows players are dying to bombs they didn't notice, increase warning to 4 seconds before touching damage numbers.

---

## Event 3 — Power Surge

**1-line description:** A stat-amplifying pickup appears at arena center; the first player to reach it gets a significant but time-limited boost.

### Lore framing
The Pit's management occasionally throws in a stimpack to encourage more aggressive fighting. Whether it's a loyalty reward or bait is left as an exercise for the competitors.

### Trigger
Random interval roll. Weighted slightly higher than other events — this one resolves quickly and cleanly.

### Duration / Resolution
- A **single pickup** spawns at arena center (at the floor door landmark, or offset slightly if a player is standing there).
- The pickup persists for **15 seconds** — if no one collects it, it despawns with a poof effect.
- On collection: the collecting player receives a **30-second buff** (see parameters).
- After collection or despawn, the event is resolved.

### Mechanical Parameters
The Power Surge buff grants one of the following (randomly selected each event from this list):
- **Bloodrush:** +30% movement speed for 30 seconds
- **Iron Hide:** +25% damage reduction for 30 seconds
- **Predator:** +25% damage dealt for 30 seconds

Only one variant fires per event. The buff variant is **announced to all players** when the pickup spawns (text callout: "BLOODRUSH SURGE" / "IRON HIDE SURGE" / "PREDATOR SURGE") so opponents know what they're racing for and what the collector just received.

Buff duration: 30 seconds. Non-stackable — collecting a second Power Surge while buffed refreshes, does not stack.

### Player Counterplay
- Race for the pickup — pure aggression play
- Contest the player going for it — intercept and fight them before they reach center
- Concede the pickup and play around the buff: kite a Predator-buffed player, pressure a Bloodrush-buffed player who may overextend, burst an Iron Hide player before the reduction matters
- Ignore it entirely if you are already winning a duel — valid but risky if an opponent gets it

### Balance Risks
- **Risk: the leading player always wins the race and runs away with the match.** Mitigation: the pickup spawns at center, which is inherently contested. A leading player may be busy fighting, not positioned centrally. The 15-second despawn window is short enough to resolve quickly if no one wants to commit.
- **Risk: the buff feels decisive — 30% damage for 30 seconds is a lot.** Intentional. This event is about creating a high-stakes moment. The buff should feel worth fighting over. If playtesting shows it creates unrecoverable leads, reduce to 20% damage / 20% speed but keep the drama.
- **Risk: players will always rush center and ignore each other.** If this becomes a consistent behavior, add a 5-second delay before the pickup becomes active (it appears, then unlocks) — gives players time to position rather than sprinting blindly.

---

## Event 4 — Blackout

**1-line description:** The arena lights cut out, reducing visibility for all players equally for a short window.

### Lore framing
Power fluctuations are a known hazard in Blackveil facilities. Maintenance has been notified. Maintenance is not coming.

### Trigger
Random interval roll. Lower weight than other events — this one is high-impact and should feel rare.

### Duration / Resolution
- Arena lighting dims to **near-dark** (not pitch black — players can still see outlines and ability effects at close range, but map reading and long-range tracking become difficult).
- Duration: **12 seconds**, then lights restore.
- Total event duration: 12 seconds.

### What Changes During Blackout
- **Long-range combat becomes harder.** Ranged champions (Jamera/Blaze) lose their natural positioning advantage because they cannot reliably track targets at distance.
- **Ability visual effects remain fully visible.** Projectiles, AoE indicators, and champion glows are unaffected — only ambient lighting dims. This ensures the Blackout is disorienting but not unfair: you can still read attacks, you just lose the spatial context of the arena.
- **The floor door landmark and arena boundary remain faintly lit.** Players can navigate; they just cannot clearly see opponents across the arena.

### Player Counterplay
- Melee champions: close the distance aggressively — this is your window
- Ranged champions: play tighter, use AoE, don't commit to long shots you can't confirm
- All players: listen for audio cues (ability sounds, footsteps, impact sounds) — the audio landscape becomes more important
- If you're ahead on HP, play defensively for 12 seconds rather than fighting into uncertainty

### Balance Risks
- **Risk: Blackout disproportionately punishes ranged champions, making champion choice a coinflip into this event.** Mitigation: "near-dark" not "pitch black." Projectile and AoE VFX are fully visible. A skilled Jamera player can still land shots on a player who fires first — they just can't pick them out from across the arena passively.
- **Risk: 12 seconds feels too long if it catches two players mid-duel.** It's intended to interrupt duels, not resolve them. 12 seconds ends before most duels do. If playtesting suggests it's too long, 8 seconds is the minimum that still makes the event feel present.
- **Risk: the lighting change requires engine-level support that is difficult to implement.** If post-processing fog or ambient light manipulation is unavailable, a simpler proxy is acceptable: a heavy fog-of-war particle volume that fills the arena. This must be tested against readability of ability VFX before shipping.

---

## Event 5 — Bounty Mark

**1-line description:** One player is marked as the Bounty target; any other player who kills them earns a bonus reward.

### Lore framing
The Pit's judges occasionally single out an exceptional fighter for the crowd's entertainment. Being marked is an honor. Surviving being marked is a greater one.

### Trigger
Random interval roll. Only fires when there are **3 players alive** (meaningless in a 2-player situation, and cannot fire if one player is currently dead/respawning). Lower weight — like Blackout, this should feel special.

### Duration / Resolution
- One living player is selected at **random** (weighted slightly toward the current kill leader — see balance note below) and marked.
- **Marked player is clearly indicated** to all: a persistent visual effect above their character (a glowing reticle or colored aura), a text callout naming them, and a persistent HUD indicator.
- The mark lasts until:
  - The marked player is killed by another player (mark resolved: killer receives the Bounty reward), or
  - **30 seconds elapse** without the marked player dying (mark expires: the marked player receives a smaller reward for surviving)
- After resolution, event ends.

### Bounty Reward (on kill)
The killer receives a **perk drop** — a physical perk pickup spawning at the kill location, claimable by anyone. This is a significant reward (perks are the primary power currency in Perk Draft mode) but is in-world claimable, so the third player can contest it.

### Survival Reward (if mark expires)
The marked player receives a **stat boost pickup** spawned near their position — smaller than the Bounty kill reward. This is the "you survived the Pit's gauntlet" consolation and provides a mild incentive to play defensively without making turtling a reliable strategy (30 seconds is a long time to avoid two motivated players).

### Marked Player Selection
- Pure random is the simplest and fairest approach.
- A mild weighting toward the current kill leader (60/40 if the leader has 2+ more kills than others) prevents the event from being purely neutral — it creates slight catch-up pressure without being an explicit rubber-band system. The leader is not guaranteed to be marked; they are more likely to be marked.
- Do not mark a player who is currently in their respawn invincibility window.

### Player Counterplay (marked player)
- Kite both opponents — use the arena's full space
- Force a 1v1 by baiting opponents into fighting each other (the Bounty goes to whoever makes the kill, so opponents may contest each other)
- Play very defensively for 30 seconds — survive the mark
- Use escape abilities aggressively (Shameleon stealth, Jamera pushback, etc.)

### Player Counterplay (hunters)
- Coordinate informally to pressure the marked target
- But remember: the Bounty perk drop is contestable — killing the target and then losing the drop to the third player is a real risk
- Avoid getting eliminated by the marked player while chasing the Bounty

### Balance Risks
- **Risk: marking the leader every time creates an obvious rubber-band feel.** Mitigation: 60/40 weighting, not 100%. There is no guarantee the leader is marked. If the randomness feels punishing, remove the leader weighting entirely and go pure random.
- **Risk: the perk drop reward is too strong — creates a runaway state for the hunter.** Mitigation: the drop is claimable by anyone, so it's contested. If perk drops are too powerful mid-match, substitute a stat boost drop (same as Strike Team kill reward tier) instead.
- **Risk: the marked player simply hides for 30 seconds and collects the survival reward.** This is a valid counterplay but it comes at the cost of 30 seconds of scoring time. In a 10-minute match, 30 seconds of inactivity is meaningful. If turtling becomes dominant strategy for marked players, reduce survival reward to nothing and make the mark purely threatening.
- **Risk: the event text/UI callout naming the marked player could feel embarrassing or targeting in a bad-faith way.** Use neutral game-world framing: "A Bounty has been placed on [Champion Name]" not anything personal.

---

## Summary Table

| # | Event Name | Duration | Key Effect | Counterplay Type |
|---|---|---|---|---|
| 1 | Blackveil Strike Team | 45s max | NPC enemies hunt players | Combat or avoidance |
| 2 | Pit Ordinance | ~5s | Bomb pattern forces repositioning | Spatial awareness |
| 3 | Power Surge | 15s window + 30s buff | Pickup grants a strong time-limited stat buff | Racing / baiting |
| 4 | Blackout | 12s | Arena lighting cuts, hurts ranged tracking | Melee opportunity / patience |
| 5 | Bounty Mark | 30s | One player is marked; killing them earns a perk drop | Cat-and-mouse / survivalism |

---

## Shared Implementation Notes

These are design constraints that the implementation must respect. Technical details belong in the dev docs, but these constraints are design-owned:

- **All events are server-authoritative.** The host triggers, resolves, and cleans up every event. Clients receive state and visuals only.
- **Events must leave the arena in a clean state.** Any spawned enemy, marker, pickup, or effect must be explicitly despawned/cleared at event resolution — no persistent residue that confuses the next event.
- **No event stacks with another.** Only one active event at a time. The trigger system is responsible for enforcing this.
- **Environmental deaths during events do not award kill score.** Bomb deaths, Strike Team deaths — these are neutral. The kill score system only increments on player-vs-player kills.
- **Buff and mark states must be networked.** Power Surge buff duration and Bounty Mark status must be visible to all clients, not just the server.

---

## Open Questions

- What announcement/callout system exists for mid-match events? Is there an announcer voice, a text banner, both, or neither? Events like Bounty Mark and Power Surge rely on legible mid-match communication.
- Can `TrapExplosion` be dynamically spawned at runtime at arbitrary positions, or must charges be pre-placed in the scene?
- What existing NPC spawn points exist in the arena (Slash & Bash) scene? Strike Team spawn placement depends on what the scene geometry offers.
- Is post-processing (lighting / fog) accessible at runtime via mod code, or is Blackout only feasible via a particle volume proxy?
- What is the respawn duration? This affects whether Bounty Mark can accidentally fire while someone is respawning (the guard in the trigger condition needs to know when a respawn window is active).
