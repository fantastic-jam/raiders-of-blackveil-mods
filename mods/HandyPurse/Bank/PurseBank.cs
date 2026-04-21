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
        [DataMember] internal int? SlotIndex;
    }

    [DataContract]
    internal sealed class TopupCompartment {
        [DataMember] internal string Key = "";
        [DataMember] internal string Hash = "";
        [DataMember] internal List<TopupEntry> Entries = new List<TopupEntry>();
    }

    [DataContract]
    internal sealed class TopupData {
        [DataMember] internal List<TopupCompartment> Compartments = new List<TopupCompartment>();
    }

    [DataContract]
    internal sealed class BankData {
        [DataMember] internal List<BankEntry> Entries = new List<BankEntry>();
    }

    internal static class PurseBank {
        private static string DataDir =>
            Path.Combine(BepInEx.Paths.BepInExRootPath, "data", "HandyPurse");

        private static string TopupPath => Path.Combine(DataDir, "topup.json");
        private static string BankPath => Path.Combine(DataDir, "bank.json");

        // ── Topup file ────────────────────────────────────────────────────

        internal static TopupData LoadTopup() {
            try {
                var path = TopupPath;
                if (!File.Exists(path)) {
                    return new TopupData();
                }
                using var stream = File.OpenRead(path);
                return (TopupData)MakeSerializer<TopupData>().ReadObject(stream) ?? new TopupData();
            }
            catch (Exception ex) {
                HandyPurseMod.PublicLogger.LogWarning($"HandyPurse: topup load failed — {ex.Message}");
                return new TopupData();
            }
        }

        internal static void SaveTopup(TopupData data) {
            try {
                if (data.Compartments.Count == 0) {
                    var path = TopupPath;
                    if (File.Exists(path)) { File.Delete(path); }
                    return;
                }
                Directory.CreateDirectory(DataDir);
                using var stream = File.Create(TopupPath);
                MakeSerializer<TopupData>().WriteObject(stream, data);
            }
            catch (Exception ex) {
                HandyPurseMod.PublicLogger.LogError($"HandyPurse: topup save failed — {ex.Message}");
            }
        }

        internal static TopupCompartment GetOrCreateCompartment(TopupData data, string key) {
            foreach (var c in data.Compartments) {
                if (c.Key == key) {
                    return c;
                }
            }
            var newCompartment = new TopupCompartment { Key = key };
            data.Compartments.Add(newCompartment);
            return newCompartment;
        }

        internal static TopupCompartment FindCompartment(TopupData data, string key) {
            foreach (var c in data.Compartments) {
                if (c.Key == key) {
                    return c;
                }
            }
            return null;
        }

        internal static void RemoveCompartment(TopupData data, string key) {
            for (int i = data.Compartments.Count - 1; i >= 0; i--) {
                if (data.Compartments[i].Key == key) {
                    data.Compartments.RemoveAt(i);
                    return;
                }
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
                HandyPurseMod.PublicLogger.LogWarning($"HandyPurse: bank load failed — {ex.Message}");
                return new BankData();
            }
        }

        internal static BankEntry FindBankEntry(BankData bank, string currencyKey) {
            foreach (var e in bank.Entries) {
                if (e.CurrencyKey == currencyKey) {
                    return e;
                }
            }
            return null;
        }

        internal static bool TryDeposit(List<BankEntry> deposit) {
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
                            Amount = incoming.Amount
                        });
                    }
                }
                SaveBank(bank);
                return true;
            }
            catch (Exception ex) {
                HandyPurseMod.PublicLogger.LogError($"HandyPurse: bank deposit failed — {ex.Message}");
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
                HandyPurseMod.PublicLogger.LogError($"HandyPurse: bank clear failed — {ex.Message}");
                return false;
            }
        }

        private static void SaveBank(BankData bank) {
            Directory.CreateDirectory(DataDir);
            using var stream = File.Create(BankPath);
            MakeSerializer<BankData>().WriteObject(stream, bank);
        }

        private static DataContractJsonSerializer MakeSerializer<T>() =>
            new DataContractJsonSerializer(typeof(T));
    }
}
