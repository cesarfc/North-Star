#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NorthStar.Character;
using UnityEditor;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// Turns the Characters-factory FBX handoff pack into the game's wardrobe data:
    /// one <see cref="ArmorData"/> per worn/armor piece and one <see cref="HairStyleData"/>
    /// per hairstyle, meshes/materials wired from the imported FBX and <c>boneNames</c>
    /// extracted for skinned pieces so <see cref="CharacterCustomizer"/> can rebind them
    /// onto the shared skeleton. Idempotent: re-running updates meshes in place and never
    /// changes an existing asset's itemId/styleId (IDs are save-file contracts).
    ///
    /// Menu: <b>Tools ▸ North-Star ▸ Character ▸ Build Asset Library From Manifest</b>.
    /// Headless: <c>-executeMethod NorthStar.EditorTools.CharacterAssetLibraryBuilder.BuildAndExit</c>.
    /// Logs one <c>[AssetLib] OK/FAIL</c> summary line for gate scripts to grep.
    /// </summary>
    public static class CharacterAssetLibraryBuilder
    {
        private const string ArmorDir = "Assets/_Game/ScriptableObjects/Armor";
        private const string HairDir = "Assets/_Game/ScriptableObjects/Hair";

        // Manifest wornGear/socketGear slot → the customizer's EquipmentSlot.
        private static readonly Dictionary<string, EquipmentSlot> WornSlotMap = new Dictionary<string, EquipmentSlot>
        {
            { "armor_overlay", EquipmentSlot.Chest },
            { "body_outfit", EquipmentSlot.Chest },
            { "legs", EquipmentSlot.Legs },
            { "feet", EquipmentSlot.Feet },
            { "helmet", EquipmentSlot.Head },
        };

        // Manifest id → stable asset file name (existing assets keep their historical names).
        private static readonly Dictionary<string, string> AssetNameMap = new Dictionary<string, string>
        {
            { "iron_chestplate", "SO_Armor_IronChestplate" },
            { "cloth_tunic", "SO_Armor_ClothTunic" },
            { "leather_pants", "SO_Armor_LeatherPants" },
            { "leather_boots", "SO_Armor_LeatherBoots" },
            { "iron_helmet", "SO_Armor_IronHelmet" },
            { "hair_short", "SO_Hair_ShortCrop" },
            { "hair_long", "SO_Hair_LongBraid" },
            { "hair_spiky", "SO_Hair_Spiky" },
        };

        private static readonly Dictionary<string, string> DisplayNameMap = new Dictionary<string, string>
        {
            { "iron_chestplate", "Iron Chestplate" },
            { "cloth_tunic", "Cloth Tunic" },
            { "leather_pants", "Leather Pants" },
            { "leather_boots", "Leather Boots" },
            { "iron_helmet", "Iron Helmet" },
            { "hair_short", "Short Crop" },
            { "hair_long", "Long Braid" },
            { "hair_spiky", "Spiky" },
        };

        // id → (defenseBonus, weightClass). Placeholder balance values; SO fields stay editable.
        private static readonly Dictionary<string, (int defense, int weight)> StatMap =
            new Dictionary<string, (int, int)>
            {
                { "iron_chestplate", (5, 3) },
                { "cloth_tunic", (1, 1) },
                { "leather_pants", (2, 2) },
                { "leather_boots", (1, 2) },
                { "iron_helmet", (3, 3) },
            };

        private static readonly Color[] DefaultHairColors =
        {
            new Color(0.12f, 0.09f, 0.07f, 1f),
            new Color(0.45f, 0.31f, 0.18f, 1f),
            new Color(0.85f, 0.74f, 0.45f, 1f),
            new Color(0.70f, 0.70f, 0.72f, 1f),
        };

        /// <summary>Headless entry point: build the library, then exit 0 (OK) / 1 (any failure).</summary>
        public static void BuildAndExit() => EditorApplication.Exit(Build() ? 0 : 1);

        /// <summary>
        /// Build/refresh every wardrobe ScriptableObject from the handoff manifest.
        /// Returns <c>true</c> when all manifest pieces resolved to meshes and were written.
        /// </summary>
        [MenuItem("Tools/North-Star/Character/Build Asset Library From Manifest")]
        public static bool Build()
        {
            NorthStarManifest manifest = NorthStarManifest.Load();
            if (manifest == null) return false;

            int ok = 0, failed = 0;

            foreach (NorthStarManifest.WornGear worn in manifest.wornGear)
            {
                if (BuildWornArmor(worn)) ok++; else failed++;
            }

            foreach (NorthStarManifest.SocketGear gear in manifest.socketGear)
            {
                switch (gear.slot)
                {
                    case "helmet":
                        if (BuildStaticArmor(gear)) ok++; else failed++;
                        break;
                    case "hair":
                        if (BuildHair(gear)) ok++; else failed++;
                        break;
                    // weapons / offhand / cape are socket props, not EquipmentSlot armor —
                    // the scene builder mounts their FBX on the manifest sockets directly.
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AssetLib] {(failed == 0 ? "OK" : "FAIL")} — wardrobe assets written={ok}, failed={failed} " +
                      $"(rig={manifest.rig}, packVersion={manifest.packVersion}).");
            return failed == 0;
        }

        // ── Worn (skinned) armor ──────────────────────────────────────────

        private static bool BuildWornArmor(NorthStarManifest.WornGear worn)
        {
            if (!WornSlotMap.TryGetValue(worn.slot, out EquipmentSlot slot))
            {
                Debug.LogError($"[AssetLib] '{worn.id}': unknown worn slot '{worn.slot}'.");
                return false;
            }

            string fbxPath = NorthStarManifest.FbxAssetPath(worn.fbx);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            SkinnedMeshRenderer smr = source != null ? source.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (smr == null || smr.sharedMesh == null)
            {
                Debug.LogError($"[AssetLib] '{worn.id}': no skinned mesh in '{fbxPath}' — re-export via the factory's skin_garment path.");
                return false;
            }

            ArmorData armor = LoadOrCreate<ArmorData>(ArmorDir, AssetName(worn.id));
            if (string.IsNullOrEmpty(armor.itemId)) armor.itemId = "armor-" + worn.id.Replace('_', '-');
            armor.displayName = DisplayName(worn.id);
            armor.slot = slot;
            armor.mesh = smr.sharedMesh;
            armor.materials = smr.sharedMaterials;
            armor.boneNames = SkeletonRebinder.ExtractBoneNames(smr);
            ApplyStats(armor, worn.id);
            EditorUtility.SetDirty(armor);
            Debug.Log($"[AssetLib] worn '{worn.id}' → {armor.name} (slot={slot}, bones={armor.boneNames.Length}).");
            return true;
        }

        // ── Static (socket) armor: helmet ─────────────────────────────────

        private static bool BuildStaticArmor(NorthStarManifest.SocketGear gear)
        {
            if (!WornSlotMap.TryGetValue(gear.slot, out EquipmentSlot slot))
            {
                Debug.LogError($"[AssetLib] '{gear.id}': unknown socket-armor slot '{gear.slot}'.");
                return false;
            }

            if (!TryGetStaticMesh(gear, out Mesh mesh, out Material[] materials)) return false;

            ArmorData armor = LoadOrCreate<ArmorData>(ArmorDir, AssetName(gear.id));
            if (string.IsNullOrEmpty(armor.itemId)) armor.itemId = "armor-" + gear.id.Replace('_', '-');
            armor.displayName = DisplayName(gear.id);
            armor.slot = slot;
            armor.mesh = mesh;
            armor.materials = materials;
            armor.boneNames = System.Array.Empty<string>(); // static socket mesh — legacy swap, placed by its renderer's bone parent
            ApplyStats(armor, gear.id);
            EditorUtility.SetDirty(armor);
            Debug.Log($"[AssetLib] socket '{gear.id}' → {armor.name} (slot={slot}, static).");
            return true;
        }

        // ── Hair ──────────────────────────────────────────────────────────

        private static bool BuildHair(NorthStarManifest.SocketGear gear)
        {
            if (!TryGetStaticMesh(gear, out Mesh mesh, out Material[] _)) return false;

            HairStyleData hair = LoadOrCreate<HairStyleData>(HairDir, AssetName(gear.id));
            if (string.IsNullOrEmpty(hair.styleId)) hair.styleId = gear.id.Replace('_', '-');
            if (string.IsNullOrEmpty(hair.displayName)) hair.displayName = DisplayName(gear.id);
            hair.mesh = mesh;
            hair.boneNames = System.Array.Empty<string>(); // static socket mesh — rendered on the head bone, no rebind
            if (hair.availableColors == null || hair.availableColors.Length == 0)
                hair.availableColors = DefaultHairColors;
            EditorUtility.SetDirty(hair);
            Debug.Log($"[AssetLib] hair '{gear.id}' → {hair.name}.");
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static bool TryGetStaticMesh(NorthStarManifest.SocketGear gear, out Mesh mesh, out Material[] materials)
        {
            mesh = null;
            materials = null;
            string fbxPath = NorthStarManifest.FbxAssetPath(gear.fbx);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (source == null)
            {
                Debug.LogError($"[AssetLib] '{gear.id}': FBX not imported at '{fbxPath}' (Git LFS pulled?).");
                return false;
            }

            // Socket gear is exported static (MeshFilter), but tolerate a skinned export too.
            MeshFilter filter = source.GetComponentInChildren<MeshFilter>(true);
            if (filter != null && filter.sharedMesh != null)
            {
                mesh = filter.sharedMesh;
                var mr = filter.GetComponent<MeshRenderer>();
                materials = mr != null ? mr.sharedMaterials : null;
                return true;
            }
            SkinnedMeshRenderer smr = source.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null)
            {
                mesh = smr.sharedMesh;
                materials = smr.sharedMaterials;
                return true;
            }
            Debug.LogError($"[AssetLib] '{gear.id}': no mesh found in '{fbxPath}'.");
            return false;
        }

        private static T LoadOrCreate<T>(string dir, string assetName) where T : ScriptableObject
        {
            string path = $"{dir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(dir);
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void ApplyStats(ArmorData armor, string id)
        {
            if (!StatMap.TryGetValue(id, out (int defense, int weight) stats)) return;
            armor.defenseBonus = stats.defense;
            armor.weightClass = stats.weight;
        }

        private static string AssetName(string id)
        {
            if (AssetNameMap.TryGetValue(id, out string mapped)) return mapped;
            string[] parts = id.Split('_');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return "SO_Armor_" + string.Concat(parts);
        }

        private static string DisplayName(string id) =>
            DisplayNameMap.TryGetValue(id, out string name) ? name : id.Replace('_', ' ');
    }
}
#endif
