using UnityEngine;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Persistent MonoBehaviour used as a coroutine host for mod networking logic.
    /// Created once in WmfMod.Awake() with DontDestroyOnLoad — survives scene transitions.
    /// Use WmfMod.Runner.StartCoroutine(...) instead of patching __instance.StartCoroutine(...)
    /// so we don't depend on game object lifetimes.
    /// </summary>
    internal sealed class CoroutineRunner : MonoBehaviour { }
}
