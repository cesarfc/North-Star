#if UNITY_EDITOR
using System.Collections.Generic;
using NorthStar.Character;
using UnityEditor;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// Assembles the fully-dressed rigged player for the slice from the character art pack:
    /// instantiates the chibi body, creates one <see cref="SkinnedMeshRenderer"/> per equipment
    /// slot (Chest/Legs/Feet on the shared skeleton; Head + hair parented to the head bone at
    /// the manifest's socket offsets), mounts the socket props (sword, shield, cape) on their
    /// hand/back sockets, wires a <see cref="CharacterCustomizer"/> to all of it, and pre-equips
    /// the starter outfit through the real <see cref="SkeletonRebinder"/> so the character is
    /// dressed in the Scene view without entering Play mode.
    /// </summary>
    public static class SliceCharacterRig
    {
        private const string BodyFbxPath = "Assets/_Game/Art/Characters/base_chibi_blender_v1.fbx";
        private const string ArmorDir = "Assets/_Game/ScriptableObjects/Armor";
        private const string HairDir = "Assets/_Game/ScriptableObjects/Hair";
        private const string GeneratedDir = "Assets/_Game/Art/Characters/Generated";

        /// <summary>
        /// Attach the dressed rig under <paramref name="player"/> and return the wired
        /// <see cref="CharacterCustomizer"/>, or <c>null</c> when the body FBX isn't imported
        /// (caller falls back to the placeholder capsule).
        /// </summary>
        public static CharacterCustomizer Attach(GameObject player, NorthStarManifest manifest)
        {
            var bodyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BodyFbxPath);
            if (bodyAsset == null || manifest == null) return null;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(bodyAsset);
            model.name = "CharacterModel";
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = new Vector3(0f, -1f, 0f); // player origin = CC centre; feet on ground
            // Bridge 2 (facing): pack FBX are exported facing -Z with bakeAxisConversion off,
            // so yaw the visual 180° to face the player root's +Z forward.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one * 1.8f;          // chibi base is 1 m; scale toward the 2 m capsule

            Dictionary<string, Transform> skeleton = SkeletonRebinder.BuildSkeletonMap(model.transform);

            // ── slot renderers ──
            SkinnedMeshRenderer chest = SlotRenderer(model.transform, "Armor_Chest");
            SkinnedMeshRenderer legs = SlotRenderer(model.transform, "Armor_Legs");
            SkinnedMeshRenderer feet = SlotRenderer(model.transform, "Armor_Feet");
            SkinnedMeshRenderer head = SocketRenderer(manifest, skeleton, model.transform, "Armor_Head", "socket_head_top", "iron_helmet");
            SkinnedMeshRenderer hair = SocketRenderer(manifest, skeleton, model.transform, "Hair", "socket_head_top", "hair_short");
            if (hair != null) hair.sharedMaterial = HairMaterial();

            // ── socket props (visual gear the customizer doesn't model yet) ──
            MountSocketProp(manifest, skeleton, "iron_sword");
            MountSocketProp(manifest, skeleton, "round_shield");
            MountSocketProp(manifest, skeleton, "green_cape");

            // ── customizer wiring ──
            var customizer = player.AddComponent<CharacterCustomizer>();
            var so = new SerializedObject(customizer);
            SerializedProperty renderers = so.FindProperty("_armorRenderers");
            var bindings = new (EquipmentSlot slot, SkinnedMeshRenderer smr)[]
            {
                (EquipmentSlot.Chest, chest),
                (EquipmentSlot.Legs, legs),
                (EquipmentSlot.Feet, feet),
                (EquipmentSlot.Head, head),
            };
            renderers.arraySize = bindings.Length;
            for (int i = 0; i < bindings.Length; i++)
            {
                SerializedProperty el = renderers.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("slot").enumValueIndex = (int)bindings[i].slot;
                el.FindPropertyRelative("renderer").objectReferenceValue = bindings[i].smr;
            }
            so.FindProperty("_hairRenderer").objectReferenceValue = hair;
            so.FindProperty("_skeletonRoot").objectReferenceValue = model.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PreEquipStarterOutfit(skeleton, model.transform, chest, legs, feet, hair);
            return customizer;
        }

        private static SkinnedMeshRenderer SlotRenderer(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<SkinnedMeshRenderer>();
        }

        /// <summary>
        /// A slot renderer for a static socket mesh (helmet/hair): parented to the socket's
        /// bone at socket offset + gear offset, so a bone-less mesh swap lands in place.
        /// </summary>
        private static SkinnedMeshRenderer SocketRenderer(
            NorthStarManifest manifest, Dictionary<string, Transform> skeleton,
            Transform fallbackParent, string name, string socketName, string gearId)
        {
            NorthStarManifest.SocketDef socket = manifest.FindSocket(socketName);
            Transform parent = fallbackParent;
            Vector3 local = Vector3.zero;
            if (socket != null && skeleton.TryGetValue(socket.parentBone, out Transform bone))
            {
                parent = bone;
                local = NorthStarManifest.ToVector3(socket.offset);
            }
            NorthStarManifest.SocketGear gear = FindGear(manifest, gearId);
            if (gear != null) local += NorthStarManifest.ToVector3(gear.offset);

            SkinnedMeshRenderer smr = SlotRenderer(parent, name);
            smr.transform.localPosition = local;
            return smr;
        }

        /// <summary>Instantiate a socket prop FBX (sword/shield/cape) on its manifest socket.</summary>
        private static void MountSocketProp(
            NorthStarManifest manifest, Dictionary<string, Transform> skeleton, string gearId)
        {
            NorthStarManifest.SocketGear gear = FindGear(manifest, gearId);
            if (gear == null) return;
            NorthStarManifest.SocketDef socket = manifest.FindSocket(gear.socket);
            if (socket == null || !skeleton.TryGetValue(socket.parentBone, out Transform bone))
            {
                Debug.LogWarning($"[CharRig] socket '{gear.socket}' for '{gearId}' not found on the rig — skipped.");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(NorthStarManifest.FbxAssetPath(gear.fbx));
            if (asset == null)
            {
                Debug.LogWarning($"[CharRig] prop FBX missing: {gear.fbx} — skipped.");
                return;
            }
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            prop.name = "Prop_" + gear.id;
            prop.transform.SetParent(bone, false);
            prop.transform.localPosition = NorthStarManifest.ToVector3(socket.offset) + NorthStarManifest.ToVector3(gear.offset);
            prop.transform.localRotation = Quaternion.Euler(NorthStarManifest.ToVector3(gear.rotationDeg));
            prop.transform.localScale = Vector3.one * (gear.scale <= 0f ? 1f : gear.scale);
        }

        /// <summary>Dress the character at build time (tunic/pants/boots + short hair) through
        /// the real rebind, so the scene opens with a dressed adventurer.</summary>
        private static void PreEquipStarterOutfit(
            Dictionary<string, Transform> skeleton, Transform skeletonRoot,
            SkinnedMeshRenderer chest, SkinnedMeshRenderer legs, SkinnedMeshRenderer feet,
            SkinnedMeshRenderer hair)
        {
            PreEquip(chest, $"{ArmorDir}/SO_Armor_ClothTunic.asset", skeleton, skeletonRoot);
            PreEquip(legs, $"{ArmorDir}/SO_Armor_LeatherPants.asset", skeleton, skeletonRoot);
            PreEquip(feet, $"{ArmorDir}/SO_Armor_LeatherBoots.asset", skeleton, skeletonRoot);

            var hairData = AssetDatabase.LoadAssetAtPath<HairStyleData>($"{HairDir}/SO_Hair_ShortCrop.asset");
            if (hair != null && hairData != null && hairData.mesh != null)
                hair.sharedMesh = hairData.mesh;
        }

        private static void PreEquip(
            SkinnedMeshRenderer renderer, string armorPath,
            Dictionary<string, Transform> skeleton, Transform skeletonRoot)
        {
            var armor = AssetDatabase.LoadAssetAtPath<ArmorData>(armorPath);
            if (renderer == null || armor == null || armor.mesh == null)
            {
                Debug.LogWarning($"[CharRig] starter piece missing ({armorPath}) — run CharacterAssetLibraryBuilder first.");
                return;
            }
            renderer.sharedMesh = armor.mesh;
            if (armor.materials != null && armor.materials.Length > 0)
                renderer.sharedMaterials = armor.materials;
            if (armor.boneNames != null && armor.boneNames.Length > 0)
                SkeletonRebinder.Rebind(renderer, armor.boneNames, skeleton, skeletonRoot);
        }

        private static NorthStarManifest.SocketGear FindGear(NorthStarManifest manifest, string gearId)
        {
            foreach (NorthStarManifest.SocketGear g in manifest.socketGear)
                if (g.id == gearId)
                    return g;
            return null;
        }

        private static Material HairMaterial()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedDir))
                AssetDatabase.CreateFolder("Assets/_Game/Art/Characters", "Generated");
            string path = GeneratedDir + "/MAT_Hair.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                // Light neutral base so the customizer's _BaseColor property-block tint reads true.
                color = new Color(0.85f, 0.8f, 0.75f),
            };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
#endif
