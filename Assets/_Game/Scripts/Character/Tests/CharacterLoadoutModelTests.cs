using NorthStar.Character;
using NUnit.Framework;
using UnityEngine;

namespace NorthStar.Character.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="CharacterLoadoutModel"/> — the pure, engine-free
    /// logic behind <see cref="CharacterCustomizer"/>'s public methods (Equip / Unequip /
    /// SetHair / SetHairColor / GetCurrentLoadout). Each mutator returns whether the loadout
    /// actually changed; that flag is what gates OnLoadoutChanged and the EventBus publish in
    /// the MonoBehaviour, so the change-detection contract is covered here.
    /// </summary>
    public class CharacterLoadoutModelTests
    {
        // ── Construction ──────────────────────────────────────────────────

        [Test]
        public void NewModel_StartsEmpty_WithWhiteHair()
        {
            var m = new CharacterLoadoutModel();
            CharacterLoadout l = m.Loadout;
            Assert.IsTrue(string.IsNullOrEmpty(l.headArmorId));
            Assert.IsTrue(string.IsNullOrEmpty(l.chestArmorId));
            Assert.IsTrue(string.IsNullOrEmpty(l.legsArmorId));
            Assert.IsTrue(string.IsNullOrEmpty(l.handsArmorId));
            Assert.IsTrue(string.IsNullOrEmpty(l.feetArmorId));
            Assert.IsTrue(string.IsNullOrEmpty(l.hairStyleId));
            Assert.AreEqual(Color.white, l.hairColor);
        }

        [Test]
        public void SeededConstructor_PreservesProvidedLoadout()
        {
            var seed = new CharacterLoadout { chestArmorId = "armor-iron-chest", hairColor = Color.red };
            var m = new CharacterLoadoutModel(seed);
            Assert.AreEqual("armor-iron-chest", m.Loadout.chestArmorId);
            Assert.AreEqual(Color.red, m.Loadout.hairColor);
        }

        // ── Equip ─────────────────────────────────────────────────────────

        [Test]
        public void Equip_StoresIdInMatchingSlot_AndReportsChange()
        {
            var m = new CharacterLoadoutModel();
            bool changed = m.Equip(EquipmentSlot.Chest, "armor-iron-chest");
            Assert.IsTrue(changed);
            Assert.AreEqual("armor-iron-chest", m.Loadout.chestArmorId);
            Assert.AreEqual("armor-iron-chest", m.GetArmorId(EquipmentSlot.Chest));
        }

        [Test]
        public void Equip_AllFiveSlots_AreIndependent()
        {
            var m = new CharacterLoadoutModel();
            m.Equip(EquipmentSlot.Head,  "h");
            m.Equip(EquipmentSlot.Chest, "c");
            m.Equip(EquipmentSlot.Legs,  "l");
            m.Equip(EquipmentSlot.Hands, "ha");
            m.Equip(EquipmentSlot.Feet,  "f");

            var l = m.Loadout;
            Assert.AreEqual("h",  l.headArmorId);
            Assert.AreEqual("c",  l.chestArmorId);
            Assert.AreEqual("l",  l.legsArmorId);
            Assert.AreEqual("ha", l.handsArmorId);
            Assert.AreEqual("f",  l.feetArmorId);
        }

        [Test]
        public void Equip_SameIdTwice_SecondCallReportsNoChange()
        {
            var m = new CharacterLoadoutModel();
            Assert.IsTrue(m.Equip(EquipmentSlot.Chest, "armor-iron-chest"));
            Assert.IsFalse(m.Equip(EquipmentSlot.Chest, "armor-iron-chest"));
        }

        [Test]
        public void Equip_ReplacingExistingPiece_ReportsChange()
        {
            var m = new CharacterLoadoutModel();
            m.Equip(EquipmentSlot.Chest, "armor-leather-chest");
            bool changed = m.Equip(EquipmentSlot.Chest, "armor-iron-chest");
            Assert.IsTrue(changed);
            Assert.AreEqual("armor-iron-chest", m.Loadout.chestArmorId);
        }

        [Test]
        public void Equip_NullOrEmptyId_ClearsSlot()
        {
            var m = new CharacterLoadoutModel();
            m.Equip(EquipmentSlot.Chest, "armor-iron-chest");
            bool changed = m.Equip(EquipmentSlot.Chest, null);
            Assert.IsTrue(changed);
            Assert.IsTrue(string.IsNullOrEmpty(m.Loadout.chestArmorId));
        }

        // ── Unequip ───────────────────────────────────────────────────────

        [Test]
        public void Unequip_OccupiedSlot_ClearsAndReportsChange()
        {
            var m = new CharacterLoadoutModel();
            m.Equip(EquipmentSlot.Head, "armor-iron-helm");
            bool changed = m.Unequip(EquipmentSlot.Head);
            Assert.IsTrue(changed);
            Assert.IsTrue(string.IsNullOrEmpty(m.Loadout.headArmorId));
        }

        [Test]
        public void Unequip_EmptySlot_ReportsNoChange()
        {
            var m = new CharacterLoadoutModel();
            Assert.IsFalse(m.Unequip(EquipmentSlot.Head));
        }

        [Test]
        public void Unequip_DoesNotAffectOtherSlots()
        {
            var m = new CharacterLoadoutModel();
            m.Equip(EquipmentSlot.Chest, "c");
            m.Equip(EquipmentSlot.Legs,  "l");
            m.Unequip(EquipmentSlot.Chest);
            Assert.IsTrue(string.IsNullOrEmpty(m.Loadout.chestArmorId));
            Assert.AreEqual("l", m.Loadout.legsArmorId);
        }

        // ── SetHair ───────────────────────────────────────────────────────

        [Test]
        public void SetHair_StoresStyleId_AndReportsChange()
        {
            var m = new CharacterLoadoutModel();
            bool changed = m.SetHair("hair-short-crop");
            Assert.IsTrue(changed);
            Assert.AreEqual("hair-short-crop", m.Loadout.hairStyleId);
        }

        [Test]
        public void SetHair_SameStyleTwice_SecondReportsNoChange()
        {
            var m = new CharacterLoadoutModel();
            Assert.IsTrue(m.SetHair("hair-short-crop"));
            Assert.IsFalse(m.SetHair("hair-short-crop"));
        }

        [Test]
        public void SetHair_NullClearsStyle()
        {
            var m = new CharacterLoadoutModel();
            m.SetHair("hair-short-crop");
            bool changed = m.SetHair(null);
            Assert.IsTrue(changed);
            Assert.IsTrue(string.IsNullOrEmpty(m.Loadout.hairStyleId));
        }

        // ── SetHairColor ──────────────────────────────────────────────────

        [Test]
        public void SetHairColor_StoresColor_AndReportsChange()
        {
            var m = new CharacterLoadoutModel();
            bool changed = m.SetHairColor(Color.red);
            Assert.IsTrue(changed);
            Assert.AreEqual(Color.red, m.Loadout.hairColor);
        }

        [Test]
        public void SetHairColor_SameColorTwice_SecondReportsNoChange()
        {
            var m = new CharacterLoadoutModel();
            m.SetHairColor(Color.green);
            Assert.IsFalse(m.SetHairColor(Color.green));
        }

        // ── GetArmorId fallback ───────────────────────────────────────────

        [Test]
        public void GetArmorId_EmptySlot_ReturnsNullOrEmpty()
        {
            var m = new CharacterLoadoutModel();
            Assert.IsTrue(string.IsNullOrEmpty(m.GetArmorId(EquipmentSlot.Feet)));
        }

        // ── Save safety ───────────────────────────────────────────────────

        [Test]
        public void Loadout_IsSaveSafe_HoldsIdsNotReferences()
        {
            // CharacterLoadout fields are strings + Color only (no UnityEngine.Object refs),
            // so the struct round-trips through JsonUtility cleanly.
            var m = new CharacterLoadoutModel();
            m.Equip(EquipmentSlot.Chest, "armor-iron-chest");
            m.SetHair("hair-long-braid");
            m.SetHairColor(new Color(0.2f, 0.4f, 0.6f, 1f));

            string json = JsonUtility.ToJson(m.Loadout);
            var restored = JsonUtility.FromJson<CharacterLoadout>(json);

            Assert.AreEqual("armor-iron-chest", restored.chestArmorId);
            Assert.AreEqual("hair-long-braid", restored.hairStyleId);
            Assert.AreEqual(new Color(0.2f, 0.4f, 0.6f, 1f), restored.hairColor);
        }
    }
}
