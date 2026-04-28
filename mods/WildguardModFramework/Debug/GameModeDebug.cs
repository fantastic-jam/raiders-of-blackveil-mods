#if WMF_DEBUG
using System;
using RR;
using RR.Backend.API.V1.Ingress;
using RR.Backend.API.V1.Ingress.Message;

namespace WildguardModFramework.Debug {
    internal static class GameModeDebug {
        internal static void PollSessions() {
            var bm = BackendManager.Instance;
            if (bm == null) { WmfMod.PublicLogger.LogInfo("WMF [DebugPoll]: BackendManager null."); return; }

            var meta = bm._gameSessionMeta;
            var mac = BackendHelper.GetBlake2Hash(meta?.MacSecretKey, "");
            var url = IngressMessagePlaySessionListJoinable.EndpointWithLastKnownHashParameter(
                IngressEndpoint.PlaySession_ListJoinableSessions, "", bm.FilterRegionSet, bm.FilterPrivateLobby, mac);
            var myId = meta?.GameSessionId ?? Guid.Empty;
            var http = bm.HttpCommunicator;
            if (http == null) { WmfMod.PublicLogger.LogInfo("WMF [DebugPoll]: HttpCommunicator null."); return; }
            WmfMod.PublicLogger.LogInfo($"WMF [DebugPoll]: polling... myId={myId}");

            http.StartCoroutine(http.GetRequest(url, 5, (_, result, httpCode, json) => {
                WmfMod.PublicLogger.LogInfo($"WMF [DebugPoll]: result={result} http={httpCode}");
                if (json == null) { return; }
                var r = Newtonsoft.Json.JsonConvert.DeserializeObject<IngressResponsePlaySessionListJoinable>(json);
                var sessions = r?.play_sessions;
                WmfMod.PublicLogger.LogInfo($"WMF [DebugPoll]: {sessions?.Count ?? 0} session(s) hash={r?.data_hash}");
                if (sessions != null) {
                    foreach (var s in sessions) {
                        bool mine = s.id == myId;
                        WmfMod.PublicLogger.LogInfo($"WMF [DebugPoll]:  [{s.state}] id={s.id} tag='{s.session_tag}' fusion={s.fusion?.region} mine={mine}");
                    }
                }
            }));
        }
    }
}
#endif
