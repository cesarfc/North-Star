using System.Collections.Generic;
using System.IO;
using System.Linq;
using NorthStar.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// One-click assembler for the character-armor smoke test (Phases 2–3 of the pipeline,
    /// see Docs/CHARACTER_ART_PIPELINE.md). Builds <c>SCN_ArmorSmokeTest</c> from the imported
    /// FBX: instantiates the rigged body, adds a Chest <see cref="SkinnedMeshRenderer"/>, wires a
    /// <see cref="CharacterCustomizer"/> (+ <c>_skeletonRoot</c>), creates the iron-chestplate
    /// <c>ArmorData</c> (boneNames auto-filled), and a <see cref="CharacterStation"/> driver — then
    /// <b>pre-equips the chestplate at build time</b> through the real <see cref="SkeletonRebinder"/>
    /// so it is visible (and proven deformed) in the Scene view without entering Play.
    ///
    /// Logs a PASS/FAIL line with the bone-resolution counts so the rebind can be verified
    /// headlessly (<c>-executeMethod NorthStar.EditorTools.ArmorSmokeTestBuilder.Build</c>).
    /// </summary>
    public static class ArmorSmokeTestBuilder
    {
        private const string BodyModel = "base_chibi_blender_v1";
        private const string ArmorModel = "iron_chestplate"; // skinned placeholder (not _meshy)
        private const string ScenePath = "Assets/_Game/Scenes/SCN_ArmorSmokeTest.unity";
        private const string ArmorAssetPath = "Assets/_Game/ScriptableObjects/Armor/SO_Armor_IronChestplate.asset";

        [MenuItem("Tools/North-Star/Character/Build Armor Smoke Test")]
        public static void Build()
        {
            string bodyPath = FindModel(BodyModel);
            string armorPath = FindModel(ArmorModel);
            if (bodyPath == null || armorPath == null)
            {
                Debug.LogError($"[SmokeTest] FAIL — missing FBX (body='{bodyPath}', armor='{armorPath}'). " +
                               "Import base_chibi_blender_v1.fbx + iron_chestplate.fbx under Assets/_Game/Art/.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCameraAndLight();

            // ── body (shared skeleton) ──
            var bodyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath);
            var character = (GameObject)PrefabUtility.InstantiatePrefab(bodyAsset);
            character.name = "SmokeCharacter";
            character.transform.position = Vector3.zero;

            Dictionary<string, Transform> skeleton = SkeletonRebinder.BuildSkeletonMap(character.transform);

            // ── chest slot renderer ──
            var chestGO = new GameObject("Armor_Chest");
            chestGO.transform.SetParent(character.transform, false);
            var chestSMR = chestGO.AddComponent<SkinnedMeshRenderer>();

            // ── armor source mesh + materials ──
            var armorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(armorPath);
            var srcSMR = armorAsset.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (srcSMR == null || srcSMR.sharedMesh == null)
            {
                Debug.LogError($"[SmokeTest] FAIL — '{ArmorModel}' has no SkinnedMeshRenderer/mesh (is it imported as a skinned model?).");
                return;
            }
            Mesh armorMesh = srcSMR.sharedMesh;
            Material[] armorMats = srcSMR.sharedMaterials;

            // ── ArmorData (boneNames auto-filled by the importer) ──
            ArmorData armor = LoadOrCreateArmor();
            armor.itemId = "armor-iron-chestplate";
            armor.displayName = "Iron Chestplate";
            armor.slot = EquipmentSlot.Chest;
            armor.mesh = armorMesh;
            armor.materials = armorMats;
            EditorUtility.SetDirty(armor);
            CharacterBoneNameImporter.TryPopulate(armor, out int boneCount);

            // ── CharacterCustomizer (wired for runtime Equip too) ──
            var cust = character.AddComponent<CharacterCustomizer>();
            var so = new SerializedObject(cust);
            SerializedProperty arr = so.FindProperty("_armorRenderers");
            arr.arraySize = 1;
            SerializedProperty el = arr.GetArrayElementAtIndex(0);
            el.FindPropertyRelative("slot").enumValueIndex = (int)EquipmentSlot.Chest;
            el.FindPropertyRelative("renderer").objectReferenceValue = chestSMR;
            so.FindProperty("_skeletonRoot").objectReferenceValue = character.transform;
            so.ApplyModifiedProperties();

            // ── CharacterStation driver (for live Play-mode toggling) ──
            var station = new GameObject("CharacterStation").AddComponent<CharacterStation>();
            var sso = new SerializedObject(station);
            sso.FindProperty("_customizer").objectReferenceValue = cust;
            SerializedProperty armorsProp = sso.FindProperty("_armors");
            armorsProp.arraySize = 1;
            armorsProp.GetArrayElementAtIndex(0).objectReferenceValue = armor;
            sso.ApplyModifiedProperties();

            // ── pre-equip at BUILD time via the real rebind (visible without Play) ──
            chestSMR.sharedMesh = armorMesh;
            chestSMR.sharedMaterials = armorMats;
            string[] boneNames = armor.boneNames;
            bool rebound = SkeletonRebinder.Rebind(chestSMR, boneNames, skeleton, character.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            // ── verification ──
            SkeletonRebinder.MapBones(boneNames, skeleton, out int missing);
            int boundNonNull = chestSMR.bones?.Count(b => b != null) ?? 0;
            bool pass = rebound && boneNames.Length > 0 && missing == 0 && boundNonNull == boneNames.Length;
            Debug.Log(
                $"[SmokeTest] {(pass ? "PASS" : "FAIL")} — skeletonBones={skeleton.Count}, " +
                $"armor.boneNames={boneNames.Length}, resolved={boneNames.Length - missing}, missing={missing}, " +
                $"chestSMR.bones(non-null)={boundNonNull}, rebind={rebound}. Scene='{ScenePath}'.");
        }

        private static ArmorData LoadOrCreateArmor()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ArmorData>(ArmorAssetPath);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(ArmorAssetPath));
            var armor = ScriptableObject.CreateInstance<ArmorData>();
            AssetDatabase.CreateAsset(armor, ArmorAssetPath);
            return armor;
        }

        private static string FindModel(string exactName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{exactName} t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == exactName) return path;
            }
            return null;
        }

        private static void AddCameraAndLight()
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            camGO.transform.position = new Vector3(0.55f, 1.0f, 1.9f);
            camGO.transform.LookAt(new Vector3(0f, 0.82f, 0f));

            var lightGO = new GameObject("Directional Light");
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
