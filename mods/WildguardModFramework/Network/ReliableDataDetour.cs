using System;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Hook;
using Fusion;
using Fusion.Sockets;
using HarmonyLib;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using RR;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Native detour on <c>NetworkManager.OnReliableDataReceived(runner, player, ReliableKey, ReadOnlySpan&lt;byte&gt;)</c>,
    /// the game's Fusion reliable-data callback that WMF multiplexes its own streams on.
    ///
    /// A Harmony patch can't be used here: Il2CppInterop runs the original through <c>il2cpp_runtime_invoke</c>, and
    /// IL2CPP's invoker for methods with byref-like (<c>Span</c>) parameters throws <c>NotImplementedException</c> —
    /// every game reliable message was dropped and runs failed to start. The detour calls the native original
    /// directly with the untouched arguments.
    ///
    /// Win x64 ABI: <c>ReliableKey</c> (16 B) and <c>ReadOnlySpan&lt;byte&gt;</c> (16 B: data pointer, int length) are
    /// passed by pointer to a caller-owned copy; <c>PlayerRef</c> (4 B) by value.
    /// </summary>
    internal static unsafe class ReliableDataDetour {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OnReliableDataReceivedFn(IntPtr self, IntPtr runner, PlayerRef player, IntPtr key, IntPtr data, IntPtr methodInfo);

        // Held statically: native code keeps raw pointers to these delegates, the GC must never collect them.
        private static OnReliableDataReceivedFn _detour;
        private static OnReliableDataReceivedFn _original;
        private static INativeDetour _hook;

        internal static void Apply() {
            var method = AccessTools.Method(typeof(NetworkManager), "OnReliableDataReceived",
                new[] { typeof(NetworkRunner), typeof(PlayerRef), typeof(ReliableKey), typeof(Il2CppSystem.ReadOnlySpan<byte>) });
            if (method == null) {
                WmfMod.PublicLogger.LogWarning("WMF: NetworkManager.OnReliableDataReceived not found — mod data channel inactive.");
                return;
            }
            var methodInfoField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
            if (methodInfoField == null) {
                WmfMod.PublicLogger.LogWarning("WMF: OnReliableDataReceived native MethodInfo not found — mod data channel inactive.");
                return;
            }
            var methodInfo = (IntPtr)methodInfoField.GetValue(null);
            var nativePtr = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfo).MethodPointer;
            if (nativePtr == IntPtr.Zero) {
                WmfMod.PublicLogger.LogWarning("WMF: OnReliableDataReceived native pointer is null — mod data channel inactive.");
                return;
            }

            _detour = Detour;
            _hook = INativeDetour.CreateAndApply(nativePtr, _detour, out _original);
        }

        private static void Detour(IntPtr self, IntPtr runner, PlayerRef player, IntPtr key, IntPtr data, IntPtr methodInfo) {
            var callOriginal = true;
            try {
                callOriginal = Dispatch(runner, player, key, data);
            }
            catch (Exception e) {
                // An exception escaping into native code kills the game — log and let the game handle the message.
                WmfMod.PublicLogger.LogError($"WMF: reliable data handler failed: {e}");
            }
            if (callOriginal) { _original(self, runner, player, key, data, methodInfo); }
        }

        private static bool Dispatch(IntPtr runner, PlayerRef player, IntPtr key, IntPtr data) {
            if (runner == IntPtr.Zero || key == IntPtr.Zero || data == IntPtr.Zero) { return true; }
            // ReliableKey.GetInts(out key0, …): key0 = first int of Data = the game's DataStreamType.
            var streamType = (DataStreamType)(*(int*)key);
            var bytes = new ReadOnlySpan<byte>(*(void**)data, *(int*)((byte*)data + IntPtr.Size));
            return GameModeProtocol.OnReliableDataReceived(new NetworkRunner(runner), player, streamType, bytes);
        }
    }
}
