using System;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RR.Backend;
using UnityEngine;

namespace OfflineMode {
    public static class OfflineSaveManager {
        private static string BackupDirectory => Path.Combine(Paths.ConfigPath, "OfflineMode");
        private static string GameSaveDirectory => Path.Combine(Application.persistentDataPath, "playergamestate");
        private static string WomboPlayerFile => Path.Combine(BackupDirectory, "womboplayerdata.json");

        // ── WomboPlayer (saved on each online session, used to restore identity offline) ──

        public static void SaveWomboPlayer(WomboPlayer player) {
            if (player == null) {
                return;
            }

            Directory.CreateDirectory(BackupDirectory);
            try {
                File.WriteAllText(WomboPlayerFile, JsonConvert.SerializeObject(player, Formatting.Indented));
            }
            catch (Exception ex) {
                OfflineModeMod.PublicLogger.LogError($"OfflineMode: Failed to save WomboPlayer data: {ex.Message}");
            }
        }

        public static WomboPlayer LoadWomboPlayer() {
            if (!File.Exists(WomboPlayerFile)) {
                return null;
            }

            try {
                return JsonConvert.DeserializeObject<WomboPlayer>(File.ReadAllText(WomboPlayerFile));
            }
            catch (Exception ex) {
                OfflineModeMod.PublicLogger.LogError($"OfflineMode: Failed to load WomboPlayer data: {ex.Message}");
                return null;
            }
        }

        // ── Save availability ─────────────────────────────────────────────────

        public static bool HasAnySave() => GetSavedPlayerUUID() != Guid.Empty;

        public static Guid GetSavedPlayerUUID() {
            var player = LoadWomboPlayer();
            return player?.WomboPlayerId ?? Guid.Empty;
        }

        // ── Game's own local save (read-only for us) ──────────────────────────

        public static PlayerGameState LoadGameLocalSave(Guid uuid) {
            var path = Path.Combine(GameSaveDirectory, $"{uuid}.json");
            if (!File.Exists(path)) { return null; }
            try {
                // The game wraps PlayerGameState in a { PlayerGameStateVersion, DataHash, PlayerGameState } envelope.
                var wrapper = JObject.Parse(File.ReadAllText(path));
                var stateToken = wrapper["PlayerGameState"];
                if (stateToken == null) { return null; }
                return stateToken.ToObject<PlayerGameState>();
            }
            catch (Exception ex) {
                OfflineModeMod.PublicLogger.LogError($"OfflineMode: Failed to load game local save: {ex.Message}");
                return null;
            }
        }

        // ── Dated backups (kept when offline play is detected on login) ───────

        public static void SaveBackup(PlayerGameState state) {
            Directory.CreateDirectory(BackupDirectory);
            var backups = Directory.GetFiles(BackupDirectory, "playerstate.*.json");
            Array.Sort(backups); // oldest first (lexicographic = chronological)
            for (int i = 0; i <= backups.Length - 20; i++) {
                File.Delete(backups[i]);
            }
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(BackupDirectory, $"playerstate.{stamp}.json");
            try {
                File.WriteAllText(path, JsonConvert.SerializeObject(state, Formatting.Indented));
                OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: Saved backup {Path.GetFileName(path)}.");
            }
            catch (Exception ex) {
                OfflineModeMod.PublicLogger.LogError($"OfflineMode: Failed to save backup: {ex.Message}");
            }
        }
    }
}
