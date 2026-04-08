using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using WildguardModFramework.ModMenu;
using WildguardModFramework.Network;
using ModRegistry;
using UnityEngine;

namespace WildguardModFramework {
    [BepInPlugin(Id, Name, Version)]
    public class WmfMod : BaseUnityPlugin, IModRegistrant {
        internal const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework";
        public const string Name = "WMF";
        public const string Version = "0.3.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        internal static WmfMod Instance { get; private set; }

        /// <summary>
        /// Persistent coroutine host. Created in Awake() with DontDestroyOnLoad so it survives
        /// scene transitions and is always available for networking coroutines.
        /// </summary>
        internal static CoroutineRunner Runner { get; private set; }

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Utility);
        public string GetModName() => Name;
        public bool Disabled { get; private set; }
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            Disabled = true;
            _harmony?.UnpatchSelf();
        }

        public void Awake() {
            Instance = this;
            PublicLogger = Logger;
            try {
                var runnerGo = new GameObject("WMF.CoroutineRunner");
                DontDestroyOnLoad(runnerGo);
                Runner = runnerGo.AddComponent<CoroutineRunner>();

                _harmony = new Harmony(Id);
                WmfConfig.Init(Config);
                NetworkPatch.Apply(_harmony);
                HostStartPagePatch.Apply(_harmony);
                MenuStartPagePatch.Apply(_harmony);
                MenuPausePagePatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }

    }
}
