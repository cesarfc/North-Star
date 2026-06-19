using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GPU-instanced grass renderer. Scatters foliage transforms over a flat XZ area (sampling
/// terrain height when a <see cref="Terrain"/> is assigned) and draws them every frame with
/// <see cref="Graphics.DrawMeshInstanced"/> — no per-blade GameObjects, so thousands of
/// blades cost a handful of draw calls instead of thousands.
///
/// <c>DrawMeshInstanced</c> is capped at 1023 matrices per call, so the scattered transforms
/// are pre-sliced into batches of that size; each batch is one draw call. The grass material
/// must have "Enable GPU Instancing" checked. Update() is used only to issue the draw calls
/// (pure rendering, not game logic), per the CONVENTIONS exception for rendering/movement.
/// </summary>
public class ProceduralFoliage : MonoBehaviour
{
    /// <summary>Hard limit imposed by Graphics.DrawMeshInstanced.</summary>
    public const int MAX_INSTANCES_PER_BATCH = 1023;

    [Header("Instanced Asset")]
    [Tooltip("Grass blade mesh to instance.")]
    [SerializeField] private Mesh _grassMesh;

    [Tooltip("Material with 'Enable GPU Instancing' checked.")]
    [SerializeField] private Material _grassMaterial;

    [Header("Scatter Area")]
    [Tooltip("Optional terrain to sample height from. If null, foliage is placed on the XZ plane at this transform's Y.")]
    [SerializeField] private Terrain _terrain;

    [Tooltip("Half-extents of the scatter area on X and Z, in meters, centered on this transform.")]
    [SerializeField] private Vector2 _areaHalfExtents = new Vector2(25f, 25f);

    [Tooltip("Number of grass instances to scatter.")]
    [SerializeField, Min(0)] private int _instanceCount = 5000;

    [Tooltip("Deterministic seed so the layout is stable across runs.")]
    [SerializeField] private int _seed = 12345;

    [Header("Per-Blade Variation")]
    [Tooltip("Uniform scale range applied per blade.")]
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.8f, 1.3f);

    [Header("Rendering")]
    [Tooltip("Cast shadows for instanced grass (off is cheaper).")]
    [SerializeField] private UnityEngine.Rendering.ShadowCastingMode _shadowCasting =
        UnityEngine.Rendering.ShadowCastingMode.Off;

    [Tooltip("Receive shadows on instanced grass.")]
    [SerializeField] private bool _receiveShadows = true;

    // Pre-batched matrices: each inner array is <= MAX_INSTANCES_PER_BATCH.
    private readonly List<Matrix4x4[]> _batches = new List<Matrix4x4[]>();
    private bool _built;

    private void OnEnable()
    {
        Rebuild();
    }

    private void Update()
    {
        Render();
    }

    /// <summary>
    /// Regenerate the foliage layout from the current settings and re-slice it into
    /// instancing batches. Call after changing area/count/seed at runtime.
    /// </summary>
    public void Rebuild()
    {
        _batches.Clear();
        _built = false;

        if (_instanceCount <= 0 || _grassMesh == null || _grassMaterial == null)
            return;

        var matrices = BuildMatrices();
        SliceIntoBatches(matrices);
        _built = true;
    }

    /// <summary>
    /// Issue the instanced draw calls for the current batches. Safe to call when nothing is
    /// built (no-op). Normally driven by Update; exposed so a custom render loop can call it.
    /// </summary>
    public void Render()
    {
        if (!_built || _grassMesh == null || _grassMaterial == null)
            return;

        for (int i = 0; i < _batches.Count; i++)
        {
            Graphics.DrawMeshInstanced(
                _grassMesh,
                0,
                _grassMaterial,
                _batches[i],
                _batches[i].Length,
                null,
                _shadowCasting,
                _receiveShadows);
        }
    }

    /// <summary>Number of instancing batches (draw calls) the current layout produces.</summary>
    public int BatchCount => _batches.Count;

    /// <summary>
    /// Compute how many DrawMeshInstanced batches a given instance count needs, given the
    /// 1023-per-call cap. Pure helper (EditMode-testable without a scene).
    /// </summary>
    public static int BatchesNeeded(int instanceCount)
    {
        if (instanceCount <= 0) return 0;
        return (instanceCount + MAX_INSTANCES_PER_BATCH - 1) / MAX_INSTANCES_PER_BATCH;
    }

    // ── Internals ────────────────────────────────────────────────────────────────

    private Matrix4x4[] BuildMatrices()
    {
        var rng = new System.Random(_seed);
        var matrices = new Matrix4x4[_instanceCount];
        Vector3 origin = transform.position;

        for (int i = 0; i < _instanceCount; i++)
        {
            float offX = ((float)rng.NextDouble() * 2f - 1f) * _areaHalfExtents.x;
            float offZ = ((float)rng.NextDouble() * 2f - 1f) * _areaHalfExtents.y;

            float worldX = origin.x + offX;
            float worldZ = origin.z + offZ;
            float worldY = SampleHeight(worldX, worldZ, origin.y);

            float yaw = (float)rng.NextDouble() * 360f;
            float scale = Mathf.Lerp(_scaleRange.x, _scaleRange.y, (float)rng.NextDouble());

            matrices[i] = Matrix4x4.TRS(
                new Vector3(worldX, worldY, worldZ),
                Quaternion.Euler(0f, yaw, 0f),
                Vector3.one * scale);
        }

        return matrices;
    }

    private float SampleHeight(float worldX, float worldZ, float fallbackY)
    {
        if (_terrain == null) return fallbackY;
        return _terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + _terrain.transform.position.y;
    }

    private void SliceIntoBatches(Matrix4x4[] all)
    {
        for (int start = 0; start < all.Length; start += MAX_INSTANCES_PER_BATCH)
        {
            int len = Mathf.Min(MAX_INSTANCES_PER_BATCH, all.Length - start);
            var batch = new Matrix4x4[len];
            System.Array.Copy(all, start, batch, 0, len);
            _batches.Add(batch);
        }
    }
}
