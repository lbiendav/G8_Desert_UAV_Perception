using System.Collections.Generic;
using UnityEngine;

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

    [Header("Auto Update")]
    public bool generateOnStart = true;
    public bool updateInEditor = true;

    private MeshFilter meshFilter;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        resolution = Mathf.Clamp(resolution, 32, 256);
        heightOffset = Mathf.Max(0f, heightOffset);
        heightSoftness = Mathf.Max(0.01f, heightSoftness);
        slopeSoftness = Mathf.Max(0.01f, slopeSoftness);

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
            meshFilter = GetComponent<MeshFilter>();
        }

        TerrainData data = terrain.terrainData;
        Vector3 size = data.size;
        Vector3 terrainPos = terrain.transform.position;

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

                float mask = Mathf.Clamp01(heightMask * slopeMask);

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

                tris.Add(a);
                tris.Add(b);
                tris.Add(c);

                tris.Add(c);
                tris.Add(b);
                tris.Add(d);
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
}