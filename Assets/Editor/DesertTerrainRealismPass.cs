using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.IO;

public static class DesertTerrainRealismPass
{
    private const string SourcePath = "Assets/New Terrain.asset";
    private const string RealisticPath = "Assets/Settings/HDRP/RealisticDesertTerrain.asset";
    private const string VolumeProfilePath = "Assets/Settings/HDRP/DesertUAV_HDRPVolumeProfile.asset";
    private const string RefinementMarkerPath = "Assets/Settings/HDRP/RealisticTerrainRefinement.done";
    private const string SessionKey = "DesertTerrainRealismPass_20260619_v3";

    [MenuItem("Tools/Desert UAV/Apply Realistic Terrain Pass")]
    public static void Apply()
    {
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("Realistic terrain pass skipped: no Terrain was found.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<TerrainData>(RealisticPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourcePath, RealisticPath))
            {
                Debug.LogError("Could not create the realistic TerrainData backup copy.");
                return;
            }

            AssetDatabase.ImportAsset(RealisticPath, ImportAssetOptions.ForceSynchronousImport);
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(RealisticPath);
            SmoothExtremeSlopes(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        TerrainData realisticData = AssetDatabase.LoadAssetAtPath<TerrainData>(RealisticPath);
        if (!File.Exists(RefinementMarkerPath))
        {
            SmoothExtremeSlopes(realisticData, 22, 0.46f);
            File.WriteAllText(RefinementMarkerPath, "Realistic terrain refinement applied once.");
            AssetDatabase.ImportAsset(RefinementMarkerPath);
            EditorUtility.SetDirty(realisticData);
        }

        ConfigureTerrainLayers(realisticData);
        ConfigurePhysicalSky();
        terrain.terrainData = realisticData;

        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider != null)
        {
            collider.terrainData = realisticData;
        }

        EditorUtility.SetDirty(terrain);
        if (collider != null)
        {
            EditorUtility.SetDirty(collider);
        }

        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        EditorSceneManager.SaveScene(terrain.gameObject.scene);
        Debug.Log("Realistic desert terrain pass applied. Original TerrainData remains untouched.");
    }

    private static void SmoothExtremeSlopes(TerrainData data, int iterations = 10, float strength = 0.32f)
    {
        int resolution = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, resolution, resolution);
        float[,] buffer = new float[resolution, resolution];

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    buffer[y, x] = heights[y, x];
                }
            }

            for (int y = 1; y < resolution - 1; y++)
            {
                for (int x = 1; x < resolution - 1; x++)
                {
                    float center = heights[y, x];
                    float min = center;
                    float max = center;
                    float sum = 0f;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            float sample = heights[y + oy, x + ox];
                            min = Mathf.Min(min, sample);
                            max = Mathf.Max(max, sample);
                            sum += sample;
                        }
                    }

                    float localRangeWorld = (max - min) * data.size.y;
                    float slopeMask = Mathf.InverseLerp(5f, 22f, localRangeWorld);
                    float average = sum / 9f;
                    buffer[y, x] = Mathf.Lerp(center, average, slopeMask * strength);
                }
            }

            (heights, buffer) = (buffer, heights);
        }

        data.SetHeights(0, 0, heights);
    }

    private static void ConfigureTerrainLayers(TerrainData data)
    {
        TerrainLayer[] layers = data.terrainLayers;
        Vector2[] naturalTileSizes =
        {
            new Vector2(12f, 12f),
            new Vector2(18f, 18f),
            new Vector2(9f, 9f),
            new Vector2(14f, 14f)
        };

        for (int i = 0; i < layers.Length; i++)
        {
            TerrainLayer layer = layers[i];
            if (layer == null)
            {
                continue;
            }

            layer.tileSize = naturalTileSizes[Mathf.Min(i, naturalTileSizes.Length - 1)];
            layer.metallic = 0f;
            layer.smoothness = 0.08f;
            layer.normalScale = 0.65f;
            EditorUtility.SetDirty(layer);
        }
    }

    private static void ConfigurePhysicalSky()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            return;
        }

        if (!profile.TryGet(out PhysicallyBasedSky sky))
        {
            sky = profile.Add<PhysicallyBasedSky>(true);
        }

        sky.active = true;
        sky.type.Override(PhysicallyBasedSkyModel.EarthSimple);
        sky.exposure.Override(0.35f);
        sky.multiplier.Override(1f);
        sky.airTint.Override(Color.white);
        sky.aerosolTint.Override(new Color(0.92f, 0.82f, 0.68f));
        sky.groundTint.Override(new Color(0.28f, 0.2f, 0.12f));
        sky.horizonTint.Override(new Color(1f, 0.88f, 0.72f));
        sky.zenithTint.Override(new Color(0.72f, 0.84f, 1f));

        if (profile.TryGet(out VisualEnvironment environment))
        {
            environment.skyType.Override((int)SkyType.PhysicallyBased);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
        }

        EditorUtility.SetDirty(profile);
    }
}
