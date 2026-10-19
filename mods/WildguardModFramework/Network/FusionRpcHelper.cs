using System;
using System.Reflection;
using System.Reflection.Emit;
using Fusion;
using HarmonyLib;
using RR;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Sends GameManager.RPC_ErrorMessageAll to a single target player only,
    /// without executing the RPC locally on the server.
    ///
    /// Fusion 2.x builds RPCs through <c>NetworkRunner.CreateRpcBuilder(key, payloadSize, behaviour, targetPlayer)</c>,
    /// which returns an <c>internal ref struct RpcBuilder</c>. Ref structs cannot be boxed, so plain reflection
    /// <c>Invoke</c> is impossible — the weaved call sequence is re-emitted as a <see cref="DynamicMethod"/>
    /// (skipVisibility) with a real target player instead of <c>PlayerRef.Invalid</c>.
    /// </summary>
    internal static class FusionRpcHelper {
        private delegate int SendStringRpc(NetworkRunner runner, NetworkBehaviour behaviour, PlayerRef target, uint key, string payload);

        private static bool _resolved;
        private static SendStringRpc _send;
        private static uint _errorMessageRpcKey;

        private static bool TryResolve() {
            if (_resolved) { return _send != null; }
            _resolved = true;

            var rpcMethod = AccessTools.Method(typeof(GameManager), nameof(GameManager.RPC_ErrorMessageAll));
            var rpcAttr = rpcMethod?.GetCustomAttribute<RpcAttribute>();
            if (rpcAttr == null || rpcAttr.Key == 0) {
                WmfMod.PublicLogger.LogWarning("FusionRpcHelper: GameManager.RPC_ErrorMessageAll [Rpc] key not found — targeted error popup unavailable.");
                return false;
            }
            _errorMessageRpcKey = rpcAttr.Key;

            var runtime = typeof(NetworkRunner).Assembly;
            var builderType = runtime.GetType("Fusion.RpcBuilder");
            var writerType = runtime.GetType("Fusion.RpcDataWriter");
            var createBuilder = AccessTools.Method(typeof(NetworkRunner), "CreateRpcBuilder",
                new[] { typeof(uint), typeof(int), typeof(NetworkBehaviour), typeof(PlayerRef) });
            var prepare = builderType == null || writerType == null
                ? null
                : AccessTools.Method(builderType, "Prepare", new[] { writerType.MakeByRefType() });
            var send = builderType == null ? null : AccessTools.Method(builderType, "Send");
            var dispose = builderType == null ? null : AccessTools.Method(builderType, "Dispose");
            var payloadSize = writerType == null ? null : AccessTools.Method(writerType, "GetPayloadSize", new[] { typeof(string) });
            var write = writerType == null ? null : AccessTools.Method(writerType, "Write", new[] { typeof(string) });
            var sendResultField = AccessTools.Field(typeof(RpcInvokeInfo), nameof(RpcInvokeInfo.SendMessageResult));

            if (createBuilder == null || prepare == null || send == null || dispose == null
                || payloadSize == null || write == null || sendResultField == null) {
                WmfMod.PublicLogger.LogWarning("FusionRpcHelper: Fusion RpcBuilder API not found (Fusion version changed?) — targeted error popup unavailable.");
                return false;
            }

            try {
                _send = BuildSender(builderType, writerType, createBuilder, prepare, send, dispose, payloadSize, write, sendResultField);
            }
            catch (Exception ex) {
                WmfMod.PublicLogger.LogWarning($"FusionRpcHelper: failed to emit RPC sender — targeted error popup unavailable. {ex.Message}");
                return false;
            }
            return true;
        }

        // Mirrors the weaver output for RPC_ErrorMessageAll:
        //   using (var b = runner.CreateRpcBuilder(key, RpcDataWriter.GetPayloadSize(msg), behaviour, target)) {
        //       var info = b.Prepare(out var writer);
        //       if (info.SendMessageResult == RpcSendMessageResult.Sent) { writer.Write(msg); b.Send(); }
        //       return (int)info.SendMessageResult;
        //   }
        private static SendStringRpc BuildSender(Type builderType, Type writerType, MethodInfo createBuilder,
            MethodInfo prepare, MethodInfo send, MethodInfo dispose, MethodInfo payloadSize, MethodInfo write,
            FieldInfo sendResultField) {
            var dm = new DynamicMethod("WMF_SendTargetedStringRpc", typeof(int),
                new[] { typeof(NetworkRunner), typeof(NetworkBehaviour), typeof(PlayerRef), typeof(uint), typeof(string) },
                typeof(FusionRpcHelper).Module, skipVisibility: true);
            var il = dm.GetILGenerator();
            var builder = il.DeclareLocal(builderType);
            var writer = il.DeclareLocal(writerType);
            var info = il.DeclareLocal(typeof(RpcInvokeInfo));
            var skipSend = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Ldarg_S, (byte)4);
            il.Emit(OpCodes.Call, payloadSize);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call, createBuilder);
            il.Emit(OpCodes.Stloc, builder);

            il.BeginExceptionBlock();
            il.Emit(OpCodes.Ldloca, builder);
            il.Emit(OpCodes.Ldloca, writer);
            il.Emit(OpCodes.Call, prepare);
            il.Emit(OpCodes.Stloc, info);
            il.Emit(OpCodes.Ldloc, info);
            il.Emit(OpCodes.Ldfld, sendResultField);
            il.Emit(OpCodes.Brtrue, skipSend); // RpcSendMessageResult.Sent == 0
            il.Emit(OpCodes.Ldloca, writer);
            il.Emit(OpCodes.Ldarg_S, (byte)4);
            il.Emit(OpCodes.Call, write);
            il.Emit(OpCodes.Ldloca, builder);
            il.Emit(OpCodes.Call, send);
            il.Emit(OpCodes.Pop);
            il.MarkLabel(skipSend);
            il.BeginFinallyBlock();
            il.Emit(OpCodes.Ldloca, builder);
            il.Emit(OpCodes.Call, dispose);
            il.EndExceptionBlock();

            il.Emit(OpCodes.Ldloc, info);
            il.Emit(OpCodes.Ldfld, sendResultField);
            il.Emit(OpCodes.Ret);

            return (SendStringRpc)dm.CreateDelegate(typeof(SendStringRpc));
        }

        /// <summary>
        /// Sends RPC_ErrorMessageAll to a specific player only.
        /// The host never enters the execute path, and other clients never receive the message.
        /// If the Fusion internals cannot be resolved, no popup is sent (the caller still disconnects the player) —
        /// never fall back to a broadcast: RPC_ErrorMessageAll quits the application of everyone who receives it.
        /// </summary>
        internal static void SendErrorMessageTo(NetworkRunner runner, GameManager gm, PlayerRef target, string message) {
            if (!TryResolve()) { return; }
            if (!runner.HasAnyActiveConnections()) { return; }

            try {
                var result = (RpcSendMessageResult)_send(runner, gm, target, _errorMessageRpcKey, message);
                if (result != RpcSendMessageResult.Sent) {
                    WmfMod.PublicLogger.LogWarning($"FusionRpcHelper: error popup not sent to {target.PlayerId} ({result}).");
                }
            }
            catch (Exception ex) {
                WmfMod.PublicLogger.LogWarning($"FusionRpcHelper: error popup send failed for {target.PlayerId}: {ex.Message}");
            }
        }
    }
}
