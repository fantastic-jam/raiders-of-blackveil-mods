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
        [DataMember] internal int Excess;
    }

    [DataContract]
    internal sealed class TopupCompartment {
        [DataMember] internal string Key = "";
        [DataMember] internal List<TopupEntry> Entries = new List<TopupEntry>();
    }

    [DataContract]
    internal sealed class TopupSave {
        [DataMember] internal long Timestamp;
        [DataMember] internal List<TopupCompartment> Compartments = new List<TopupCompartment>();
    }

    [DataContract]
    internal sealed class BankData {
        [DataMember] internal List<BankEntry> Entries = new List<BankEntry>();
    }

    internal static class PurseBank {
        private static string _dataDir;
        internal static string DataDir => _dataDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "HandyPurse");
        internal static void OverrideDataDir(string path) => _dataDir = path;

        internal static Action<string> Warn = _ => { };
        internal static Action<string> Error = _ => { };

        private static string BankPath => Path.Combine(DataDir, "bank.json");

        // ── Migration ─────────────────────────────────────────────────────
        // On startup: deposit the latest topup file's excess into bank.json,
        // then delete all topup files.

        internal static void MigrateAllTopupsToBank() {
            try {
                var legacy = Path.Combine(DataDir, "topup.json");
                if (File.Exists(legacy)) { File.Delete(legacy); }
            }
            catch { }

            if (!Directory.Exists(DataDir)) { return; }

            var files = Directory.GetFiles(DataDir, "topup-*.json");
            if (files.Length == 0) { return; }

            Array.Sort(files);
            TopupSave latest = null;
            for (int i = files.Length - 1; i >= 0; i--) {
                try {
                    using var stream = File.OpenRead(files[i]);
                    var save = (TopupSave)MakeSerializer<TopupSave>().ReadObject(stream);
                    if (save?.Compartments?.Count > 0) { latest = save; break; }
                }
                catch (Exception ex) {
                    Warn($"HandyPurse: skipping corrupt topup file {files[i]} — {ex.Message}");
                }
            }

            if (latest != null) {
                var deposit = new List<BankEntry>();
                foreach (var compartment in latest.Compartments) {
                    foreach (var entry in compartment.Entries) {
                        deposit.Add(new BankEntry {
                            CurrencyKey = entry.CurrencyKey,
                            AssetId = entry.AssetId,
                            Amount = entry.Excess,
                        });
                    }
                }
                if (deposit.Count > 0) {
                    TryDeposit(deposit);
                    Warn($"HandyPurse: migrated {deposit.Count} topup entries to bank — withdraw via the bank menu.");
                }
            }

            foreach (var file in files) {
                try { File.Delete(file); }
                catch (Exception ex) { Warn($"HandyPurse: failed to delete topup file {file} — {ex.Message}"); }
            }
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

        private static DataContractJsonSerializer MakeSerializer<T>() =>
            new DataContractJsonSerializer(typeof(T));
    }
}
