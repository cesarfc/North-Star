#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// Typed view of the Characters-factory handoff manifest
    /// (<c>Assets/_Game/Art/Characters/northstar-manifest.json</c>): rig bones, socket
    /// definitions (parent bone + local offset), socket-mounted gear (static meshes:
    /// helmet, hair, weapons, cape) and worn gear (skinned meshes: chest/legs/feet).
    /// Editor-only — the runtime never reads the manifest; it consumes the ScriptableObjects
    /// and scenes the builders produce from it.
    /// </summary>
    [Serializable]
    public class NorthStarManifest
    {
        /// <summary>A named attach point on the shared rig (e.g. socket_head_top on head).</summary>
        [Serializable]
        public class SocketDef
        {
            public string name;
            public string parentBone;
            public float[] offset;
        }

        /// <summary>A static mesh mounted on a socket (helmet, hair, weapon, shield, cape).</summary>
        [Serializable]
        public class SocketGear
        {
            public string id;
            public string slot;
            public string socket;
            public float[] offset;
            public float[] rotationDeg;
            public float scale = 1f;
            public string[] hide;
            public string fbx;
            public bool fbxTextured;
        }

        /// <summary>A skinned mesh that co-deforms with the body (chestplate, tunic, pants, boots).</summary>
        [Serializable]
        public class WornGear
        {
            public string id;
            public string slot;
            public float[] region;
            public string skinnedProfile;
            public string fbx;
        }

        public string rig;
        public string rigVersion;
        public int packVersion;
        public string[] bones;
        public string[] bases;
        public SocketDef[] sockets = Array.Empty<SocketDef>();
        public SocketGear[] socketGear = Array.Empty<SocketGear>();
        public WornGear[] wornGear = Array.Empty<WornGear>();

        /// <summary>Project-relative path of the handoff manifest inside the imported art pack.</summary>
        public const string ManifestPath = "Assets/_Game/Art/Characters/northstar-manifest.json";

        /// <summary>Folder every <c>fbx</c> field in the manifest is relative to.</summary>
        public const string ArtRoot = "Assets/_Game/Art/Characters/";

        /// <summary>Load and parse the handoff manifest, or return <c>null</c> (with an error log)
        /// when the file is missing/unreadable so callers can fail their build step cleanly.</summary>
        public static NorthStarManifest Load()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError($"[Manifest] '{ManifestPath}' not found — import the Characters-repo FBX pack first.");
                return null;
            }
            var manifest = JsonUtility.FromJson<NorthStarManifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || string.IsNullOrEmpty(manifest.rig))
                Debug.LogError($"[Manifest] '{ManifestPath}' did not parse into a usable manifest.");
            return manifest;
        }

        /// <summary>Project-relative asset path for a manifest <c>fbx</c> entry.</summary>
        public static string FbxAssetPath(string manifestFbx) => ArtRoot + manifestFbx;

        /// <summary>Find a socket definition by name, or <c>null</c> when absent.</summary>
        public SocketDef FindSocket(string socketName)
        {
            foreach (SocketDef s in sockets)
                if (s.name == socketName)
                    return s;
            return null;
        }

        /// <summary>Convert a manifest float triplet to a Vector3 (Vector3.zero when null/short).</summary>
        public static Vector3 ToVector3(float[] triplet)
        {
            if (triplet == null || triplet.Length < 3) return Vector3.zero;
            return new Vector3(triplet[0], triplet[1], triplet[2]);
        }
    }
}
#endif
