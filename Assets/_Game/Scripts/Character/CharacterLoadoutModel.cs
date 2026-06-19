using UnityEngine;

namespace NorthStar.Character
{
    /// <summary>
    /// Pure, engine-glue-free model of a character's equipped loadout. Holds the
    /// <see cref="CharacterLoadout"/> struct (save-safe IDs only) and the rules for
    /// mutating it. Every mutator returns <c>true</c> only when the loadout actually
    /// changed, so the owning <see cref="CharacterCustomizer"/> can avoid redundant
    /// mesh swaps and events. Kept free of <see cref="MonoBehaviour"/> so it is fully
    /// EditMode-testable without play mode.
    /// </summary>
    public class CharacterLoadoutModel
    {
        private CharacterLoadout _loadout;

        /// <summary>A copy of the current loadout (struct semantics — callers cannot mutate internals).</summary>
        public CharacterLoadout Loadout => _loadout;

        /// <summary>Create an empty loadout with a default (white) hair color.</summary>
        public CharacterLoadoutModel()
        {
            _loadout = new CharacterLoadout { hairColor = Color.white };
        }

        /// <summary>Create a model seeded from an existing (e.g. loaded) loadout.</summary>
        public CharacterLoadoutModel(CharacterLoadout loadout)
        {
            _loadout = loadout;
        }

        /// <summary>
        /// Record an armor id in the given slot. Passing <c>null</c> clears the slot
        /// (equivalent to <see cref="Unequip"/>). Returns <c>true</c> if the stored id changed.
        /// </summary>
        public bool Equip(EquipmentSlot slot, string itemId)
        {
            string current = GetArmorId(slot);
            string next = string.IsNullOrEmpty(itemId) ? null : itemId;
            if (current == next) return false;
            SetArmorId(slot, next);
            return true;
        }

        /// <summary>Clear the armor id in the given slot. Returns <c>true</c> if the slot was occupied.</summary>
        public bool Unequip(EquipmentSlot slot)
        {
            if (string.IsNullOrEmpty(GetArmorId(slot))) return false;
            SetArmorId(slot, null);
            return true;
        }

        /// <summary>
        /// Set the hairstyle id (or <c>null</c> to clear). Returns <c>true</c> if the id changed.
        /// </summary>
        public bool SetHair(string styleId)
        {
            string next = string.IsNullOrEmpty(styleId) ? null : styleId;
            if (_loadout.hairStyleId == next) return false;
            _loadout.hairStyleId = next;
            return true;
        }

        /// <summary>Set the hair tint color. Returns <c>true</c> if the color changed.</summary>
        public bool SetHairColor(Color color)
        {
            if (_loadout.hairColor == color) return false;
            _loadout.hairColor = color;
            return true;
        }

        /// <summary>Read the armor id currently stored for a slot (<c>null</c> when empty).</summary>
        public string GetArmorId(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head:  return _loadout.headArmorId;
                case EquipmentSlot.Chest: return _loadout.chestArmorId;
                case EquipmentSlot.Legs:  return _loadout.legsArmorId;
                case EquipmentSlot.Hands: return _loadout.handsArmorId;
                case EquipmentSlot.Feet:  return _loadout.feetArmorId;
                default:                  return null;
            }
        }

        private void SetArmorId(EquipmentSlot slot, string id)
        {
            switch (slot)
            {
                case EquipmentSlot.Head:  _loadout.headArmorId  = id; break;
                case EquipmentSlot.Chest: _loadout.chestArmorId = id; break;
                case EquipmentSlot.Legs:  _loadout.legsArmorId  = id; break;
                case EquipmentSlot.Hands: _loadout.handsArmorId = id; break;
                case EquipmentSlot.Feet:  _loadout.feetArmorId  = id; break;
            }
        }
    }
}
