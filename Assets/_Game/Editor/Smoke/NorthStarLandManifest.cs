#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// Typed view of the Characters-factory LAND pack manifest
    /// (<c>Assets/_Game/Art/Environment/northstar-land-manifest.json</c>): the environment
    /// prop FBX (with per-prop scatter density/scale/footprint) and the tileable ground
    /// textures with their terrain-layer roles. Editor-only — consumed by the scene builders
    /// to author terrain + prop scatter; the runtime only sees the resulting scenes.
    /// </summary>
    [Serializable]
    public class NorthStarLandManifest
    {
        /// <summary>Scatter parameters for one prop (density per 100 m², scale range).</summary>
        [Serializable]
        public class Scatter
        {
            public float countPer100m2;
            public float scaleMin = 1f;
            public float scaleMax = 1f;
        }

        /// <summary>One scatterable environment prop FBX.</summary>
        [Serializable]
        public class Prop
        {
            public string id;
            public string fbx;
            public string role;
            public float footprintRadius;
            public Scatter scatter = new Scatter();
        }

        /// <summary>One tileable ground texture and the terrain-layer role it plays.</summary>
        [Serializable]
        public class GroundTexture
        {
            public string id;
            public string file;
            public string role;
        }

        public string pack;
        public int version;
        public int seed;
        public Prop[] props = Array.Empty<Prop>();
        public GroundTexture[] textures = Array.Empty<GroundTexture>();

        /// <summary>Project-relative path of the land manifest inside the imported pack.</summary>
        public const string ManifestPath = "Assets/_Game/Art/Environment/northstar-land-manifest.json";

        /// <summary>Folder every <c>fbx</c>/<c>file</c> field in the manifest is relative to.</summary>
        public const string ArtRoot = "Assets/_Game/Art/Environment/";

        /// <summary>Load and parse the land manifest, or <c>null</c> (with an error log) when missing.</summary>
        public static NorthStarLandManifest Load()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[LandManifest] '{ManifestPath}' not found — run the factory's export-northstar-land --copy first.");
                return null;
            }
            var manifest = JsonUtility.FromJson<NorthStarLandManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.props.Length == 0)
                Debug.LogError($"[LandManifest] '{ManifestPath}' did not parse into a usable manifest.");
            return manifest;
        }

        /// <summary>Project-relative asset path for a manifest-relative file entry.</summary>
        public static string AssetPath(string manifestRelative) => ArtRoot + manifestRelative;

        /// <summary>Find a prop by id, or <c>null</c> when absent.</summary>
        public Prop FindProp(string id)
        {
            foreach (Prop p in props)
                if (p.id == id)
                    return p;
            return null;
        }
    }
}
#endif
