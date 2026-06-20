using System.Collections.Generic;
using NorthStar.Character;
using NUnit.Framework;
using UnityEngine;

namespace NorthStar.Character.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SkeletonRebinder"/> — the bone-name remapping that lets
    /// <see cref="CharacterCustomizer"/> swap a skinned armor/hair mesh onto a shared skeleton.
    /// The pure <see cref="SkeletonRebinder.MapBones{T}"/> core is covered with plain strings
    /// (no engine state); the Transform/renderer wrappers are covered with throwaway
    /// GameObjects torn down after each test.
    /// </summary>
    public class SkeletonRebinderTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        private Transform NewBone(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go.transform;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ── MapBones (pure core) ──────────────────────────────────────────

        [Test]
        public void MapBones_PreservesOrder_AndMapsSubsetWithNoMisses()
        {
            // base skeleton has more bones than the garment uses (the verified 24 ⊃ 20 case)
            var skeleton = new Dictionary<string, string>
            {
                { "hips", "Hips" }, { "chest", "Chest" }, { "head", "Head" },
                { "fingers_l", "FingersL" }, { "thumb_r", "ThumbR" },
            };
            var boneNames = new[] { "head", "hips", "chest" };

            string[] mapped = SkeletonRebinder.MapBones(boneNames, skeleton, out int missing);

            Assert.AreEqual(0, missing);
            CollectionAssert.AreEqual(new[] { "Head", "Hips", "Chest" }, mapped);
        }

        [Test]
        public void MapBones_ReportsMissing_AndNullsTheGap()
        {
            var skeleton = new Dictionary<string, string> { { "hips", "Hips" } };
            var boneNames = new[] { "hips", "ghost_bone" };

            string[] mapped = SkeletonRebinder.MapBones(boneNames, skeleton, out int missing);

            Assert.AreEqual(1, missing);
            Assert.AreEqual("Hips", mapped[0]);
            Assert.IsNull(mapped[1]);
        }

        [Test]
        public void MapBones_NullOrEmptyInput_ReturnsEmpty()
        {
            var skeleton = new Dictionary<string, string> { { "hips", "Hips" } };

            string[] fromNull = SkeletonRebinder.MapBones((string[])null, skeleton, out int m1);
            string[] fromEmpty = SkeletonRebinder.MapBones(new string[0], skeleton, out int m2);

            Assert.AreEqual(0, fromNull.Length);
            Assert.AreEqual(0, m1);
            Assert.AreEqual(0, fromEmpty.Length);
            Assert.AreEqual(0, m2);
        }

        // ── Unity glue ────────────────────────────────────────────────────

        [Test]
        public void BuildSkeletonMap_IndexesEveryChildByName()
        {
            Transform root = NewBone("root");
            Transform hips = NewBone("hips");
            Transform chest = NewBone("chest");
            hips.SetParent(root);
            chest.SetParent(hips);

            Dictionary<string, Transform> map = SkeletonRebinder.BuildSkeletonMap(root);

            Assert.AreEqual(3, map.Count);
            Assert.AreSame(root, map["root"]);
            Assert.AreSame(chest, map["chest"]);
        }

        [Test]
        public void BuildSkeletonMap_NullRoot_ReturnsEmpty()
        {
            Assert.AreEqual(0, SkeletonRebinder.BuildSkeletonMap(null).Count);
        }

        [Test]
        public void ExtractBoneNames_ReturnsBindOrderNames()
        {
            var go = new GameObject("smr");
            _spawned.Add(go);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.bones = new[] { NewBone("hips"), NewBone("chest"), NewBone("head") };

            string[] names = SkeletonRebinder.ExtractBoneNames(smr);

            CollectionAssert.AreEqual(new[] { "hips", "chest", "head" }, names);
        }

        [Test]
        public void Rebind_AppliesMappedBones_WhenAllResolve()
        {
            var go = new GameObject("smr");
            _spawned.Add(go);
            var smr = go.AddComponent<SkinnedMeshRenderer>();

            Transform root = NewBone("root");
            var skeleton = new Dictionary<string, Transform>
            {
                { "root", root }, { "hips", NewBone("hips") }, { "chest", NewBone("chest") },
            };

            bool ok = SkeletonRebinder.Rebind(smr, new[] { "chest", "hips" }, skeleton, root);

            Assert.IsTrue(ok);
            CollectionAssert.AreEqual(new[] { skeleton["chest"], skeleton["hips"] }, smr.bones);
            Assert.AreSame(root, smr.rootBone);
        }

        [Test]
        public void Rebind_LeavesRendererUntouched_WhenABoneIsMissing()
        {
            var go = new GameObject("smr");
            _spawned.Add(go);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            Transform original = NewBone("original");
            smr.bones = new[] { original };

            var skeleton = new Dictionary<string, Transform> { { "hips", NewBone("hips") } };

            bool ok = SkeletonRebinder.Rebind(smr, new[] { "hips", "ghost_bone" }, skeleton);

            Assert.IsFalse(ok);
            CollectionAssert.AreEqual(new[] { original }, smr.bones); // unchanged
        }
    }
}
