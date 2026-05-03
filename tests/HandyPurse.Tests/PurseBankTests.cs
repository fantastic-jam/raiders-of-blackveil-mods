using System.Collections.Generic;
using System.IO;
using HandyPurse.Bank;
using Xunit;

namespace HandyPurse.Tests {
    public class PurseBankTests : System.IDisposable {
        private readonly string _tempDir;

        // Fake DataHash hex strings (32 bytes = 64 hex chars, but any length works for tests)
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

        // ── Topup save files ──────────────────────────────────────────────

        [Fact]
        public void WriteAndFindTopupSave_RoundTrip() {
            var save = new TopupSave {
                Timestamp = Ts1,
                Compartments = new List<TopupCompartment> {
                    new TopupCompartment {
                        Key = "Common",
                        Entries = new List<TopupEntry> {
                            new TopupEntry { CurrencyKey = "Scrap", AssetId = 71, VanillaAmount = 3000, Excess = 6999, SlotIndex = 0 },
                        },
                    },
                },
            };

            PurseBank.WriteTopupSave(save);
            var loaded = PurseBank.FindTopupSave(Ts1);

            Assert.NotNull(loaded);
            Assert.Equal(Ts1, loaded.Timestamp);
            Assert.Single(loaded.Compartments);
            Assert.Equal("Common", loaded.Compartments[0].Key);
            Assert.Single(loaded.Compartments[0].Entries);
            Assert.Equal(6999, loaded.Compartments[0].Entries[0].Excess);
        }

        [Fact]
        public void FindTopupSave_NoMatchingFile_ReturnsNull() {
            var result = PurseBank.FindTopupSave(0L);
            Assert.Null(result);
        }

        [Fact]
        public void DeleteTopupSave_RemovesFile() {
            PurseBank.WriteTopupSave(new TopupSave { Timestamp = Ts1 });
            Assert.NotNull(PurseBank.FindTopupSave(Ts1));

            PurseBank.DeleteTopupSave(Ts1);
            Assert.Null(PurseBank.FindTopupSave(Ts1));
        }

        [Fact]
        public void GetAllTopupSaves_ReturnsAllFiles() {
            PurseBank.WriteTopupSave(new TopupSave { Timestamp = Ts1 });
            PurseBank.WriteTopupSave(new TopupSave { Timestamp = Ts2 });

            var all = PurseBank.GetAllTopupSaves();
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public void GetAllTopupSaves_NoFiles_ReturnsEmpty() {
            var all = PurseBank.GetAllTopupSaves();
            Assert.Empty(all);
        }

        [Fact]
        public void WriteTopupSave_MultipleCompartments_RoundTrip() {
            var save = new TopupSave {
                Timestamp = Ts1,
                Compartments = new List<TopupCompartment> {
                    new TopupCompartment { Key = "Common", Entries = new List<TopupEntry> {
                        new TopupEntry { CurrencyKey = "Scrap", AssetId = 71, Excess = 6999, SlotIndex = 0 },
                    }},
                    new TopupCompartment { Key = "Warrior", Entries = new List<TopupEntry> {
                        new TopupEntry { CurrencyKey = "BlackCoin", AssetId = 58, Excess = 400, SlotIndex = 0 },
                    }},
                },
            };

            PurseBank.WriteTopupSave(save);
            var loaded = PurseBank.FindTopupSave(Ts1);

            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.Compartments.Count);
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
    }
}
