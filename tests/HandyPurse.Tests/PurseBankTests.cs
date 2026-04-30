using System.Collections.Generic;
using System.IO;
using HandyPurse.Bank;
using Xunit;

namespace HandyPurse.Tests {
    public class PurseBankTests : System.IDisposable {
        private readonly string _tempDir;

        public PurseBankTests() {
            _tempDir = Path.Combine(Path.GetTempPath(), "HandyPurse_Tests_" + System.Guid.NewGuid().ToString("N"));
            PurseBank.OverrideDataDir(_tempDir);
        }

        public void Dispose() {
            if (Directory.Exists(_tempDir)) {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        // ── Topup file ────────────────────────────────────────────────────

        [Fact]
        public void SaveAndLoadTopup_RoundTrip() {
            var data = new TopupData();
            var compartment = PurseBank.GetOrCreateCompartment(data, "Common");
            compartment.Hash = "58:3000";
            compartment.Entries.Add(new TopupEntry {
                CurrencyKey = "Scrap",
                AssetId = 71,
                VanillaAmount = 3000,
                Excess = 6999,
                SlotIndex = 0,
            });

            PurseBank.SaveTopup(data);
            var loaded = PurseBank.LoadTopup();

            Assert.Single(loaded.Compartments);
            Assert.Equal("Common", loaded.Compartments[0].Key);
            Assert.Equal("58:3000", loaded.Compartments[0].Hash);
            Assert.Single(loaded.Compartments[0].Entries);
            Assert.Equal(6999, loaded.Compartments[0].Entries[0].Excess);
        }

        [Fact]
        public void SaveTopup_EmptyData_DeletesFile() {
            // First write a non-empty topup
            var data = new TopupData();
            PurseBank.GetOrCreateCompartment(data, "Common").Entries.Add(
                new TopupEntry { CurrencyKey = "Scrap", AssetId = 71, Excess = 100 });
            PurseBank.SaveTopup(data);
            Assert.True(File.Exists(Path.Combine(_tempDir, "topup.json")));

            // Now save empty — should delete the file
            PurseBank.SaveTopup(new TopupData());
            Assert.False(File.Exists(Path.Combine(_tempDir, "topup.json")));
        }

        [Fact]
        public void LoadTopup_NoFile_ReturnsEmpty() {
            var data = PurseBank.LoadTopup();
            Assert.Empty(data.Compartments);
        }

        [Fact]
        public void RemoveCompartment_RemovesCorrectKey() {
            var data = new TopupData();
            PurseBank.GetOrCreateCompartment(data, "Common");
            PurseBank.GetOrCreateCompartment(data, "Warrior");

            PurseBank.RemoveCompartment(data, "Common");

            Assert.Single(data.Compartments);
            Assert.Equal("Warrior", data.Compartments[0].Key);
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
