using System;
using System.Collections.Generic;
using UnityEngine;

namespace NorthStar.Character
{
    /// <summary>
    /// Pure helper for rebinding a swapped skinned mesh onto a character's shared
    /// skeleton. A <see cref="SkinnedMeshRenderer"/>'s <c>bones[i]</c> must line up with
    /// its mesh's <c>bindposes[i]</c>; swapping only <see cref="SkinnedMeshRenderer.sharedMesh"/>
    /// (as armor pieces authored against different bone orders are) leaves the old bone
    /// array in place and deforms the mesh incorrectly. This remaps each mesh bone — by
    /// name — onto the character's skeleton so one shared rig drives every equipped piece.
    ///
    /// The core <see cref="MapBones{T}"/> is engine-free and EditMode-testable; the
    /// <see cref="Rebind"/>/<see cref="BuildSkeletonMap"/>/<see cref="ExtractBoneNames"/>
    /// wrappers are the thin Unity glue.
    /// </summary>
    public static class SkeletonRebinder
    {
        /// <summary>
        /// Map an ordered list of bone names onto entries of <paramref name="skeleton"/>,
        /// preserving order so the result lines up with a mesh's bind poses. Names absent
        /// from the skeleton yield <c>null</c> at that index and increment
        /// <paramref name="missing"/>; callers should refuse to apply a result with misses.
        /// Pure (no UnityEngine dependency) so it is unit-testable without play mode.
        /// </summary>
        public static T[] MapBones<T>(
            IReadOnlyList<string> boneNames,
            IReadOnlyDictionary<string, T> skeleton,
            out int missing) where T : class
        {
            missing = 0;
            if (boneNames == null || boneNames.Count == 0) return Array.Empty<T>();

            var result = new T[boneNames.Count];
            for (int i = 0; i < boneNames.Count; i++)
            {
                string name = boneNames[i];
                if (name != null && skeleton != null && skeleton.TryGetValue(name, out T bone) && bone != null)
                    result[i] = bone;
                else
                    missing++;
            }
            return result;
        }

        /// <summary>
        /// Rebind <paramref name="renderer"/>'s bones to <paramref name="skeleton"/> by the
        /// mesh's <paramref name="boneNames"/> (bind-pose order). Applies the new bone array
        /// only when every name resolves — a partial array (with nulls) would throw during
        /// skinning, so a miss leaves the renderer untouched and returns <c>false</c>. When
        /// <paramref name="rootBone"/> is supplied it becomes the renderer's root bone.
        /// </summary>
        public static bool Rebind(
            SkinnedMeshRenderer renderer,
            IReadOnlyList<string> boneNames,
            IReadOnlyDictionary<string, Transform> skeleton,
            Transform rootBone = null)
        {
            if (renderer == null || boneNames == null || boneNames.Count == 0 || skeleton == null)
                return false;

            Transform[] bones = MapBones(boneNames, skeleton, out int missing);
            if (missing > 0) return false;

            renderer.bones = bones;
            if (rootBone != null) renderer.rootBone = rootBone;
            return true;
        }

        /// <summary>
        /// Index every <see cref="Transform"/> under <paramref name="root"/> (inclusive) by
        /// name into a lookup the rebind binds against. Built once from the character's shared
        /// armature root. On duplicate names the last one wins.
        /// </summary>
        public static Dictionary<string, Transform> BuildSkeletonMap(Transform root)
        {
            var map = new Dictionary<string, Transform>();
            if (root == null) return map;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                map[t.name] = t;
            return map;
        }

        /// <summary>
        /// Read a renderer's bones in bind-pose order as names — the value an
        /// <c>ArmorData.boneNames</c> / <c>HairStyleData.boneNames</c> field stores so the
        /// runtime can rebind the mesh later. Call on the imported FBX's renderer (e.g. from
        /// an editor importer) to populate the data.
        /// </summary>
        public static string[] ExtractBoneNames(SkinnedMeshRenderer source)
        {
            if (source == null || source.bones == null) return Array.Empty<string>();
            Transform[] bones = source.bones;
            var names = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                names[i] = bones[i] != null ? bones[i].name : null;
            return names;
        }
    }
}
