using HarmonyLib;

namespace ThePit.FeralEngine.Abilities {
    // Applies all champion and enemy ability PvP patches in one call.
    internal static class AbilityPatch {
        internal static void Apply(Harmony harmony) {
            ShameleonAttackPatch.Apply(harmony);
            ShameleonShadowStrikePatch.Apply(harmony);
            ShameleonShadowDancePatch.Apply(harmony);
            ShameleonTongueLeapPatch.Apply(harmony);
            ShameleonEnterTheShadowPatch.Apply(harmony);
            BlazeAttackPatch.Apply(harmony);
            BlazeBlastWavePatch.Apply(harmony);
            BlazeSpecialAreaPatch.Apply(harmony);
            BlazeDevastationPatch.Apply(harmony);
            SunStrikeAreaPatch.Apply(harmony);
            BeatriceAttackPatch.Apply(harmony);
            BeatriceEntanglingRootsPatch.Apply(harmony);
            BeatriceLotusFlowerPatch.Apply(harmony);
            BeatriceSpecialObjectPatch.Apply(harmony);
            ManEaterPlantBrainPatch.Apply(harmony);
            WitheredSeedBrainPatch.Apply(harmony);
            ChampionMinionPatch.Apply(harmony);
            AreaCharacterSelectorPatch.Apply(harmony);
            RhinoAttackPatch.Apply(harmony);
            RhinoEarthquakePatch.Apply(harmony);
            RhinoShieldsUpPatch.Apply(harmony);
            RhinoStampedePatch.Apply(harmony);
            RhinoSpinPatch.Apply(harmony);
            ProjectileCasterSelfSkipPatch.Apply(harmony);
        }

        internal static void ExpandAllCasters() {
            BlazeAttackPatch.ExpandAllCasters();
            BeatriceAttackPatch.ExpandAllCasters();
            BeatriceEntanglingRootsPatch.ExpandAllCasters();
            BeatriceLotusFlowerPatch.ExpandAllCasters();
            RhinoAttackPatch.SeedAllProxies();
            RhinoEarthquakePatch.SeedAllProxies();
            RhinoShieldsUpPatch.SeedAllProxies();
            RhinoSpinPatch.SeedAllProxies();
            RhinoStampedePatch.SeedAllProxies();
        }

        internal static void ResetAll() {
            ShameleonShadowDancePatch.Reset();
            ShameleonEnterTheShadowPatch.Reset();
            ShameleonTongueLeapPatch.Reset();
            BlazeBlastWavePatch.Reset();
            SunStrikeAreaPatch.Reset();
            BlazeAttackPatch.ResetAllCasters();
            BlazeSpecialAreaPatch.Reset();
            BeatriceAttackPatch.ResetAllCasters();
            BeatriceEntanglingRootsPatch.ResetAllCasters();
            BeatriceLotusFlowerPatch.ResetAllCasters();
            WitheredSeedBrainPatch.ResetAllCasters();
        }
    }
}
