using System.Collections.Generic;
using HandyPurse.Bank;
using Xunit;

namespace HandyPurse.Tests {
    // Asset IDs and vanilla caps taken directly from the live save file
    // (019d319b-b1c0-73cf-acd5-bf087cc138a7.json):
    //   BlackCoin  type=30  assetId=58  vanillaCap=200
    //   Glitter    type=32  assetId=59  vanillaCap=200
    //   Scrap      type=40  assetId=71  vanillaCap=3000
    public class BankLogicTests {
        private const int BlackCoin = BankLogic.TypeBlackCoin;   // 30
        private const int Glitter = BankLogic.TypeGlitter;     // 32
        private const int Scrap = BankLogic.TypeScrap;       // 40

        private const int AssetBlackCoin = 58;
        private const int AssetGlitter = 59;
        private const int AssetScrap = 71;

        private const int CapBlackCoin = 200;
        private const int CapGlitter = 200;
        private const int CapScrap = 3000;

        private static int? VanillaCap(int assetId) => assetId switch {
            AssetBlackCoin => CapBlackCoin,
            AssetGlitter => CapGlitter,
            AssetScrap => CapScrap,
            _ => null,
        };

        // ── ComputeExcess ─────────────────────────────────────────────────

        [Fact]
        public void ComputeExcess_SingleScrapSlotAboveCap_StripsExcess() {
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 9999 },
            };

            var entries = BankLogic.ComputeExcess(slots, VanillaCap);

