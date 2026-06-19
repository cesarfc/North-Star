using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NorthStar.Core.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SaveSystem"/>. These touch the real filesystem under
    /// <c>Application.persistentDataPath/saves/</c> using a unique slot name per run, and
    /// delete the slot in teardown so tests stay isolated and leave no artifacts.
    /// </summary>
    public class SaveSystemTests
    {
        private string _slot;

        [SetUp]
        public void SetUp()
        {
            // Unique per test so a crashed prior run cannot leak state into this one.
            _slot = "test-slot-" + Guid.NewGuid().ToString("N");
            SaveSystem.DeleteSave(_slot);
        }

        [TearDown]
        public void TearDown() => SaveSystem.DeleteSave(_slot);

        private static GameSaveData MakeSample()
        {
            return new GameSaveData
            {
                playTimeSeconds = 123.5f,
                currentZoneId = "forest-zone-01",
                activeQuestId = "quest-find-the-key",
                loadout = new CharacterLoadout
                {
                    chestArmorId = "armor-iron-chest",
                    hairStyleId = "hair-short",
                    hairColor = new Color(0.2f, 0.3f, 0.4f, 1f)
                },
                inventory = new InventorySnapshot
                {
                    gold = 250,
                    entries = new[]
                    {
                        new InventoryEntry { itemId = "potion-health", quantity = 3 },
                        new InventoryEntry { itemId = "key-bronze", quantity = 1 }
                    }
                },
                questFlags = new[]
                {
                    new QuestFlagEntry { questId = "quest-intro", completed = true },
                    new QuestFlagEntry { questId = "quest-find-the-key", completed = false }
                }
            };
        }

        [Test]
        public void Save_ReturnsTrue_AndSlotExists()
        {
            bool ok = SaveSystem.Save(_slot, MakeSample());

            Assert.IsTrue(ok, "Save should succeed for valid data.");
            Assert.IsTrue(SaveSystem.SaveExists(_slot), "Slot should exist on disk after Save.");
        }

        [Test]
        public void SaveThenLoad_RoundTripsScalarFields()
        {
            var original = MakeSample();
            SaveSystem.Save(_slot, original);

            GameSaveData loaded = SaveSystem.Load(_slot);

            Assert.IsNotNull(loaded, "Load should return data for an existing slot.");
            Assert.AreEqual(original.playTimeSeconds, loaded.playTimeSeconds);
            Assert.AreEqual(original.currentZoneId, loaded.currentZoneId);
            Assert.AreEqual(original.activeQuestId, loaded.activeQuestId);
        }

        [Test]
        public void SaveThenLoad_RoundTripsNestedLoadoutAndInventory()
        {
            var original = MakeSample();
            SaveSystem.Save(_slot, original);

            GameSaveData loaded = SaveSystem.Load(_slot);

            Assert.AreEqual(original.loadout.chestArmorId, loaded.loadout.chestArmorId);
            Assert.AreEqual(original.loadout.hairStyleId, loaded.loadout.hairStyleId);
            Assert.AreEqual(original.loadout.hairColor, loaded.loadout.hairColor);

            Assert.AreEqual(original.inventory.gold, loaded.inventory.gold);
            Assert.AreEqual(original.inventory.entries.Length, loaded.inventory.entries.Length);
            Assert.AreEqual("potion-health", loaded.inventory.entries[0].itemId);
            Assert.AreEqual(3, loaded.inventory.entries[0].quantity);
        }

        [Test]
        public void SaveThenLoad_RoundTripsQuestFlagArray()
        {
            var original = MakeSample();
            SaveSystem.Save(_slot, original);

            GameSaveData loaded = SaveSystem.Load(_slot);

            Assert.IsNotNull(loaded.questFlags);
            Assert.AreEqual(2, loaded.questFlags.Length, "QuestFlagEntry[] should survive JSON round-trip.");
            Assert.AreEqual("quest-intro", loaded.questFlags[0].questId);
            Assert.IsTrue(loaded.questFlags[0].completed);
            Assert.AreEqual("quest-find-the-key", loaded.questFlags[1].questId);
            Assert.IsFalse(loaded.questFlags[1].completed);
        }

        [Test]
        public void Save_StampsSavedAt_WithUtcTimestamp()
        {
            var data = MakeSample();
            Assert.IsTrue(string.IsNullOrEmpty(data.savedAt), "Precondition: savedAt unset before Save.");

            SaveSystem.Save(_slot, data);
            GameSaveData loaded = SaveSystem.Load(_slot);

            Assert.IsFalse(string.IsNullOrEmpty(loaded.savedAt), "Save should stamp savedAt.");
            Assert.DoesNotThrow(() => DateTime.Parse(loaded.savedAt),
                "savedAt should be a parseable ISO-8601 timestamp.");
        }

        [Test]
        public void Load_MissingSlot_ReturnsNull()
        {
            string missing = "definitely-missing-" + Guid.NewGuid().ToString("N");
            Assert.IsFalse(SaveSystem.SaveExists(missing));

            GameSaveData loaded = SaveSystem.Load(missing);

            Assert.IsNull(loaded, "Load must return null (not a default object) for a missing slot.");
        }

        [Test]
        public void SaveExists_FalseForMissing_TrueAfterSave()
        {
            Assert.IsFalse(SaveSystem.SaveExists(_slot), "Slot should not exist before Save.");
            SaveSystem.Save(_slot, MakeSample());
            Assert.IsTrue(SaveSystem.SaveExists(_slot), "Slot should exist after Save.");
        }

        [Test]
        public void DeleteSave_RemovesTheSlot()
        {
            SaveSystem.Save(_slot, MakeSample());
            Assert.IsTrue(SaveSystem.SaveExists(_slot));

            SaveSystem.DeleteSave(_slot);

            Assert.IsFalse(SaveSystem.SaveExists(_slot), "Slot should be gone after DeleteSave.");
            Assert.IsNull(SaveSystem.Load(_slot), "Load after delete should return null.");
        }

        [Test]
        public void DeleteSave_MissingSlot_DoesNotThrow()
        {
            string missing = "missing-" + Guid.NewGuid().ToString("N");
            Assert.DoesNotThrow(() => SaveSystem.DeleteSave(missing));
        }

        [Test]
        public void Save_NullData_ReturnsFalse()
        {
            // SaveSystem logs an error on invalid input; declare it so the runner does not fail.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("SaveSystem.*null data"));

            bool ok = SaveSystem.Save(_slot, null);

            Assert.IsFalse(ok, "Saving null data should fail gracefully.");
            Assert.IsFalse(SaveSystem.SaveExists(_slot), "No file should be written for null data.");
        }

        [Test]
        public void Save_EmptySlotName_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("SaveSystem.*empty slot"));
            Assert.IsFalse(SaveSystem.Save("", MakeSample()));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("SaveSystem.*empty slot"));
            Assert.IsFalse(SaveSystem.Save(null, MakeSample()));
        }
    }
}
