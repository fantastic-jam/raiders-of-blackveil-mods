using System.Reflection;
using Fusion;
using HarmonyLib;
using RR;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Sends RPC_ErrorMessageAll (method index 9 on GameManager) to a single target player only,
    /// without executing the RPC locally on the server.
    /// Uses reflection only for the two private Fusion internals: NetworkRunner.Simulation and NetworkBehaviour.ObjectIndex.
    /// </summary>
    internal static unsafe class FusionRpcHelper {
        private static bool _resolved;
        private static PropertyInfo _simulationProp;
        private static FieldInfo _objectIndexField;

        private static bool TryResolve() {
            if (_resolved) { return _simulationProp != null && _objectIndexField != null; }
            _resolved = true;
            _simulationProp = AccessTools.Property(typeof(NetworkRunner), "Simulation");
            _objectIndexField = AccessTools.Field(typeof(NetworkBehaviour), "ObjectIndex");
            if (_simulationProp == null || _objectIndexField == null) {
                WmfMod.PublicLogger.LogWarning("FusionRpcHelper: could not resolve Simulation or ObjectIndex via reflection.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sends RPC_ErrorMessageAll to a specific player only.
        /// The host never enters the execute path, and other clients never receive the message.
        /// </summary>
        internal static void SendErrorMessageTo(NetworkRunner runner, GameManager gm, PlayerRef target, string message) {
            if (!TryResolve()) {
                WmfMod.PublicLogger.LogWarning("FusionRpcHelper: falling back to broadcast.");
                gm.RPC_ErrorMessageAll(message);
                return;
            }

            int size = 8 + ((ReadWriteUtilsForWeaver.GetByteCountUtf8NoHash(message) + 3) & -4);
            if (!SimulationMessage.CanAllocateUserPayload(size)) {
                WmfMod.PublicLogger.LogWarning("FusionRpcHelper: payload too large.");
                return;
            }
            if (!runner.HasAnyActiveConnections()) { return; }

            var sim = (Simulation)_simulationProp.GetValue(runner);
            var objectIndex = (int)_objectIndexField.GetValue(gm);

            SimulationMessage* ptr = SimulationMessage.Allocate(sim, size);
            byte* data = (byte*)ptr + 28; // struct header is 28 bytes (7 x int32/PlayerRef fields before payload)
            *(RpcHeader*)data = RpcHeader.Create(gm.Object.Id, objectIndex, 9);
            int offset = 8; // skip RpcHeader (8 bytes) before writing string payload
            offset = ((ReadWriteUtilsForWeaver.WriteStringUtf8NoHash(data + offset, message) + 3) & -4) + offset;
            ptr->Offset = offset * 8;
            ptr->SetTarget(target); // sets Target field + FLAG_TARGET_PLAYER — only target receives/executes
            runner.SendRpc(ptr);
        }
    }
}
