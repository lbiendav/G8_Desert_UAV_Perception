using System;
using UnityEngine;

[ExecuteAlways]
public class WetSurfaceController : MonoBehaviour
{
    [Serializable]
    public class WetMaterialTarget
    {
        public Material material;

        [Range(0f, 1f)]
        public float drySmoothness = 0.25f;
        [Range(0f, 1f)]
        public float wetSmoothness = 1f;

        [Range(0f, 1f)]
        public float dryRoughness = 0.75f;
        [Range(0f, 1f)]
        public float wetRoughness = 0f;
    }

    [Serializable]
    public class WetTerrainLayerTarget
    {
        public TerrainLayer terrainLayer;

        [Range(0f, 1f)]
        public float drySmoothness = 0.15f;
        [Range(0f, 1f)]
        public float wetSmoothness = 1f;
    }

    [Header("Wetness")]
    [Range(0f, 1f)]
    public float wetness;

    [Header("Terrain PBR Layers")]
    public Terrain[] terrains;
    public WetTerrainLayerTarget[] terrainLayers;

    [Header("Static Infrastructure Materials")]
    public WetMaterialTarget[] staticMaterials;

    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int RoughnessId = Shader.PropertyToID("_Roughness");

    private void Reset()
    {
        terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        RebuildTerrainLayerTargetsFromTerrains();
    }

    private void OnValidate()
    {
        ApplyWetness(wetness);
    }

    public void ApplyWetness(float targetWetness)
    {
        wetness = Mathf.Clamp01(targetWetness);
        ApplyTerrainWetness();
        ApplyMaterialWetness();
    }

    [ContextMenu("Rebuild Terrain Layer Targets")]
    public void RebuildTerrainLayerTargetsFromTerrains()
    {
        if (terrains == null || terrains.Length == 0)
        {
            terrainLayers = Array.Empty<WetTerrainLayerTarget>();
            return;
        }

        int count = 0;
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] != null && terrains[i].terrainData != null)
            {
                count += terrains[i].terrainData.terrainLayers.Length;
            }
        }

        terrainLayers = new WetTerrainLayerTarget[count];
        int index = 0;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            TerrainLayer[] layers = terrain.terrainData.terrainLayers;
            for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                TerrainLayer layer = layers[layerIndex];
                terrainLayers[index] = new WetTerrainLayerTarget
                {
                    terrainLayer = layer,
                    drySmoothness = layer != null ? layer.smoothness : 0.15f,
                    wetSmoothness = 1f
                };
                index++;
            }
        }
    }

    private void ApplyTerrainWetness()
    {
        if (terrainLayers == null)
        {
            return;
        }

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            WetTerrainLayerTarget target = terrainLayers[i];
            if (target == null || target.terrainLayer == null)
            {
                continue;
            }

            target.terrainLayer.smoothness = Mathf.Lerp(target.drySmoothness, target.wetSmoothness, wetness);
        }
    }

    private void ApplyMaterialWetness()
    {
        if (staticMaterials == null)
        {
            return;
        }

        for (int i = 0; i < staticMaterials.Length; i++)
        {
            WetMaterialTarget target = staticMaterials[i];
            if (target == null || target.material == null)
            {
                continue;
            }

            float smoothness = Mathf.Lerp(target.drySmoothness, target.wetSmoothness, wetness);
            float roughness = Mathf.Lerp(target.dryRoughness, target.wetRoughness, wetness);
            ApplyFloatIfPresent(target.material, GlossinessId, smoothness);
            ApplyFloatIfPresent(target.material, SmoothnessId, smoothness);
            ApplyFloatIfPresent(target.material, RoughnessId, roughness);
        }
    }

    private static void ApplyFloatIfPresent(Material material, int propertyId, float value)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetFloat(propertyId, value);
        }
    }
}
