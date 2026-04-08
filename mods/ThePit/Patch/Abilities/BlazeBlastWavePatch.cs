using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // PvP extension for BlazeBlastWave.
    // The original CollectAndGrabEnemies / PushAwayEnemies / DamageEnemies iterate
    // NetworkEnemyBase.AllEnemies, which is empty in the arena. We add champion
    // detection on top via postfix without touching the original flow.
    internal static class BlazeBlastWavePatch {
        // Per-instance (GetInstanceID) champion damage schedule: ActorID → runner tick
        private static readonly Dictionary<int, Dictionary<int, int>> _champTicks
            = new Dictionary<int, Dictionary<int, int>>();

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

        internal static void Reset() => _champTicks.Clear();

        internal static void Apply(Harmony harmony) {
            _collectAngleField = AccessTools.Field(typeof(BlazeBlastWave), "_collectAngle");
            _collectDistanceField = AccessTools.Field(typeof(BlazeBlastWave), "_collectDistance");
            _collectFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_collectFrames");
            _pushWidthField = AccessTools.Field(typeof(BlazeBlastWave), "_pushWidth");
            _pushDistanceField = AccessTools.Field(typeof(BlazeBlastWave), "_pushDistance");
            _pushDelayFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_pushDelayFrames");
            _pushFramesField = AccessTools.Field(typeof(BlazeBlastWave), "_pushFrames");
            _damageAfterPushField = AccessTools.Field(typeof(BlazeBlastWave), "DamageAfterPush");
            _actionStarterTickGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_actionStarterTick");
            _pushDirectionGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_pushDirection");
            _pushPositionGetter = AccessTools.PropertyGetter(typeof(BlazeBlastWave), "_pushPosition");

            var collect = AccessTools.Method(typeof(BlazeBlastWave), "CollectAndGrabEnemies");
            var push = AccessTools.Method(typeof(BlazeBlastWave), "PushAwayEnemies");
            var damage = AccessTools.Method(typeof(BlazeBlastWave), "DamageEnemies");

            if (collect == null || push == null || damage == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave methods not found — Blast Wave PvP inactive.");
                return;
            }

            harmony.Patch(collect,
                postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(CollectPostfix)));
            harmony.Patch(push,
                postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(PushPostfix)));
            harmony.Patch(damage,
                postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(DamagePostfix)));
        }

        private static void CollectPostfix(BlazeBlastWave __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (__instance.MainState != ChampionAbility.MainStateValues.InAction) { return; }
            if (_actionStarterTickGetter == null || _collectFramesField == null || _collectDistanceField == null) { return; }

            int actionTick = (int)_actionStarterTickGetter.Invoke(__instance, null);
            int collectFrames = (int)_collectFramesField.GetValue(__instance);
            int collectEnd = actionTick + (int)(collectFrames * ChampionAbility.FrameTicks);
            int tick = __instance.Runner.Tick;
            if (tick < actionTick || tick > collectEnd) { return; }

            float collectDist = (float)_collectDistanceField.GetValue(__instance);
            var forward = __instance.transform.forward;
            var pos = __instance.transform.position;
            var self = __instance.Stats;

            var hits = PvpDetector.OverlapSphere(pos + forward * (collectDist * 0.5f), collectDist,
                excludes: new[] { self });
            foreach (var target in hits) {
                // Push champion toward Blaze's forward direction (collect sweep).
                var dir = pos - target.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) { dir = forward; }
                var force = dir.normalized * (collectDist * 2f / __instance.Runner.DeltaTime);
                target.Character?.AddOneFramePushForce(force * 0.5f);
            }
        }

        private static void PushPostfix(BlazeBlastWave __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (_actionStarterTickGetter == null || _pushDelayFramesField == null ||
                _pushFramesField == null || _pushWidthField == null || _pushDistanceField == null) { return; }

            int actionTick = (int)_actionStarterTickGetter.Invoke(__instance, null);
            int pushDelay = (int)((int)_pushDelayFramesField.GetValue(__instance) * ChampionAbility.FrameTicks);
            int pushFrames = (int)((int)_pushFramesField.GetValue(__instance) * ChampionAbility.FrameTicks);
            int pushStart = actionTick + pushDelay;
            int pushEnd = pushStart + pushFrames;
            int tick = __instance.Runner.Tick;
            if (tick < pushStart || tick > pushEnd) { return; }

            float pushWidth = (float)_pushWidthField.GetValue(__instance);
            float pushDist = (float)_pushDistanceField.GetValue(__instance);

            Vector3 pushDir = _pushDirectionGetter != null
                ? (Vector3)_pushDirectionGetter.Invoke(__instance, null)
                : __instance.transform.forward;
            Vector3 pushPos = _pushPositionGetter != null
                ? (Vector3)_pushPositionGetter.Invoke(__instance, null)
                : __instance.transform.position;

            var self = __instance.Stats;
            var hits = PvpDetector.OverlapSphere(pushPos + pushDir * (pushDist * 0.5f),
                pushDist + pushWidth, excludes: new[] { self });

            int instanceId = __instance.GetInstanceID();
            if (!_champTicks.TryGetValue(instanceId, out var tickMap)) {
                tickMap = new Dictionary<int, int>();
                _champTicks[instanceId] = tickMap;
            }

            foreach (var target in hits) {
                var offset = target.transform.position - pushPos;
                offset.y = 0f;
                // Only targets within the push corridor
                float fwd = Vector3.Dot(pushDir, offset);
                float side = Mathf.Abs(Vector3.Dot(
                    new Vector3(-pushDir.z, 0f, pushDir.x), offset));
                float charR = target.Character != null ? target.Character.CharRadius : 0.5f;
                if (fwd < 0f || fwd > pushDist || side > pushWidth * 0.5f + charR) { continue; }

                // Push away
                var force = pushDir * (pushDist / __instance.Runner.DeltaTime);
                target.Character?.AddOneFramePushForce(force * 0.9f);
                target.Character?.AddPushForce(force * 0.1f);

                // Schedule damage (2 ticks later, same as original NPC logic)
                if (!tickMap.ContainsKey(target.ActorID)) {
                    tickMap[target.ActorID] = __instance.Runner.Tick + 2;
                }
            }
        }

        private static void DamagePostfix(BlazeBlastWave __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (_damageAfterPushField == null) { return; }

            int instanceId = __instance.GetInstanceID();
            if (!_champTicks.TryGetValue(instanceId, out var tickMap) || tickMap.Count == 0) { return; }

            var dmg = (DamageDescriptor)_damageAfterPushField.GetValue(__instance);
            Vector3 pushDir = _pushDirectionGetter != null
                ? (Vector3)_pushDirectionGetter.Invoke(__instance, null)
                : __instance.transform.forward;

            var self = __instance.Stats;
            bool idle = __instance.MainState == ChampionAbility.MainStateValues.Idle;

            var toRemove = new System.Collections.Generic.List<int>();
            bool allDone = true;
            foreach (var kv in tickMap) {
                if (kv.Value == __instance.Runner.Tick) {
                    // Find the target champion by ActorID
                    var players = RR.PlayerManager.Instance?.GetPlayers();
                    if (players == null) { toRemove.Add(kv.Key); continue; }
                    foreach (var p in players) {
                        var champ = p.PlayableChampion;
                        if (champ?.Stats?.ActorID == kv.Key) {
                            champ.Stats.TakeBasicDamage(dmg, self, pushDir, __instance.ConnectedUserAction);
                            break;
                        }
                    }
                    toRemove.Add(kv.Key);
                } else if (kv.Value > __instance.Runner.Tick) {
                    allDone = false;
                }
            }
            foreach (var id in toRemove) { tickMap.Remove(id); }
            if (idle || allDone) { _champTicks.Remove(instanceId); }
        }
    }
}
