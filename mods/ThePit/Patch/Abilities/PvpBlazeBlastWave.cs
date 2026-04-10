using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal class PvpBlazeBlastWave {
        internal static readonly FieldInfo CollectAngleField = AccessTools.Field(typeof(BlazeBlastWave), "_collectAngle");
        internal static readonly FieldInfo CollectDistanceField = AccessTools.Field(typeof(BlazeBlastWave), "_collectDistance");
        internal static readonly FieldInfo CollectFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_collectFrames");
        internal static readonly FieldInfo PushWidthField = AccessTools.Field(typeof(BlazeBlastWave), "_pushWidth");
        internal static readonly FieldInfo PushDistanceField = AccessTools.Field(typeof(BlazeBlastWave), "_pushDistance");
        internal static readonly FieldInfo PushDelayFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_pushDelayFrames");
        internal static readonly FieldInfo PushFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_pushFrames");
        internal static readonly FieldInfo DamageAfterPushField = AccessTools.Field(typeof(BlazeBlastWave), "DamageAfterPush");
        internal static readonly MethodInfo ActionStarterTickGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_actionStarterTick");
        internal static readonly MethodInfo PushDirectionGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_pushDirection");
        internal static readonly MethodInfo PushPositionGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_pushPosition");

        private readonly BlazeBlastWave _inst;
        // ActorID → scheduled damage tick
        private readonly Dictionary<int, int> _champTicks = new();

        internal PvpBlazeBlastWave(BlazeBlastWave inst) { _inst = inst; }

        internal void OnCollect() {
            if (!ThePitState.IsAttackPossible) { return; }
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.MainState != ChampionAbility.MainStateValues.InAction) { return; }
            if (ActionStarterTickGetter == null || CollectFramesField == null || CollectDistanceField == null) { return; }

            int actionTick = (int)ActionStarterTickGetter.Invoke(_inst, null);
            int collectFrames = (int)CollectFramesField.GetValue(_inst);
            int collectEnd = actionTick + (int)(collectFrames * ChampionAbility.FrameTicks);
            int tick = _inst.Runner.Tick;
            if (tick < actionTick || tick > collectEnd) { return; }

            float collectDist = (float)CollectDistanceField.GetValue(_inst);
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
            if (!ThePitState.IsAttackPossible) { return; }
            if (_inst.Runner?.IsServer != true) { return; }
            if (ActionStarterTickGetter == null || PushDelayFramesField == null ||
                PushFramesField == null || PushWidthField == null || PushDistanceField == null) { return; }

            int actionTick = (int)ActionStarterTickGetter.Invoke(_inst, null);
            int pushDelay = (int)((int)PushDelayFramesField.GetValue(_inst) * ChampionAbility.FrameTicks);
            int pushFrames = (int)((int)PushFramesField.GetValue(_inst) * ChampionAbility.FrameTicks);
            int pushStart = actionTick + pushDelay;
            int pushEnd = pushStart + pushFrames;
            int tick = _inst.Runner.Tick;
            if (tick < pushStart || tick > pushEnd) { return; }

            float pushWidth = (float)PushWidthField.GetValue(_inst);
            float pushDist = (float)PushDistanceField.GetValue(_inst);

            Vector3 pushDir = PushDirectionGetter != null
                ? (Vector3)PushDirectionGetter.Invoke(_inst, null) : _inst.transform.forward;
            Vector3 pushPos = PushPositionGetter != null
                ? (Vector3)PushPositionGetter.Invoke(_inst, null) : _inst.transform.position;

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
            if (!ThePitState.IsAttackPossible) { return; }
            if (_inst.Runner?.IsServer != true) { return; }
            if (DamageAfterPushField == null || _champTicks.Count == 0) { return; }

            var dmg = (DamageDescriptor)DamageAfterPushField.GetValue(_inst);
            Vector3 pushDir = PushDirectionGetter != null
                ? (Vector3)PushDirectionGetter.Invoke(_inst, null) : _inst.transform.forward;

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
