using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Stats;
using ThePit.FeralEngine;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Fires an instant damage burst + radial push on all nearby champions when Blaze teleports.
    // Triggered once per ultimate use at the PrepareToTeleport → FollowThrough state transition.
    internal class PvpBlazeDevastation {
        // BlazeDevastation.InternalState enum values (Inactive=0, Aiming=1, WindUp=2, Prepare=3, Follow=4, Cancel=5)
        private const int StateWindUp = 2;
        private const int StatePrepareToTeleport = 3;
        private const int StateFollowThrough = 4;
        private const int StateCancelFrames = 5;
        private const float BurstRadius = 8f;
        private const float PushStrength = 20f;

        private static PropertyInfo _innerStateProp;
        private static PropertyInfo _connectedUserActionProp;
        private static MethodInfo _takeBasicDamageMethod;

        private readonly BlazeDevastation _inst;
        private int _prevInnerState;
        private bool _isRecast;

        internal PvpBlazeDevastation(BlazeDevastation inst) {
            _inst = inst;
        }

        internal static void Init() {
            _innerStateProp = AccessTools.Property(typeof(ChampionAbility), "InnerState");
            _connectedUserActionProp = AccessTools.Property(typeof(ChampionAbility), "ConnectedUserAction");
            foreach (var m in typeof(StatsManager).GetMethods()) {
                if (m.Name != "TakeBasicDamage") { continue; }
                var p = m.GetParameters();
                if (p.Length >= 1 && p[0].ParameterType != typeof(float)) { _takeBasicDamageMethod = m; break; }
            }
            if (_innerStateProp == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbility.InnerState not found — Blaze arrival burst inactive.");
            }
            if (_takeBasicDamageMethod == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: StatsManager.TakeBasicDamage not found — Blaze arrival burst inactive.");
            }
        }

        internal void OnFixedUpdate() {
            if (_innerStateProp == null || _takeBasicDamageMethod == null) { return; }
            if (!_inst.Object.HasStateAuthority) { return; }

            int currentState = (int)_innerStateProp.GetValue(_inst);

            // Track whether PrepareToTeleport was entered from WindUp (first cast = arrive)
            // or from CancelFrames (recast = move away). Only the first cast deals burst damage.
            if (currentState == StatePrepareToTeleport) {
                if (_prevInnerState == StateCancelFrames) { _isRecast = true; } else if (_prevInnerState == StateWindUp) { _isRecast = false; }
            }

            bool justTeleported = currentState == StateFollowThrough && _prevInnerState == StatePrepareToTeleport;
            _prevInnerState = currentState;

            if (justTeleported && !_isRecast) { FireArrivalBurst(); }
        }

        private void FireArrivalBurst() {
            var casterStats = _inst.Stats;
            if (casterStats == null || !ThePitState.IsDraftMode) { return; }

            var dmg = _inst.Damage;
            if (dmg == null) { return; }

            var userAction = _connectedUserActionProp?.GetValue(_inst) ?? (object)0;
            var arrivalPos = _inst.transform.position;

            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                if (champ.Stats.ActorID == casterStats.ActorID) { continue; }
                if (FeralCore.IsRespawnInvincible(champ.Stats.ActorID)) { continue; }

                var diff = champ.transform.position - arrivalPos;
                diff.y = 0f;
                if (diff.magnitude > BurstRadius) { continue; }

                var direction = diff.magnitude > 0.01f ? diff.normalized : Vector3.forward;

                var burst = dmg.Value;
                burst.damageValue *= 2f;
                _takeBasicDamageMethod.Invoke(champ.Stats, new object[] { burst, casterStats, direction, userAction });
                champ.AddPushForce(direction * PushStrength);
            }
        }
    }
}
