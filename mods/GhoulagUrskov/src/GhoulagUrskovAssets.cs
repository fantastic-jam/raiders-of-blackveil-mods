using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GhoulagUrskov {
    internal static class GhoulagUrskovAssets {
        internal static GameObject Prefab { get; private set; }
        internal static bool IsLoaded { get; private set; }

        internal static bool EnsureLoaded() {
            if (IsLoaded) {
                return true;
            }

            return Load();
        }

        internal static bool Load() {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var catalogPath = Path.Combine(dir, "Assets", $"catalog_{GhoulagUrskovMod.Version}.json");

            if (!File.Exists(catalogPath)) {
                GhoulagUrskovMod.PublicLogger.LogError($"[GhoulagUrskov] Catalog not found at: {catalogPath}");
                return false;
            }

            var catalogHandle = Addressables.LoadContentCatalogAsync(catalogPath, true);
            catalogHandle.WaitForCompletion();
            GhoulagUrskovMod.PublicLogger.LogInfo("[GhoulagUrskov] Catalog loaded.");

            var prefabHandle = Addressables.LoadAssetAsync<GameObject>("urskov_prefab");
            prefabHandle.WaitForCompletion();
            Prefab = prefabHandle.Result;

            if (Prefab == null) {
                GhoulagUrskovMod.PublicLogger.LogError("[GhoulagUrskov] Prefab 'urskov_prefab' not found in catalog.");
                return false;
            }

            GhoulagUrskovMod.PublicLogger.LogInfo($"[GhoulagUrskov] Prefab loaded: {Prefab.name}");
            IsLoaded = true;
            return true;
        }
    }
}
