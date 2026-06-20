using System;
using System.Collections.Generic;
using NorthStar.Character;
using UnityEditor;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// Editor utility that fills <c>ArmorData.boneNames</c> / <c>HairStyleData.boneNames</c>
    /// from the source FBX, so <see cref="CharacterCustomizer"/> can rebind a swapped skinned
    /// mesh onto the character's shared skeleton (see <see cref="SkeletonRebinder"/>). For each
    /// asset it resolves: assigned <c>Mesh</c> → its FBX → the <see cref="SkinnedMeshRenderer"/>
    /// that uses that mesh → its bone names in bind-pose order.
    ///
    /// Runs automatically when an FBX is (re)imported, and on demand via the
    /// <b>Tools ▸ North-Star ▸ Character</b> menu or an asset's context menu (gear icon).
    /// An empty <c>boneNames</c> is the legacy sharedMesh-only swap, so this is purely additive —
    /// assets without a skinned source mesh are left untouched.
    /// </summary>
    public class CharacterBoneNameImporter : AssetPostprocessor
    {
        // FBX paths queued by a (re)import, drained on the next editor tick so we never
        // mutate/save other assets in the middle of an import.
        private static readonly HashSet<string> _pendingFbx = new HashSet<string>();

        // ── Auto-sync on FBX (re)import ────────────────────────────────────

        private static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool queued = false;
            foreach (string path in imported)
            {
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    _pendingFbx.Add(path);
                    queued = true;
                }
            }
            if (queued) EditorApplication.delayCall += FlushPending;
        }

        private static void FlushPending()
        {
            EditorApplication.delayCall -= FlushPending;
            if (_pendingFbx.Count == 0) return;
            var fbx = new HashSet<string>(_pendingFbx);
            _pendingFbx.Clear();

            int assets = 0, total = 0;
            foreach (UnityEngine.Object asset in LoadAll("t:ArmorData", "t:HairStyleData"))
            {
                if (!MeshComesFrom(asset, fbx)) continue;
                if (TryPopulate(asset, out int n)) { assets++; total += n; }
            }
            if (assets > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[boneNames] auto-synced {assets} asset(s) ({total} bones) from reimported FBX.");
            }
        }

        // ── Manual triggers ───────────────────────────────────────────────

        [MenuItem("Tools/North-Star/Character/Sync boneNames — Selection")]
        private static void SyncSelection()
        {
            int assets = 0, total = 0;
            foreach (UnityEngine.Object o in Selection.objects)
                if (TryPopulate(o, out int n)) { assets++; total += n; }
            Report(assets, total);
        }

        [MenuItem("Tools/North-Star/Character/Sync boneNames — All armor + hair")]
        private static void SyncAll()
        {
            int assets = 0, total = 0;
            foreach (UnityEngine.Object o in LoadAll("t:ArmorData", "t:HairStyleData"))
                if (TryPopulate(o, out int n)) { assets++; total += n; }
            Report(assets, total);
        }

        [MenuItem("CONTEXT/ArmorData/Sync boneNames from FBX")]
        [MenuItem("CONTEXT/HairStyleData/Sync boneNames from FBX")]
        private static void SyncContext(MenuCommand cmd)
        {
            if (TryPopulate(cmd.context, out int n))
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[boneNames] {cmd.context.name}: synced {n} bones.", cmd.context);
            }
            else
            {
                Debug.LogWarning(
                    $"[boneNames] {cmd.context.name}: no skinned source mesh found — left unchanged.",
                    cmd.context);
            }
        }

        // ── Core ──────────────────────────────────────────────────────────

        /// <summary>
        /// Populate <paramref name="asset"/>'s <c>boneNames</c> from its <c>mesh</c>'s source FBX.
        /// Works for any ScriptableObject exposing serialized <c>mesh</c> + <c>boneNames</c> fields
        /// (ArmorData, HairStyleData). Returns <c>true</c> and the bone count when it found a
        /// matching <see cref="SkinnedMeshRenderer"/> and wrote names; <c>false</c> (no change)
        /// otherwise.
        /// </summary>
        public static bool TryPopulate(UnityEngine.Object asset, out int count)
        {
            count = 0;
            if (asset == null) return false;

            var so = new SerializedObject(asset);
            SerializedProperty meshProp = so.FindProperty("mesh");
            SerializedProperty bonesProp = so.FindProperty("boneNames");
            if (meshProp == null || bonesProp == null || !bonesProp.isArray) return false;

            var mesh = meshProp.objectReferenceValue as Mesh;
            if (mesh == null) return false;

            string fbxPath = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(fbxPath)) return false;

            string[] names = ExtractFromFbx(fbxPath, mesh);
            if (names == null || names.Length == 0) return false;

            bonesProp.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                bonesProp.GetArrayElementAtIndex(i).stringValue = names[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            count = names.Length;
            return true;
        }

        /// <summary>Find the bind-order bone names of the renderer in <paramref name="fbxPath"/>
        /// whose mesh is <paramref name="mesh"/>, or <c>null</c> if there is no skinned match.</summary>
        private static string[] ExtractFromFbx(string fbxPath, Mesh mesh)
        {
            var root = AssetDatabase.LoadMainAssetAtPath(fbxPath) as GameObject;
            if (root == null) return null;
            foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh == mesh)
                    return SkeletonRebinder.ExtractBoneNames(smr);
            return null;
        }

        private static bool MeshComesFrom(UnityEngine.Object asset, HashSet<string> fbxPaths)
        {
            var so = new SerializedObject(asset);
            var mesh = so.FindProperty("mesh")?.objectReferenceValue as Mesh;
            return mesh != null && fbxPaths.Contains(AssetDatabase.GetAssetPath(mesh));
        }

        private static IEnumerable<UnityEngine.Object> LoadAll(params string[] typeFilters)
        {
            var seen = new HashSet<string>();
            foreach (string filter in typeFilters)
                foreach (string guid in AssetDatabase.FindAssets(filter))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!seen.Add(path)) continue;
                    UnityEngine.Object obj = AssetDatabase.LoadMainAssetAtPath(path);
                    if (obj != null) yield return obj;
                }
        }

        private static void Report(int assets, int total)
        {
            if (assets > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[boneNames] synced {assets} asset(s), {total} bones total.");
            }
            else
            {
                Debug.LogWarning("[boneNames] no armor/hair asset with a skinned source mesh was updated.");
            }
        }
    }
}
