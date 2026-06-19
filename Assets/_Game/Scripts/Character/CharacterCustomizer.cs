using System;
using System.Collections.Generic;
using UnityEngine;

namespace NorthStar.Character
{
    /// <summary>
    /// Runtime character customization: swaps armor and hair meshes on a shared skeleton
    /// and tracks the equipped <see cref="CharacterLoadout"/>. All armor pieces share the
    /// same bone hierarchy, so equipping is a <see cref="SkinnedMeshRenderer.sharedMesh"/>
    /// swap (bones/root are rebound to this character's rig) rather than instantiating prefabs.
    ///
    /// The pure loadout rules live in <see cref="CharacterLoadoutModel"/>; this MonoBehaviour is
    /// the engine glue. Every change raises the <see cref="OnLoadoutChanged"/> C# event and
    /// publishes a <see cref="LoadoutChangedEvent"/> on the <see cref="EventBus"/>. The loadout
    /// stores item IDs (never object references), so it is save-safe.
    /// </summary>
    public class CharacterCustomizer : MonoBehaviour
    {
        [Serializable]
        private struct ArmorRendererBinding
        {
            public EquipmentSlot slot;
            public SkinnedMeshRenderer renderer;
        }

        [Header("Armor Renderers (one SkinnedMeshRenderer per equipment slot)")]
        [Tooltip("Each entry binds a SkinnedMeshRenderer on the shared rig to an equipment slot.")]
        [SerializeField] private ArmorRendererBinding[] _armorRenderers = Array.Empty<ArmorRendererBinding>();

        [Header("Hair")]
        [SerializeField] private SkinnedMeshRenderer _hairRenderer;
        [Tooltip("Shader color property used to tint hair (URP Lit uses _BaseColor).")]
        [SerializeField] private string _hairColorProperty = "_BaseColor";

        // Resolved slot -> renderer map, built once from the serialized bindings.
        private readonly Dictionary<EquipmentSlot, SkinnedMeshRenderer> _slotRenderers
            = new Dictionary<EquipmentSlot, SkinnedMeshRenderer>();

        private CharacterLoadoutModel _model;
        private MaterialPropertyBlock _hairBlock;

        /// <summary>Raised after any change to the loadout, carrying the full new loadout.</summary>
        public event Action<CharacterLoadout> OnLoadoutChanged;

        private void Awake()
        {
            _model = new CharacterLoadoutModel();
            BuildRendererMap();
        }

        /// <summary>Index the serialized armor renderer bindings into a slot lookup.</summary>
        private void BuildRendererMap()
        {
            _slotRenderers.Clear();
            if (_armorRenderers == null) return;
            foreach (var binding in _armorRenderers)
            {
                if (binding.renderer != null)
                    _slotRenderers[binding.slot] = binding.renderer;
            }
        }

        /// <summary>
        /// Equip an armor piece: store its id in the matching slot and swap the slot's
        /// SkinnedMeshRenderer to the armor's mesh and materials. The armor's own
        /// <see cref="ArmorData.slot"/> is authoritative — the <paramref name="slot"/>
        /// argument is honored for API compatibility but mismatches are corrected to the
        /// data's slot. Null data unequips the slot.
        /// </summary>
        public void Equip(EquipmentSlot slot, ArmorData data)
        {
            if (data == null)
            {
                Unequip(slot);
                return;
            }

            EquipmentSlot targetSlot = data.slot;
            bool changed = _model.Equip(targetSlot, data.itemId);
            ApplyArmorVisual(targetSlot, data);
            if (changed) RaiseChanged();
        }

        /// <summary>Remove whatever armor occupies the slot and clear its mesh.</summary>
        public void Unequip(EquipmentSlot slot)
        {
            bool changed = _model.Unequip(slot);
            ApplyArmorVisual(slot, null);
            if (changed) RaiseChanged();
        }

        /// <summary>
        /// Set the active hairstyle: store its id and swap the hair renderer's mesh.
        /// Null data clears the hairstyle. Re-applies the current hair color to the new mesh.
        /// </summary>
        public void SetHair(HairStyleData data)
        {
            string styleId = data != null ? data.styleId : null;
            bool changed = _model.SetHair(styleId);

            if (_hairRenderer != null)
                _hairRenderer.sharedMesh = data != null ? data.mesh : null;

            ApplyHairColor(_model.Loadout.hairColor);
            if (changed) RaiseChanged();
        }

        /// <summary>Tint the current hair mesh. Stored in the loadout so it survives save/load.</summary>
        public void SetHairColor(Color color)
        {
            bool changed = _model.SetHairColor(color);
            ApplyHairColor(color);
            if (changed) RaiseChanged();
        }

        /// <summary>Return a copy of the current save-safe loadout (IDs + hair color).</summary>
        public CharacterLoadout GetCurrentLoadout() => _model.Loadout;

        // ── Visual application ────────────────────────────────────────────

        private void ApplyArmorVisual(EquipmentSlot slot, ArmorData data)
        {
            if (!_slotRenderers.TryGetValue(slot, out var renderer) || renderer == null)
                return;

            if (data == null)
            {
                renderer.sharedMesh = null;
                renderer.enabled = false;
                return;
            }

            renderer.sharedMesh = data.mesh;
            if (data.materials != null && data.materials.Length > 0)
                renderer.sharedMaterials = data.materials;
            renderer.enabled = data.mesh != null;
        }

        private void ApplyHairColor(Color color)
        {
            if (_hairRenderer == null) return;
            _hairBlock ??= new MaterialPropertyBlock();
            _hairRenderer.GetPropertyBlock(_hairBlock);
            _hairBlock.SetColor(_hairColorProperty, color);
            _hairRenderer.SetPropertyBlock(_hairBlock);
        }

        private void RaiseChanged()
        {
            CharacterLoadout loadout = _model.Loadout;
            OnLoadoutChanged?.Invoke(loadout);
            EventBus.Publish(new LoadoutChangedEvent { loadout = loadout });
        }
    }
}
