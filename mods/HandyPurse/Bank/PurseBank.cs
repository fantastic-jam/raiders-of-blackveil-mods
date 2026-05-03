using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace HandyPurse.Bank {
    [DataContract]
    internal sealed class BankEntry {
        [DataMember] internal string CurrencyKey = "";
        [DataMember] internal int AssetId;
        [DataMember] internal int Amount;
    }

    [DataContract]
    internal sealed class TopupEntry {
        [DataMember] internal string CurrencyKey = "";
        [DataMember] internal int AssetId;
        [DataMember] internal int VanillaAmount;
        [DataMember] internal int Excess;
        // Nullable to survive round-trips from older file versions where this field was absent.
        [DataMember] internal int? SlotIndex;
    }

    // Compartment key = "Common" or champion type name.
    [DataContract]
    internal sealed class TopupCompartment {
        [DataMember] internal string Key = "";
        [DataMember] internal List<TopupEntry> Entries = new List<TopupEntry>();
    }

    // One file per save event, identified by PlayerGameState.TimeStamp.
    [DataContract]
    internal sealed class TopupSave {
        [DataMember] internal long Timestamp;   // PlayerGameState.TimeStamp — stable through cloud round-trip
        [DataMember] internal List<TopupCompartment> Compartments = new List<TopupCompartment>();
    }

    [DataContract]
    internal sealed class BankData {
        [DataMember] internal List<BankEntry> Entries = new List<BankEntry>();
    }

    internal static class PurseBank {
        // Overridable for tests — defaults to BepInEx data directory (set in HandyPurseMod.Awake).
        private static string _dataDir;
        internal static string DataDir => _dataDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "HandyPurse");
        internal static void OverrideDataDir(string path) => _dataDir = path;

        // Overridable for tests — no-ops by default.
        internal static Action<string> Warn = _ => { };
        internal static Action<string> Error = _ => { };

        private static string BankPath => Path.Combine(DataDir, "bank.json");

        // ── Topup files (one per save, named by timestamp) ────────────────

        /// <summary>
        /// Writes the topup save to disk. Returns false if the write failed.
        /// </summary>
        internal static bool WriteTopupSave(TopupSave save) {
            try {
                Directory.CreateDirectory(DataDir);
                var path = TopupSavePath(save.Timestamp);
                var tmp = path + ".tmp";
                using (var stream = File.Create(tmp)) {
                    MakeSerializer<TopupSave>().WriteObject(stream, save);
                }
                if (File.Exists(path)) { File.Delete(path); }
                File.Move(tmp, path);
                return true;
            }
            catch (Exception ex) {
                Error($"HandyPurse: topup write failed — {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// O(1) direct lookup — TopupSave filename is keyed by PlayerGameState.TimeStamp,
        /// which is set by the game before save and returned unchanged from the cloud on load.
        /// </summary>
        internal static TopupSave FindTopupSave(long timestamp) {
            try {
                var path = TopupSavePath(timestamp);
                if (!File.Exists(path)) { return null; }
                using var stream = File.OpenRead(path);
                return (TopupSave)MakeSerializer<TopupSave>().ReadObject(stream);
            }
            catch (Exception ex) {
                Warn($"HandyPurse: topup lookup failed — {ex.Message}");
                return null;
            }
        }

        internal static void DeleteTopupSave(long timestamp) {
            try {
                var path = TopupSavePath(timestamp);
                if (File.Exists(path)) { File.Delete(path); }
            }
            catch (Exception ex) {
                Warn($"HandyPurse: topup delete failed — {ex.Message}");
            }
        }

        /// <summary>
        /// Returns all topup save files on disk.
        /// </summary>
        internal static List<TopupSave> GetAllTopupSaves() {
            var result = new List<TopupSave>();
            try {
                if (!Directory.Exists(DataDir)) { return result; }
                var files = Directory.GetFiles(DataDir, "topup-*.json");
                foreach (var file in files) {
                    try {
                        using var stream = File.OpenRead(file);
                        var save = (TopupSave)MakeSerializer<TopupSave>().ReadObject(stream);
                        if (save != null) { result.Add(save); }
                    }
                    catch (Exception ex) {
                        Warn($"HandyPurse: skipping corrupt topup file {file} — {ex.Message}");
                    }
                }
            }
            catch (Exception ex) {
                Warn($"HandyPurse: topup scan failed — {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Deletes the legacy single topup.json if it exists (upgrade from pre-0.8 format).
        /// </summary>
        internal static void DeleteLegacyTopup() {
            try {
                var path = Path.Combine(DataDir, "topup.json");
                if (File.Exists(path)) { File.Delete(path); }
            }
            catch { }
        }

        // ── Bank file ─────────────────────────────────────────────────────

        internal static BankData LoadBank() {
            try {
                var path = BankPath;
                if (!File.Exists(path)) {
                    return new BankData();
                }
                using var stream = File.OpenRead(path);
                return (BankData)MakeSerializer<BankData>().ReadObject(stream) ?? new BankData();
            }
            catch (Exception ex) {
                Warn($"HandyPurse: bank load failed — {ex.Message}");
                return new BankData();
            }
        }

        internal static bool TryDeposit(List<BankEntry> deposit) {
            if (deposit == null || deposit.Count == 0) { return true; }
            try {
                var bank = LoadBank();
                foreach (var incoming in deposit) {
                    var existing = FindBankEntry(bank, incoming.CurrencyKey);
                    if (existing != null) {
                        existing.Amount += incoming.Amount;
                    } else {
                        bank.Entries.Add(new BankEntry {
                            CurrencyKey = incoming.CurrencyKey,
                            AssetId = incoming.AssetId,
                            Amount = incoming.Amount,
                        });
                    }
                }
                SaveBank(bank);
                return true;
            }
            catch (Exception ex) {
                Error($"HandyPurse: bank deposit failed — {ex.Message}");
                return false;
            }
        }

        internal static bool TryClearBank() {
            try {
                var path = BankPath;
                if (File.Exists(path)) {
                    File.Delete(path);
                }
                return true;
            }
            catch (Exception ex) {
                Error($"HandyPurse: bank clear failed — {ex.Message}");
                return false;
            }
        }

        private static BankEntry FindBankEntry(BankData bank, string currencyKey) {
            foreach (var e in bank.Entries) {
                if (e.CurrencyKey == currencyKey) {
                    return e;
                }
            }
            return null;
        }

        private static void SaveBank(BankData bank) {
            Directory.CreateDirectory(DataDir);
            var tmp = BankPath + ".tmp";
            using (var stream = File.Create(tmp)) {
                MakeSerializer<BankData>().WriteObject(stream, bank);
            }
            if (File.Exists(BankPath)) { File.Delete(BankPath); }
            File.Move(tmp, BankPath);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string TopupSavePath(long timestamp) =>
            Path.Combine(DataDir, $"topup-{timestamp}.json");

        private static DataContractJsonSerializer MakeSerializer<T>() =>
            new DataContractJsonSerializer(typeof(T));
    }
}