            Assert.Single(entries);
            Assert.Equal("Scrap", entries[0].CurrencyKey);
            Assert.Equal(AssetScrap, entries[0].AssetId);
            Assert.Equal(CapScrap, entries[0].VanillaAmount);
            Assert.Equal(6999, entries[0].Excess);
            Assert.Equal(0, entries[0].SlotIndex);
            // Slot clamped to vanilla cap.
            Assert.Equal(CapScrap, slots[0].Amount);
        }

        [Fact]
        public void ComputeExcess_SlotAtCap_ProducesNoEntry() {
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 },
            };

            var entries = BankLogic.ComputeExcess(slots, VanillaCap);

            Assert.Empty(entries);
            Assert.Equal(3000, slots[0].Amount);
        }

        [Fact]
        public void ComputeExcess_SlotBelowCap_NotTouched() {
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 1500 },
            };

            var entries = BankLogic.ComputeExcess(slots, VanillaCap);

            Assert.Empty(entries);
            Assert.Equal(1500, slots[0].Amount);
        }

        [Fact]
        public void ComputeExcess_NonManagedCurrency_NotTouched() {
            // ItemType=10 is not a managed currency
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = 10, AssetId = 56, Amount = 99999 },
            };

            var entries = BankLogic.ComputeExcess(slots, _ => 5);

            Assert.Empty(entries);
            Assert.Equal(99999, slots[0].Amount);
        }

        [Fact]
        public void ComputeExcess_MixedSlots_OnlyAboveCapStripped() {
            // Mirrors the actual save: 20 Scrap slots (all at 3000) + BlackCoin above cap
            var slots = new List<ItemSlot>();
            for (int i = 0; i < 20; i++) {
                slots.Add(new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 });
            }
            slots.Add(new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 600 });
            slots.Add(new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 100 });
            slots.Add(new ItemSlot { ItemType = Glitter, AssetId = AssetGlitter, Amount = 500 });

            var entries = BankLogic.ComputeExcess(slots, VanillaCap);

            // Only the 600 BlackCoin and 500 Glitter are above cap
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.CurrencyKey == "BlackCoin" && e.Excess == 400);
            Assert.Contains(entries, e => e.CurrencyKey == "Glitter" && e.Excess == 300);

            // All 20 Scrap slots untouched
            for (int i = 0; i < 20; i++) { Assert.Equal(3000, slots[i].Amount); }

            // Clamped slots
            Assert.Equal(CapBlackCoin, slots[20].Amount);
            Assert.Equal(100, slots[21].Amount);          // was already under cap
            Assert.Equal(CapGlitter, slots[22].Amount);
        }

        [Fact]
        public void ComputeExcess_RealSaveStructure_NothingToStrip() {
            // The actual save has all currencies already at or below vanilla cap
            // (HandyPurse previously stripped them to topup file)
            var slots = new List<ItemSlot>();
            for (int i = 0; i < 20; i++) {
                slots.Add(new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 });
            }
            // 33 BlackCoin slots at 200
            for (int i = 0; i < 33; i++) {
                slots.Add(new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 200 });
            }

            var entries = BankLogic.ComputeExcess(slots, VanillaCap);

            Assert.Empty(entries);
        }

        // ── ComputeHash ───────────────────────────────────────────────────

        [Fact]
        public void ComputeHash_SameSlots_ProducesSameHash() {
            var slots = MakeScrapSlot(9999);
            var hash1 = BankLogic.ComputeHash(slots);
            var hash2 = BankLogic.ComputeHash(slots);
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void ComputeHash_DifferentAmounts_ProducesDifferentHash() {
            var hash1 = BankLogic.ComputeHash(MakeScrapSlot(3000));
            var hash2 = BankLogic.ComputeHash(MakeScrapSlot(9999));
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void ComputeHash_NonManagedSlotsIgnored() {
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 },
                new ItemSlot { ItemType = 10,    AssetId = 56,         Amount = 99999 },
            };
            var hashWithExtra = BankLogic.ComputeHash(slots);
            var hashWithout = BankLogic.ComputeHash(MakeScrapSlot(3000));
            Assert.Equal(hashWithout, hashWithExtra);
        }

        // ── ApplyTopup — normal round-trip ────────────────────────────────
        // HandyPurse clamps before cloud serialisation. Cloud stores clamped amounts.
        // On load, cloud returns clamped amounts. ComputeHash documents the invariant
        // that the post-clamp slot set produces the same hash as the cloud-returned state.

        [Fact]
        public void ApplyTopup_RoundTrip_CloudReturnsClamped_ExcessRestored() {
            // Save: player had 9999 scrap. ComputeExcess clamps to 3000, stores excess=6999.
            var saveSlots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 9999 },
            };
            var entries = BankLogic.ComputeExcess(saveSlots, VanillaCap);
            var saveHash = BankLogic.ComputeHash(saveSlots); // hash of post-clamp state

            // Load: cloud returns the clamped amount (as written by HandyPurse).
            var loadedSlots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 },
            };
            // Post-clamp hash matches cloud-returned state.
            Assert.Equal(saveHash, BankLogic.ComputeHash(loadedSlots));

            var unresolved = BankLogic.ApplyTopup(loadedSlots, entries);

            Assert.Equal(9999, loadedSlots[0].Amount);  // excess restored
            Assert.Empty(unresolved);
        }

        [Fact]
        public void ApplyTopup_RoundTrip_MultiCurrency_ExcessRestored() {
            var saveSlots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap,     AssetId = AssetScrap,     Amount = 9999 },
                new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 600  },
                new ItemSlot { ItemType = Glitter,   AssetId = AssetGlitter,   Amount = 450  },
                new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 50   },
            };
            var entries = BankLogic.ComputeExcess(saveSlots, VanillaCap);
            var saveHash = BankLogic.ComputeHash(saveSlots);
            // saveSlots now: Scrap=3000, BlackCoin[0]=200, Glitter=200, BlackCoin[1]=50 (untouched)

            // Load: cloud returns clamped amounts.
            var loadedSlots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap,     AssetId = AssetScrap,     Amount = 3000 },
                new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 200  },
                new ItemSlot { ItemType = Glitter,   AssetId = AssetGlitter,   Amount = 200  },
                new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 50   },
            };
            Assert.Equal(saveHash, BankLogic.ComputeHash(loadedSlots));

            var unresolved = BankLogic.ApplyTopup(loadedSlots, entries);

            Assert.Equal(9999, loadedSlots[0].Amount);
            Assert.Equal(600, loadedSlots[1].Amount);
            Assert.Equal(450, loadedSlots[2].Amount);
            Assert.Equal(50, loadedSlots[3].Amount);  // was under cap, untouched
            Assert.Empty(unresolved);
        }

        // ── ApplyTopup — hash is order-independent ────────────────────────
        // ComputeHash sorts by assetId so reordered slots with the same amounts produce the
        // same hash — useful as an integrity check for slot layout identity.

        [Fact]
        public void ApplyTopup_HashIsOrderIndependent_SortedByAssetId() {
            var slots1 = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap,     AssetId = AssetScrap,     Amount = 3000 },
                new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 200  },
            };
            var slots2 = new List<ItemSlot> {
                new ItemSlot { ItemType = BlackCoin, AssetId = AssetBlackCoin, Amount = 200  },
                new ItemSlot { ItemType = Scrap,     AssetId = AssetScrap,     Amount = 3000 },
            };
            Assert.Equal(BankLogic.ComputeHash(slots1), BankLogic.ComputeHash(slots2));
        }

        // ── ApplyTopup — empty entries ────────────────────────────────────

        [Fact]
        public void ApplyTopup_EmptyEntries_NoChange() {
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 },
            };

            var unresolved = BankLogic.ApplyTopup(slots, new List<TopupEntry>());

            Assert.Equal(3000, slots[0].Amount);
            Assert.Empty(unresolved);
        }

        // ── ApplyTopup — invalid slot indices ────────────────────────────

        [Fact]
        public void ApplyTopup_NullSlotIndex_EntryReturnedAsUnresolved() {
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 },
            };
            var entries = new List<TopupEntry> {
                new TopupEntry { CurrencyKey = "Scrap", AssetId = AssetScrap, Excess = 1000, SlotIndex = null },
            };

            var unresolved = BankLogic.ApplyTopup(slots, entries);

            Assert.Equal(3000, slots[0].Amount);  // not modified
            Assert.Single(unresolved);
            Assert.Equal("Scrap", unresolved[0].CurrencyKey);
        }

        [Fact]
        public void ApplyTopup_OutOfBoundsSlotIndex_EntryReturnedAsUnresolved() {
            var slots = new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = 3000 },
            };
            var entries = new List<TopupEntry> {
                new TopupEntry { CurrencyKey = "Scrap", AssetId = AssetScrap, Excess = 1000, SlotIndex = 5 },
            };

            var unresolved = BankLogic.ApplyTopup(slots, entries);

            Assert.Equal(3000, slots[0].Amount);  // not modified
            Assert.Single(unresolved);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static List<ItemSlot> MakeScrapSlot(int amount) =>
            new List<ItemSlot> {
                new ItemSlot { ItemType = Scrap, AssetId = AssetScrap, Amount = amount },
            };
    }
}
