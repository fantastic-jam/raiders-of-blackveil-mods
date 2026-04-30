using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RaiderRoughPatches.Patch;

namespace RaiderRoughPatches {
    [BepInPlugin(Id, Name, Version)]
    public class RaiderRoughPatchesMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.raiderroughpatches";
        public const string Name = "RaiderRoughPatches";
        public const string Version = "0.0.2";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private Harmony _harmony;
        private bool _enabled;

        public string GetModType() => "Utility";
        public string GetModName() => Name;
        public string GetModDescription() => "Unofficial community patches for Raiders of Blackveil — stash auto-stack, session visibility, door vote on disconnect, barrier self-grant.";
        public bool IsClientRequired => false;
        public bool Disabled => !_enabled;

        public void Enable() {
            if (_enabled) { return; }
            _enabled = true;
            RaiderRoughPatchesPatch.Patch(_harmony);
            PublicLogger.LogInfo($"{Name}: enabled.");
        }

        public void Disable() {
            if (!_enabled) { return; }
            _enabled = false;
            _harmony.UnpatchSelf();
            PublicLogger.LogInfo($"{Name}: disabled.");
        }

        private void Awake() {
            PublicLogger = Logger;
            try {
                _harmony = new Harmony(Id);
                RaiderRoughPatchesPatch.Init(Config);
                RaiderRoughPatchesPatch.Patch(_harmony);
                _enabled = true;
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
