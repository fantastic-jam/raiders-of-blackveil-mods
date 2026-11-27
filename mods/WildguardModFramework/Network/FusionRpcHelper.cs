using System;
using Fusion;
using RR;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Sends GameManager.RPC_ErrorMessageAll to a single target player only,
    /// without executing the RPC locally on the server.
    ///
    /// Fusion 2.x builds RPCs through <c>NetworkRunner.CreateRpcBuilder(key, payloadSize, behaviour, targetPlayer)</c>.
    /// The weaved RPC method always passes <c>PlayerRef.Invalid</c> (broadcast); this helper replays the same
    /// sequence with a real target player.
    ///
    /// IL2CPP: CreateRpcBuilder and the RpcBuilder/RpcDataWriter structs are public on the interop wrappers
    /// (exposed as boxed value types), so they are called directly — no IL emission. The [Rpc] attribute is
    /// not kept in the interop assemblies, so the RPC key is a constant: read it from the Cpp2IL dummy assembly
    /// (game-lib/il2cpp/dummy/Assembly-CSharp.dll → GameManager.RPC_ErrorMessageAll [Rpc(Key = …)]) after a game update.
    /// </summary>
    internal static class FusionRpcHelper {
        private const uint ErrorMessageAllRpcKey = 1480208655u;

        /// <summary>
        /// Sends RPC_ErrorMessageAll to a specific player only.
        /// The host never enters the execute path, and other clients never receive the message.
        /// If anything fails, no popup is sent (the caller still disconnects the player) —
        /// never fall back to a broadcast: RPC_ErrorMessageAll quits the application of everyone who receives it.
        /// </summary>
        internal static void SendErrorMessageTo(NetworkRunner runner, GameManager gm, PlayerRef target, string message) {
            if (!runner.HasAnyActiveConnections()) { return; }

            try {
                // Mirrors the weaver output for RPC_ErrorMessageAll:
                //   using (var b = runner.CreateRpcBuilder(key, RpcDataWriter.GetPayloadSize(msg), behaviour, target)) {
                //       var info = b.Prepare(out var writer);
                //       if (info.SendMessageResult == RpcSendMessageResult.Sent) { writer.Write(msg); b.Send(); }
                //   }
                var builder = runner.CreateRpcBuilder(ErrorMessageAllRpcKey, RpcDataWriter.GetPayloadSize(message), gm, target);
                RpcSendMessageResult result;
                try {
                    var info = builder.Prepare(out var writer);
                    result = info.SendMessageResult;
                    if (result == RpcSendMessageResult.Sent) {
                        writer.Write(message);
                        builder.Send();
                    }
                }
                finally {
                    builder.Dispose();
                }
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
