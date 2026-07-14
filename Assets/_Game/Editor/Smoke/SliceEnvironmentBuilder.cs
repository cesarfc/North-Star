#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// Authors the slice's environment from the factory land pack: a Unity <see cref="Terrain"/>
    /// (flat gameplay plateau, Perlin hills, raised bowl rim, slope/noise-splatted ground layers),
    /// deterministic prop scatter (trees/rocks/bushes from the land manifest, colliders on the
    /// big ones), and GPU-instanced grass via the Landscape module's <see cref="ProceduralFoliage"/>.
    /// All generated sub-assets (TerrainData, TerrainLayers, grass material) are saved under
    /// <c>Assets/_Game/Art/Environment/Generated</c> so scenes can reference them.
    /// </summary>
    public static class SliceEnvironmentBuilder
    {
        private const string GeneratedDir = "Assets/_Game/Art/Environment/Generated";
        private const float PlateauHeight = 2f;

        /// <summary>Everything that shapes one zone's terrain bowl.</summary>
        public struct TerrainSpec
        {
            public string name;          // Terrain object + TerrainData asset name
            public Vector2 worldCenter;  // XZ world center of the zone
            public float size;           // square terrain edge length in meters
            public float plateauRadius;  // flat gameplay area radius around worldCenter
            public int seed;             // hill-noise seed
        }

        /// <summary>
        /// Build a terrain whose plateau surface sits exactly at world Y = 0 around
        /// <c>worldCenter</c>, with hills beyond the plateau and a raised rim at the edges
        /// so the player can't walk off the world. Saves/overwrites the TerrainData asset.
        /// </summary>
        public static Terrain BuildTerrain(TerrainSpec spec, NorthStarLandManifest land)
        {
            EnsureGeneratedDir();

            int heightRes = 129;
            const float maxHeight = 20f;
            var data = new TerrainData
            {
                heightmapResolution = heightRes,
                size = new Vector3(spec.size, maxHeight, spec.size),
            };

            float half = spec.size / 2f;
            Vector3 origin = new Vector3(spec.worldCenter.x - half, -PlateauHeight, spec.worldCenter.y - half);

            var heights = new float[heightRes, heightRes];
            for (int z = 0; z < heightRes; z++)
            {
                for (int x = 0; x < heightRes; x++)
                {
                    float wx = origin.x + (x / (float)(heightRes - 1)) * spec.size;
                    float wz = origin.z + (z / (float)(heightRes - 1)) * spec.size;
                    // Note: SetHeights indexes [row, column] = [z, x].
                    heights[z, x] = SampleHeight(wx, wz, spec) / maxHeight;
                }
            }
            data.SetHeights(0, 0, heights);

            ApplyLayers(data, spec, origin, land);

            string assetPath = $"{GeneratedDir}/{spec.name}_Data.asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = spec.name;
            terrainGo.transform.position = origin;
            return terrainGo.GetComponent<Terrain>();
        }

        /// <summary>World-space terrain height (meters above the terrain origin plane).</summary>
        private static float SampleHeight(float wx, float wz, TerrainSpec spec)
        {
            float dx = wx - spec.worldCenter.x;
            float dz = wz - spec.worldCenter.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            float half = spec.size / 2f;

            float hills = PlateauHeight + Mathf.PerlinNoise(
                (wx + spec.seed * 37.7f) * 0.03f,
                (wz + spec.seed * 91.3f) * 0.03f) * 5f;

            float hillBlend = Mathf.InverseLerp(spec.plateauRadius, spec.plateauRadius + 18f, r);
            float h = Mathf.Lerp(PlateauHeight, hills, Mathf.SmoothStep(0f, 1f, hillBlend));

            // Raised rim: keep the player inside the bowl.
            float rimStart = half - 14f;
            float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rimStart, half - 2f, Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz))));
            return h + rim * 9f;
        }

        /// <summary>Create terrain layers from the land-pack textures and splat by slope + noise.</summary>
        private static void ApplyLayers(TerrainData data, TerrainSpec spec, Vector3 origin, NorthStarLandManifest land)
        {
            TerrainLayer grass = MakeLayer(land, "terrain_base", spec.name + "_Grass");
            TerrainLayer dirt = MakeLayer(land, "terrain_slope", spec.name + "_Dirt");
            TerrainLayer rock = MakeLayer(land, "terrain_cliff", spec.name + "_Rock");
            TerrainLayer path = MakeLayer(land, "terrain_path", spec.name + "_Path");
            data.terrainLayers = new[] { grass, dirt, rock, path };

            int res = data.alphamapResolution;
            var alpha = new float[res, res, 4];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)(res - 1);
                    float nz = z / (float)(res - 1);
                    float wx = origin.x + nx * spec.size;
                    float wz = origin.z + nz * spec.size;

                    float steep = data.GetSteepness(nx, nz); // degrees
                    float wRock = Mathf.InverseLerp(28f, 45f, steep);
                    float wDirt = (1f - wRock) * Mathf.InverseLerp(0.60f, 0.72f,
                        Mathf.PerlinNoise((wx + 13.7f) * 0.05f, (wz + 7.3f) * 0.05f));

                    // Path ring on the plateau connecting the stations.
                    float dx = wx - spec.worldCenter.x;
                    float dz = wz - spec.worldCenter.y;
                    float r = Mathf.Sqrt(dx * dx + dz * dz);
                    float wPath = (1f - wRock) * 0.85f * Mathf.Clamp01(1f - Mathf.Abs(r - 6.5f) / 2.2f);

                    float wGrass = Mathf.Max(0f, 1f - wRock - wDirt - wPath);
                    float sum = wGrass + wDirt + wRock + wPath;
                    alpha[z, x, 0] = wGrass / sum;
                    alpha[z, x, 1] = wDirt / sum;
                    alpha[z, x, 2] = wRock / sum;
                    alpha[z, x, 3] = wPath / sum;
                }
            }
            data.SetAlphamaps(0, 0, alpha);
        }

        private static TerrainLayer MakeLayer(NorthStarLandManifest land, string role, string assetName)
        {
            Texture2D tex = null;
            foreach (NorthStarLandManifest.GroundTexture t in land.textures)
            {
                if (t.role != role) continue;
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(NorthStarLandManifest.AssetPath(t.file));
                break;
            }
            if (tex == null)
                Debug.LogWarning($"[Env] no land-pack texture for role '{role}' — layer will be blank.");

            var layer = new TerrainLayer
            {
                diffuseTexture = tex,
                tileSize = new Vector2(4f, 4f),
            };
            string path = $"{GeneratedDir}/{assetName}_Layer.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        /// <summary>
        /// Deterministically scatter the land-pack props in the ring between the gameplay
        /// plateau and the terrain rim. Trees and large rocks get simple primitive colliders;
        /// grass is excluded (it is GPU-instanced separately via <see cref="AddGrass"/>).
        /// </summary>
        public static void ScatterProps(Terrain terrain, TerrainSpec spec, NorthStarLandManifest land, int seed)
        {
            var parent = new GameObject(spec.name + "_Props").transform;
            float innerR = spec.plateauRadius + 3f;
            float outerR = spec.size / 2f - 15f;
            float ringArea = Mathf.PI * (outerR * outerR - innerR * innerR);

            var rng = new System.Random(seed);
            int placed = 0;
            foreach (NorthStarLandManifest.Prop prop in land.props)
            {
                if (prop.scatter.countPer100m2 <= 0f) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(NorthStarLandManifest.AssetPath(prop.fbx));
                if (asset == null)
                {
                    Debug.LogWarning($"[Env] prop FBX missing: {prop.fbx} — skipped.");
                    continue;
                }

                int count = Mathf.RoundToInt(ringArea / 100f * prop.scatter.countPer100m2);
                for (int i = 0; i < count; i++)
                {
                    float u = (float)rng.NextDouble();
                    float r = Mathf.Sqrt(Mathf.Lerp(innerR * innerR, outerR * outerR, u));
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float wx = spec.worldCenter.x + Mathf.Cos(ang) * r;
                    float wz = spec.worldCenter.y + Mathf.Sin(ang) * r;
                    float wy = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + terrain.transform.position.y;

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    instance.name = $"{prop.id}_{i}";
                    instance.transform.SetParent(parent, false);
                    instance.transform.position = new Vector3(wx, wy, wz);
                    instance.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    float s = Mathf.Lerp(prop.scatter.scaleMin, prop.scatter.scaleMax, (float)rng.NextDouble());
                    instance.transform.localScale = Vector3.one * s;
                    instance.isStatic = true;

                    AddPropCollider(instance, prop, s);
                    placed++;
                }
            }
            Debug.Log($"[Env] {spec.name}: scattered {placed} props.");
        }

        private static void AddPropCollider(GameObject instance, NorthStarLandManifest.Prop prop, float scale)
        {
            switch (prop.role)
            {
                case "tree":
                    var capsule = instance.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0f, 1.5f, 0f);
                    capsule.radius = 0.3f;
                    capsule.height = 3f;
                    break;
                case "rock" when prop.footprintRadius >= 0.8f:
                    var sphere = instance.AddComponent<SphereCollider>();
                    sphere.center = new Vector3(0f, prop.footprintRadius * 0.5f, 0f);
                    sphere.radius = prop.footprintRadius * 0.85f;
                    break;
            }
        }

        /// <summary>
        /// Wire the Landscape module's GPU-instanced grass over the zone using the land pack's
        /// grass-tuft mesh and a saved instanced URP material. Returns the foliage component.
        /// </summary>
        public static ProceduralFoliage AddGrass(Terrain terrain, TerrainSpec spec, NorthStarLandManifest land, int instanceCount)
        {
            NorthStarLandManifest.Prop grassProp = land.FindProp("grass_tuft");
            Mesh grassMesh = null;
            if (grassProp != null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(NorthStarLandManifest.AssetPath(grassProp.fbx));
                if (asset != null)
                {
                    var filter = asset.GetComponentInChildren<MeshFilter>(true);
                    if (filter != null) grassMesh = filter.sharedMesh;
                }
            }
            if (grassMesh == null)
            {
                Debug.LogWarning("[Env] grass_tuft mesh not found — skipping instanced grass.");
                return null;
            }

            var go = new GameObject(spec.name + "_Grass");
            go.transform.position = new Vector3(spec.worldCenter.x, 0f, spec.worldCenter.y);
            var foliage = go.AddComponent<ProceduralFoliage>();
            var so = new SerializedObject(foliage);
            so.FindProperty("_grassMesh").objectReferenceValue = grassMesh;
            so.FindProperty("_grassMaterial").objectReferenceValue = GrassMaterial();
            so.FindProperty("_terrain").objectReferenceValue = terrain;
            so.FindProperty("_areaHalfExtents").vector2Value = Vector2.one * (spec.size / 2f - 16f);
            so.FindProperty("_instanceCount").intValue = instanceCount;
            so.FindProperty("_seed").intValue = spec.seed * 7 + 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            return foliage;
        }

        private static Material GrassMaterial()
        {
            EnsureGeneratedDir();
            string path = GeneratedDir + "/MAT_GrassBlade.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(lit)
            {
                color = new Color(0.35f, 0.62f, 0.30f),
                enableInstancing = true, // required by Graphics.DrawMeshInstanced
            };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureGeneratedDir()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedDir))
                AssetDatabase.CreateFolder("Assets/_Game/Art/Environment", "Generated");
        }
    }
}
#endif
