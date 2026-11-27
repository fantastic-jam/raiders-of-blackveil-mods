using System;
using UnityEngine;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Persistent MonoBehaviour used as a coroutine host for mod networking logic.
    /// Created once in WmfMod.Load() with DontDestroyOnLoad — survives scene transitions.
    /// Use WmfMod.Runner.StartCoroutine(...) instead of patching __instance.StartCoroutine(...)
    /// so we don't depend on game object lifetimes.
    /// </summary>
    /// <remarks>
    /// IL2CPP: injected into the IL2CPP domain via ClassInjector (see WmfMod.Load).
    /// Start coroutines with <c>Runner.StartCoroutine(routine.WrapToIl2Cpp())</c>.
    /// </remarks>
    internal sealed class CoroutineRunner : MonoBehaviour {
        public CoroutineRunner(IntPtr ptr) : base(ptr) { }
    }
}
