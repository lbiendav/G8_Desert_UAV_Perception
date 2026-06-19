using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainThermalEmissionOverlay : MonoBehaviour
{
    [Header("Source")]
    public Terrain terrain;
    public ThermalGroundProfile thermalProfile;

    [Header("Mesh")]
    [Range(32, 256)]
    public int resolution = 128;
    public float heightOffset = 0.18f;

    [Header("Emission")]
    [Range(0f, 1f)]
    public float overlayAlpha = 0.45f;
    public float emissionMultiplier = 1.5f;
    public Shader overlayShader;
    public bool generateOnStart = true;
    public bool updateInEditor = true;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private static readonly int GlobalHeatIntensityId = Shader.PropertyToID("_GlobalHeatIntensity");
    private static readonly int EmissionMultiplierId = Shader.PropertyToID("_EmissionMultiplier");

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    private void Update()
    {
        CacheComponents();
        if (meshRenderer != null)
        {
            // The overlay is sensor data, not part of the visible editor scene.
            // Runtime camera culling decides whether EO/IR cameras can see it.
            meshRenderer.forceRenderingOff = !Application.isPlaying;
        }

        ApplyMaterialProperties();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        resolution = Mathf.Clamp(resolution, 32, 256);
        heightOffset = Mathf.Max(0f, heightOffset);
        overlayAlpha = Mathf.Clamp01(overlayAlpha);
        emissionMultiplier = Mathf.Max(0f, emissionMultiplier);

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

    [ContextMenu("Generate Thermal Overlay")]
    public void Generate()
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return;
        }

        CacheComponents();
        EnsureMaterial();

        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;
        Vector3 terrainPosition = terrain.transform.position;
        int vertexCount = (resolution + 1) * (resolution + 1);

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        Color[] colors = new Color[vertexCount];

        int vertexIndex = 0;
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float nx = x / (float)resolution;
                float nz = z / (float)resolution;
                Vector3 worldPosition = new Vector3(
                    terrainPosition.x + nx * size.x,
                    terrainPosition.y,
                    terrainPosition.z + nz * size.z);

                worldPosition.y = terrain.SampleHeight(worldPosition) + terrainPosition.y + heightOffset;

                float heat = thermalProfile != null ? thermalProfile.SampleHeatAtWorldPosition(worldPosition, false) : 1f;
                Color emissionColor = thermalProfile != null
                    ? thermalProfile.EvaluateTerrainEmissionColor(worldPosition, false)
                    : Color.Lerp(new Color(0.05f, 0.08f, 0.22f), new Color(1f, 0.18f, 0.02f), heat);

                vertices[vertexIndex] = transform.InverseTransformPoint(worldPosition);
                uvs[vertexIndex] = new Vector2(nx, nz);
                colors[vertexIndex] = new Color(emissionColor.r, emissionColor.g, emissionColor.b, heat * overlayAlpha);
                vertexIndex++;
            }
        }

        List<int> triangles = new List<int>(resolution * resolution * 6);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = z * (resolution + 1) + x;
                int a = i;
                int b = i + resolution + 1;
                int c = i + 1;
                int d = i + resolution + 2;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(d);
            }
        }

        Mesh mesh = new Mesh
        {
            name = "Generated_Terrain_Thermal_Emission_Overlay",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            vertices = vertices,
            uv = uvs,
            colors = colors,
            triangles = triangles.ToArray()
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
        ApplyMaterialProperties();
    }

    private void CacheComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void EnsureMaterial()
    {
        CacheComponents();
        if (meshRenderer.sharedMaterial != null)
        {
            return;
        }

        bool usingHDRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        if (usingHDRP)
        {
            overlayShader = Shader.Find("DesertUAV/HDRP/Terrain Thermal Overlay");
        }
        else if (overlayShader == null)
        {
            overlayShader = Shader.Find("Custom/TerrainThermalEmissionOverlay_Builtin");
        }

        if (overlayShader != null)
        {
            meshRenderer.sharedMaterial = new Material(overlayShader)
            {
                name = "Runtime_Terrain_Thermal_Emission_Overlay"
            };
        }
    }

    private void ApplyMaterialProperties()
    {
        CacheComponents();
        if (meshRenderer == null || meshRenderer.sharedMaterial == null)
        {
            return;
        }

        float environmentHeat = thermalProfile != null && thermalProfile.environment != null
            ? thermalProfile.environment.CurrentHeatIntensity
            : 1f;

        meshRenderer.sharedMaterial.SetFloat(GlobalHeatIntensityId, Mathf.Clamp01(environmentHeat));
        meshRenderer.sharedMaterial.SetFloat(EmissionMultiplierId, emissionMultiplier);

        if (meshRenderer.sharedMaterial.HasProperty("_EmissiveColor"))
        {
            Color thermalColor = new Color(1f, 0.18f, 0.03f, 1f);
            meshRenderer.sharedMaterial.SetColor(
                "_EmissiveColor",
                thermalColor * Mathf.Clamp01(environmentHeat) * emissionMultiplier);
        }
    }
}
