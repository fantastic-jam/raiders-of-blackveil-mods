using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Perk;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpBlazeBlastWave {
        private static FieldInfo _collectAngleField;
        private static FieldInfo _collectDistanceField;
        private static FieldInfo _collectFramesField;
        private static FieldInfo _pushWidthField;
        private static FieldInfo _pushDistanceField;
        private static FieldInfo _pushDelayFramesField;
        private static FieldInfo _pushFramesField;
        private static FieldInfo _damageAfterPushField;
        private static MethodInfo _actionStarterTickGetter;
        private static MethodInfo _pushDirectionGetter;
        private static MethodInfo _pushPositionGetter;

        private readonly BlazeBlastWave _inst;
        // ActorID → scheduled damage tick
        private readonly Dictionary<int, int> _champTicks = new();

        internal static void Init() {
            _collectAngleField = AccessTools.Field(typeof(BlazeBlastWave), "_collectAngle");
            if (_collectAngleField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._collectAngle not found — collect angle unavailable.");
            }

            _collectDistanceField = AccessTools.Field(typeof(BlazeBlastWave), "_collectDistance");
            if (_collectDistanceField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._collectDistance not found — Blast Wave collect PvP inactive.");
            }

            _collectFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_collectFrames");
            if (_collectFramesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._collectFrames not found — Blast Wave collect PvP inactive.");
            }

            _pushWidthField = AccessTools.Field(typeof(BlazeBlastWave), "_pushWidth");
            if (_pushWidthField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._pushWidth not found — Blast Wave push PvP inactive.");
            }

            _pushDistanceField = AccessTools.Field(typeof(BlazeBlastWave), "_pushDistance");
            if (_pushDistanceField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._pushDistance not found — Blast Wave push PvP inactive.");
            }

            _pushDelayFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_pushDelayFrames");
            if (_pushDelayFramesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._pushDelayFrames not found — Blast Wave push PvP inactive.");
            }

            _pushFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_pushFrames");
            if (_pushFramesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._pushFrames not found — Blast Wave push PvP inactive.");
            }

            _damageAfterPushField = AccessTools.Field(typeof(BlazeBlastWave), "DamageAfterPush");
            if (_damageAfterPushField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave.DamageAfterPush not found — Blast Wave damage PvP inactive.");
            }

            _actionStarterTickGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_actionStarterTick");
            if (_actionStarterTickGetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._actionStarterTick not found — Blast Wave collect/push PvP inactive.");
            }

            _pushDirectionGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_pushDirection");
            if (_pushDirectionGetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._pushDirection not found — push direction will use transform.forward.");
            }

            _pushPositionGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_pushPosition");
            if (_pushPositionGetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave._pushPosition not found — push position will use transform.position.");
            }
        }

        internal PvpBlazeBlastWave(BlazeBlastWave inst) { _inst = inst; }

        internal void OnCollect() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.MainState != ChampionAbility.MainStateValues.InAction) { return; }
            if (_actionStarterTickGetter == null || _collectFramesField == null || _collectDistanceField == null) { return; }

            int actionTick = (int)_actionStarterTickGetter.Invoke(_inst, null);
            int collectFrames = (int)_collectFramesField.GetValue(_inst);
            int collectEnd = actionTick + (int)(collectFrames * ChampionAbility.FrameTicks);
            int tick = _inst.Runner.Tick;
            if (tick < actionTick || tick > collectEnd) { return; }

            float collectDist = (float)_collectDistanceField.GetValue(_inst);
            var forward = _inst.transform.forward;
            var pos = _inst.transform.position;
            var self = _inst.Stats;

            foreach (var target in PvpDetector.OverlapSphere(pos + forward * (collectDist * 0.5f), collectDist, excludes: new[] { self })) {
                var dir = pos - target.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) { dir = forward; }
                target.Character?.AddOneFramePushForce(dir.normalized * (collectDist * 2f / _inst.Runner.DeltaTime) * 0.5f);
            }
        }

        internal void OnPush() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_actionStarterTickGetter == null || _pushDelayFramesField == null ||
                _pushFramesField == null || _pushWidthField == null || _pushDistanceField == null) { return; }

            int actionTick = (int)_actionStarterTickGetter.Invoke(_inst, null);
            int pushDelay = (int)((int)_pushDelayFramesField.GetValue(_inst) * ChampionAbility.FrameTicks);
            int pushFrames = (int)((int)_pushFramesField.GetValue(_inst) * ChampionAbility.FrameTicks);
            int pushStart = actionTick + pushDelay;
            int pushEnd = pushStart + pushFrames;
            int tick = _inst.Runner.Tick;
            if (tick < pushStart || tick > pushEnd) { return; }

            float pushWidth = (float)_pushWidthField.GetValue(_inst);
            float pushDist = (float)_pushDistanceField.GetValue(_inst);

            Vector3 pushDir = _pushDirectionGetter != null
                ? (Vector3)_pushDirectionGetter.Invoke(_inst, null) : _inst.transform.forward;
            Vector3 pushPos = _pushPositionGetter != null
                ? (Vector3)_pushPositionGetter.Invoke(_inst, null) : _inst.transform.position;

            var self = _inst.Stats;
            foreach (var target in PvpDetector.OverlapSphere(pushPos + pushDir * (pushDist * 0.5f), pushDist + pushWidth, excludes: new[] { self })) {
                var offset = target.transform.position - pushPos;
                offset.y = 0f;
                float fwd = Vector3.Dot(pushDir, offset);
                float side = Mathf.Abs(Vector3.Dot(new Vector3(-pushDir.z, 0f, pushDir.x), offset));
                float charR = target.Character != null ? target.Character.CharRadius : 0.5f;
                if (fwd < 0f || fwd > pushDist || side > pushWidth * 0.5f + charR) { continue; }

                var force = pushDir * (pushDist / _inst.Runner.DeltaTime);
                target.Character?.AddOneFramePushForce(force * 0.9f);
                target.Character?.AddPushForce(force * 0.1f);

                if (!_champTicks.ContainsKey(target.ActorID)) {
                    _champTicks[target.ActorID] = _inst.Runner.Tick + 2;
                }
            }
        }

        internal void OnDamage() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_damageAfterPushField == null || _champTicks.Count == 0) { return; }

            var dmg = (DamageDescriptor)_damageAfterPushField.GetValue(_inst);
            Vector3 pushDir = _pushDirectionGetter != null
                ? (Vector3)_pushDirectionGetter.Invoke(_inst, null) : _inst.transform.forward;

            var self = _inst.Stats;
            bool idle = _inst.MainState == ChampionAbility.MainStateValues.Idle;
            var toRemove = new List<int>();
            bool allDone = true;

            foreach (var kv in _champTicks) {
                if (kv.Value == _inst.Runner.Tick) {
                    var players = RR.PlayerManager.Instance?.GetPlayers();
                    if (players != null) {
                        foreach (var p in players) {
                            var champ = p.PlayableChampion;
                            if (champ?.Stats?.ActorID == kv.Key) {
                                champ.Stats.TakeBasicDamage(dmg, self, pushDir, _inst.ConnectedUserAction);
                                break;
                            }
                        }
                    }
                    toRemove.Add(kv.Key);
                } else if (kv.Value > _inst.Runner.Tick) {
                    allDone = false;
                }
            }
            foreach (var id in toRemove) { _champTicks.Remove(id); }
            if (idle || allDone) { _champTicks.Clear(); }
        }

        internal void Reset() { _champTicks.Clear(); }
    }
}
