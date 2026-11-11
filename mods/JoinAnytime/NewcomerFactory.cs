using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RR;
using RR.Backend.Phoenix;
using RR.Game;
using RR.Game.Perk;
using RR.Level;

namespace JoinAnytime {
    /// <summary>
    /// Builds the Phoenix party member a newcomer is "restored" from, averaged over the live party.
    /// <para>
    /// Two steps, because perks can only be rolled once the newcomer has a slot and a champion
    /// (<see cref="PerkDatabase.GetRandomPerkAmount"/> works per <c>PlayerFilter</c>):
    /// <see cref="BuildSkeleton"/> at connect time (XP, ability levels, full health), then
    /// <see cref="FillPerks"/> when the game consumes the member to restore the spawned champion.
    /// </para>
    /// </summary>
    internal static class NewcomerFactory {
        // internal PhoenixPartyMember DungeonManager.BuildPartyMember(Player) — the game's own snapshot builder.
        private static MethodInfo _buildPartyMember;

        internal static bool Init() {
            _buildPartyMember = AccessTools.Method(typeof(DungeonManager), "BuildPartyMember", new[] { typeof(Player) });
            if (_buildPartyMember == null) {
                JoinAnytimeMod.PublicLogger.LogWarning("JoinAnytime: DungeonManager.BuildPartyMember not found — newcomers can't be averaged, mid-run joins disabled.");
                return false;
            }
            return true;
        }

        /// <summary>Snapshot of every live player except <paramref name="exclude"/>, built by the game.</summary>
        private static List<PhoenixPartyMember> SnapshotParty(Player exclude = null) {
            var dungeon = GameManager.Instance?.GetLevelManager() as DungeonManager;
            var pm = PlayerManager.Instance;
            if (dungeon == null || pm == null) { return new List<PhoenixPartyMember>(); }
            return pm.GetPlayers()
                .Where(p => p != exclude)
                .Select(p => _buildPartyMember.Invoke(dungeon, new object[] { p }) as PhoenixPartyMember)
                .Where(m => m != null)
                .ToList();
        }

        /// <summary>Connect time: XP and ability levels averaged over the party, full health, no talents or perks yet.</summary>
        internal static PhoenixPartyMember BuildSkeleton(Guid playerId) {
            var party = SnapshotParty();
            if (party.Count == 0) { return null; }

            int Avg(Func<PhoenixPartyMember, int> f) => (int)party.Average(m => (double)f(m));
            PhoenixAbilityRef Ability(Func<PhoenixPartyMember, PhoenixAbilityRef> f) => new PhoenixAbilityRef {
                XPLevel = Avg(m => f(m)?.XPLevel ?? 0),
                CurrentCharge = 1,          // charge abilities start full
                CooldownRemainingTime = 0f,
            };

            return new PhoenixPartyMember {
                PlayerId = playerId,
                IsHost = false,
                UserName = "",              // unknown until the join RPC; only used for the crash-recovery roster
                PlayableCharacterIndex = 0, // not used on the reconnect path — the client's own champion wins
                ChampionXPAmount = Avg(m => m.ChampionXPAmount),
                ChampionAbilityPoints = Avg(m => m.ChampionAbilityPoints),
                ChampionUltimatePoints = Avg(m => m.ChampionUltimatePoints),
                // ApplyMemberState dereferences all five without null checks.
                AbilityData_Attack = Ability(m => m.AbilityData_Attack),
                AbilityData_Power = Ability(m => m.AbilityData_Power),
                AbilityData_Special = Ability(m => m.AbilityData_Special),
                AbilityData_Defensive = Ability(m => m.AbilityData_Defensive),
                AbilityData_Ultimate = Ability(m => m.AbilityData_Ultimate),
                // SetHealthForRecovery clamps to max available health; the default 0 would spawn a 0-HP champion.
                ChampionHealthCurrent = float.MaxValue,
                ChampionInjury = 0f,
                ExtraLifeProductUsed = false,
                // Talents are champion-specific and the newcomer picks their own champion — leave them empty.
                ActivatedTalents = new List<PhoenixTalentRef>(),
                SelectableTalents = new List<PhoenixTalentRef>(),
                CollectedPerkIdsLevel0 = new List<int>(),
                CollectedPerkIdsLevel1 = new List<int>(),
                CollectedPerkIdsLevel2 = new List<int>(),
            };
        }

        /// <summary>
        /// Restore time (champion spawned, slot known): rolls random perks for the newcomer's champion, matching the
        /// party's average perk count and rarity per upgrade level. Mutates <paramref name="member"/> in place, right
        /// before the game applies it.
        /// </summary>
        internal static void FillPerks(PhoenixPartyMember member, Player newcomer) {
            var party = SnapshotParty(exclude: newcomer);
            if (party.Count == 0) { return; }

            var rolled = new List<PerkDescriptor>();
            RollLevel(party.Select(m => m.CollectedPerkIdsLevel0).ToList(), member.CollectedPerkIdsLevel0, newcomer, rolled);
            RollLevel(party.Select(m => m.CollectedPerkIdsLevel1).ToList(), member.CollectedPerkIdsLevel1, newcomer, rolled);
            RollLevel(party.Select(m => m.CollectedPerkIdsLevel2).ToList(), member.CollectedPerkIdsLevel2, newcomer, rolled);
        }

        private static void RollLevel(List<List<int>> partyLevelPerks, List<int> target, Player newcomer, List<PerkDescriptor> rolled) {
            var db = PerkDatabase.Instance;
            // Each party member's perks at this level, sorted by rarity, so slot i averages comparable perks.
            var byMember = partyLevelPerks
                .Select(ids => (ids ?? new List<int>())
                    .Select(db.PerkDescriptorByID)
                    .Where(p => p != null)
                    .OrderBy(p => (int)p.Rarity)
                    .ToList())
                .ToList();

            int count = (int)byMember.Average(list => (double)list.Count);
            for (int i = 0; i < count; i++) {
                var rarities = byMember.Where(list => i < list.Count).Select(list => (int)list[i].Rarity).ToList();
                var rarity = rarities.Count == 0 ? Rarity.Common : (Rarity)(int)rarities.Average();
                var roll = db.GetRandomPerkAmount(1, Category.None, rarity, newcomer.PlayerFilter, rolled);
                if (roll.Count == 0) { return; }
                rolled.Add(roll[0]);
                target.Add(roll[0].PerkID);
            }
        }
    }
}
