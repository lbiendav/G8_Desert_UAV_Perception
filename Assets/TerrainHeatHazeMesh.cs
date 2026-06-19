using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainHeatHazeMesh : MonoBehaviour
{
    [Header("Target")]
    public Terrain terrain;

    [Header("Mesh Quality")]
    [Range(32, 256)]
    public int resolution = 160;

    [Header("Heat Layer")]
    public float heightOffset = 0.12f;

    [Header("Soft Ground Mask")]
    public float maxWorldHeight = 28f;
    public float heightSoftness = 12f;

    [Range(0f, 90f)]
    public float maxSlopeAngle = 45f;

    public float slopeSoftness = 20f;

    [Header("Terrain Layer Heat")]
    [Tooltip("Heat multiplier per Terrain Layer index. Example: sand high, rock medium, soil low, water zero.")]
    public float[] terrainLayerHeatMultipliers = { 1f };

    [Header("HDRP Distortion")]
    public DesertEnvironmentController environment;
    [Tooltip("HDRP distortion is measured approximately in screen pixels.")]
    [Range(0f, 32f)]
    public float distortionStrength = 3f;
    public Vector2 distortionTiling = new Vector2(7f, 7f);
    public Vector2 distortionScroll = new Vector2(0.035f, 0.11f);
    [Range(0f, 1f)]
    public float minimumTriangleHeat = 0.04f;

    [Header("Auto Update")]
    public bool generateOnStart = true;
    public bool updateInEditor = true;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int DistortionScaleId = Shader.PropertyToID("_DistortionScale");
    private static readonly int DistortionVectorMapStId = Shader.PropertyToID("_DistortionVectorMap_ST");

    void Awake()
    {
        CacheComponents();
    }

    void OnEnable()
    {
        CacheComponents();
        EnableDistortionOnAllCameras();
        ApplyDistortionProperties();
    }

    void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    void Update()
    {
        ApplyDistortionProperties();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        resolution = Mathf.Clamp(resolution, 32, 256);
        heightOffset = Mathf.Max(0f, heightOffset);
        heightSoftness = Mathf.Max(0.01f, heightSoftness);
        slopeSoftness = Mathf.Max(0.01f, slopeSoftness);
        distortionStrength = Mathf.Max(0f, distortionStrength);
        distortionTiling.x = Mathf.Max(0.01f, distortionTiling.x);
        distortionTiling.y = Mathf.Max(0.01f, distortionTiling.y);
        minimumTriangleHeat = Mathf.Clamp01(minimumTriangleHeat);

        if (!Application.isPlaying && updateInEditor)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Generate();
                }
            };
        }
    }
#endif

    public void Generate()
    {
        if (terrain == null) return;

        if (meshFilter == null)
        {
            CacheComponents();
        }

        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;
        Vector3 terrainPos = terrain.transform.position;
        float[,,] alphamaps = null;
        int alphaWidth = 0;
        int alphaHeight = 0;
        int alphaLayers = 0;

        if (data.alphamapLayers > 0)
        {
            alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            alphaWidth = data.alphamapWidth;
            alphaHeight = data.alphamapHeight;
            alphaLayers = data.alphamapLayers;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Generated_Terrain_Heat_Haze_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        int vertexCount = (resolution + 1) * (resolution + 1);

        Vector3[] verts = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        Color[] colors = new Color[vertexCount];

        int v = 0;

        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float nx = x / (float)resolution;
                float nz = z / (float)resolution;

                float worldX = terrainPos.x + nx * size.x;
                float worldZ = terrainPos.z + nz * size.z;

                float terrainHeight = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrainPos.y;
                float slope = data.GetSteepness(nx, nz);

                float heightMask = 1f - Mathf.SmoothStep(
                    maxWorldHeight - heightSoftness,
                    maxWorldHeight,
                    terrainHeight
                );

                float slopeMask = 1f - Mathf.SmoothStep(
                    maxSlopeAngle - slopeSoftness,
                    maxSlopeAngle,
                    slope
                );

                float terrainLayerHeat = SampleTerrainLayerHeat(alphamaps, alphaWidth, alphaHeight, alphaLayers, nx, nz);
                float mask = Mathf.Clamp01(heightMask * slopeMask * terrainLayerHeat);

                Vector3 worldPos = new Vector3(
                    worldX,
                    terrainHeight + heightOffset,
                    worldZ
                );

                verts[v] = transform.InverseTransformPoint(worldPos);
                uvs[v] = new Vector2(nx, nz);

                colors[v] = new Color(1f, 1f, 1f, mask);

                v++;
            }
        }

        List<int> tris = new List<int>();

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = z * (resolution + 1) + x;

                int a = i;
                int b = i + resolution + 1;
                int c = i + 1;
                int d = i + resolution + 2;

                if ((colors[a].a + colors[b].a + colors[c].a) / 3f >= minimumTriangleHeat)
                {
                    tris.Add(a);
                    tris.Add(b);
                    tris.Add(c);
                }

                if ((colors[c].a + colors[b].a + colors[d].a) / 3f >= minimumTriangleHeat)
                {
                    tris.Add(c);
                    tris.Add(b);
                    tris.Add(d);
                }
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }

    private void CacheComponents()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    private void ApplyDistortionProperties()
    {
        CacheComponents();
        if (meshRenderer == null || meshRenderer.sharedMaterial == null)
            return;

        Material material = meshRenderer.sharedMaterial;
        float heat = environment != null ? environment.CurrentHeatIntensity : 1f;
        float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        Vector2 offset = distortionScroll * time;

        propertyBlock ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(DistortionScaleId, distortionStrength * Mathf.Clamp01(heat));
        propertyBlock.SetVector(
            DistortionVectorMapStId,
            new Vector4(distortionTiling.x, distortionTiling.y, offset.x, offset.y));
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private static void EnableDistortionOnAllCameras()
    {
        foreach (HDAdditionalCameraData cameraData in
                 FindObjectsByType<HDAdditionalCameraData>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            cameraData.customRenderingSettings = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.Distortion, true);
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.RoughDistortion, true);
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.TransparentObjects, true);
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.Distortion] = true;
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.RoughDistortion] = true;
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.TransparentObjects] = true;
        }
    }

    private float SampleTerrainLayerHeat(float[,,] alphamaps, int alphaWidth, int alphaHeight, int alphaLayers, float nx, float nz)
    {
        if (alphamaps == null || alphaLayers == 0)
        {
            return 1f;
        }

        int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (alphaWidth - 1)), 0, alphaWidth - 1);
        int az = Mathf.Clamp(Mathf.RoundToInt(nz * (alphaHeight - 1)), 0, alphaHeight - 1);
        float heat = 0f;

        for (int layer = 0; layer < alphaLayers; layer++)
        {
            float multiplier = layer < terrainLayerHeatMultipliers.Length ? terrainLayerHeatMultipliers[layer] : 1f;
            heat += alphamaps[az, ax, layer] * Mathf.Max(0f, multiplier);
        }

        return Mathf.Clamp01(heat);
    }
}
