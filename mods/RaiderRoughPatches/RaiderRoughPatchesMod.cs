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
        public const string Version = "0.0.1";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        public string GetModType() => "Utility";
        public string GetModName() => Name;
        public string GetModDescription() => "Unofficial community patches for Raiders of Blackveil.";

        private void Awake() {
            PublicLogger = Logger;
            try {
                RaiderRoughPatchesPatch.Apply(new Harmony(Id), Config);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
