using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using HandyPurse.Bank;
using Xunit;

namespace HandyPurse.Tests {
    public class PurseBankTests : System.IDisposable {
        private readonly string _tempDir;

        private const long Ts1 = 639133464880300758L;
        private const long Ts2 = 639133464880417294L;

        public PurseBankTests() {
            _tempDir = Path.Combine(Path.GetTempPath(), "HandyPurse_Tests_" + System.Guid.NewGuid().ToString("N"));
            PurseBank.OverrideDataDir(_tempDir);
        }

        public void Dispose() {
            if (Directory.Exists(_tempDir)) {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        // ── Migration ─────────────────────────────────────────────────────

        [Fact]
        public void MigrateAllTopupsToBank_LatestTopupDepositedToBank() {
            WriteTopupFile(Ts1, new List<(string, int, int)> {
                ("Scrap", 71, 6999),
            });
            WriteTopupFile(Ts2, new List<(string, int, int)> {
                ("BlackCoin", 58, 400),
            });

            PurseBank.MigrateAllTopupsToBank();

            var bank = PurseBank.LoadBank();
            Assert.Single(bank.Entries);
            Assert.Contains(bank.Entries, e => e.CurrencyKey == "BlackCoin" && e.Amount == 400);

            Assert.Empty(Directory.GetFiles(_tempDir, "topup-*.json"));
        }

        [Fact]
        public void MigrateAllTopupsToBank_MergesIntoExistingBank() {
            PurseBank.TryDeposit(new List<BankEntry> {
                new BankEntry { CurrencyKey = "Scrap", AssetId = 71, Amount = 1000 },
            });
            WriteTopupFile(Ts1, new List<(string, int, int)> {
                ("Scrap", 71, 500),
            });

            PurseBank.MigrateAllTopupsToBank();

            var bank = PurseBank.LoadBank();
            Assert.Single(bank.Entries);
            Assert.Equal(1500, bank.Entries[0].Amount);
        }

        [Fact]
        public void MigrateAllTopupsToBank_NoTopups_BankUnchanged() {
            PurseBank.TryDeposit(new List<BankEntry> {
                new BankEntry { CurrencyKey = "Scrap", AssetId = 71, Amount = 100 },
            });

            PurseBank.MigrateAllTopupsToBank();

            var bank = PurseBank.LoadBank();
            Assert.Single(bank.Entries);
            Assert.Equal(100, bank.Entries[0].Amount);
        }

        [Fact]
        public void MigrateAllTopupsToBank_AllTopupFilesDeleted() {
            WriteTopupFile(Ts1, new List<(string, int, int)> { ("Scrap", 71, 100) });
            WriteTopupFile(Ts2, new List<(string, int, int)> { ("Scrap", 71, 200) });

            PurseBank.MigrateAllTopupsToBank();

            Assert.Empty(Directory.GetFiles(_tempDir, "topup-*.json"));
        }

        // ── Bank file ─────────────────────────────────────────────────────

        [Fact]
        public void TryDeposit_AccumulatesIntoExistingEntry() {
            PurseBank.TryDeposit(new List<BankEntry> {
                new BankEntry { CurrencyKey = "Scrap", AssetId = 71, Amount = 1000 },
            });
            PurseBank.TryDeposit(new List<BankEntry> {
                new BankEntry { CurrencyKey = "Scrap", AssetId = 71, Amount = 2500 },
            });

            var bank = PurseBank.LoadBank();
            Assert.Single(bank.Entries);
            Assert.Equal(3500, bank.Entries[0].Amount);
        }

        [Fact]
        public void TryDeposit_MultipleCurrencies_StoredSeparately() {
            PurseBank.TryDeposit(new List<BankEntry> {
                new BankEntry { CurrencyKey = "Scrap",     AssetId = 71, Amount = 6999 },
                new BankEntry { CurrencyKey = "BlackCoin", AssetId = 58, Amount = 400  },
            });

            var bank = PurseBank.LoadBank();
            Assert.Equal(2, bank.Entries.Count);
            Assert.Contains(bank.Entries, e => e.CurrencyKey == "Scrap" && e.Amount == 6999);
            Assert.Contains(bank.Entries, e => e.CurrencyKey == "BlackCoin" && e.Amount == 400);
        }

        [Fact]
        public void TryClearBank_DeletesFile() {
            PurseBank.TryDeposit(new List<BankEntry> {
                new BankEntry { CurrencyKey = "Scrap", AssetId = 71, Amount = 100 },
            });
            Assert.True(File.Exists(Path.Combine(_tempDir, "bank.json")));

            PurseBank.TryClearBank();
            Assert.False(File.Exists(Path.Combine(_tempDir, "bank.json")));
        }

        [Fact]
        public void LoadBank_NoFile_ReturnsEmpty() {
            var bank = PurseBank.LoadBank();
            Assert.Empty(bank.Entries);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void WriteTopupFile(long timestamp, List<(string currencyKey, int assetId, int excess)> entries) {
            Directory.CreateDirectory(_tempDir);
            var save = new TopupSave {
                Timestamp = timestamp,
                Compartments = new List<TopupCompartment> {
                    new TopupCompartment {
                        Key = "Common",
                        Entries = new List<TopupEntry>(),
                    },
                },
            };
            foreach (var (key, assetId, excess) in entries) {
                save.Compartments[0].Entries.Add(new TopupEntry {
                    CurrencyKey = key,
                    AssetId = assetId,
                    Excess = excess,
                });
            }
            var path = Path.Combine(_tempDir, $"topup-{timestamp}.json");
            var serializer = new DataContractJsonSerializer(typeof(TopupSave));
            using var stream = File.Create(path);
            serializer.WriteObject(stream, save);
        }
    }
}
