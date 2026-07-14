using System;
using UnityEngine;

[ExecuteAlways]
public class ThermalGroundProfile : MonoBehaviour
{
    [Serializable]
    public class TerrainLayerThermalProfile
    {
        public TerrainLayer terrainLayer;
        [Range(0f, 1f)]
        public float heat = 0.5f;
        public Color emissionColor = new Color(1f, 0.18f, 0.02f);
        public float emissionMultiplier = 1f;
    }

    [Serializable]
    public class PbrMaterialThermalProfile
    {
        public Material material;
        [Range(0f, 1f)]
        public float heat = 0.5f;
        public Color emissionColor = new Color(1f, 0.18f, 0.02f);
        public float emissionMultiplier = 1f;
    }

    [Header("Environment")]
    public DesertEnvironmentController environment;
    [Range(0f, 1f)]
    public float defaultTerrainHeat = 0.35f;
    public float globalEmissionMultiplier = 1f;
    public bool applyMaterialEmissionEveryFrame = true;

    [Header("Terrain Heat By Geographic Layer")]
    public Terrain[] terrains;
    public TerrainLayerThermalProfile[] terrainLayers;

    [Header("PBR Material Emission")]
    public PbrMaterialThermalProfile[] pbrMaterials;
    public string emissionColorProperty = "_EmissionColor";

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Reset()
    {
        terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        RebuildTerrainLayerProfilesFromTerrains();
    }

    private void Update()
    {
        if (applyMaterialEmissionEveryFrame)
        {
            ApplyMaterialEmission();
        }
    }

    private void OnValidate()
    {
        defaultTerrainHeat = Mathf.Clamp01(defaultTerrainHeat);
        globalEmissionMultiplier = Mathf.Max(0f, globalEmissionMultiplier);
        if (applyMaterialEmissionEveryFrame)
        {
            ApplyMaterialEmission();
        }
        else
        {
            ClearMaterialEmission();
        }
    }

    [ContextMenu("Rebuild Terrain Layer Profiles")]
    public void RebuildTerrainLayerProfilesFromTerrains()
    {
        if (terrains == null || terrains.Length == 0)
        {
            terrainLayers = Array.Empty<TerrainLayerThermalProfile>();
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

        terrainLayers = new TerrainLayerThermalProfile[count];
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
                terrainLayers[index] = new TerrainLayerThermalProfile
                {
                    terrainLayer = layers[layerIndex],
                    heat = defaultTerrainHeat,
                    emissionColor = Color.Lerp(new Color(0.08f, 0.12f, 0.35f), new Color(1f, 0.18f, 0.02f), defaultTerrainHeat),
                    emissionMultiplier = 1f
                };
                index++;
            }
        }
    }

    public void ApplyMaterialEmission()
    {
        if (pbrMaterials == null)
        {
            return;
        }

        float environmentHeat = GetEnvironmentHeat();
        int propertyId = emissionColorProperty == "_EmissionColor" ? EmissionColorId : Shader.PropertyToID(emissionColorProperty);

        for (int i = 0; i < pbrMaterials.Length; i++)
        {
            PbrMaterialThermalProfile target = pbrMaterials[i];
            if (target == null || target.material == null || !target.material.HasProperty(propertyId))
            {
                continue;
            }

            float heat = Mathf.Clamp01(target.heat * environmentHeat);
            Color emission = target.emissionColor * (heat * target.emissionMultiplier * globalEmissionMultiplier);
            target.material.EnableKeyword("_EMISSION");
            target.material.SetColor(propertyId, emission);
        }
    }

    [ContextMenu("Clear Thermal Material Emission")]
    public void ClearMaterialEmission()
    {
        if (pbrMaterials == null)
        {
            return;
        }

        int configuredPropertyId = emissionColorProperty == "_EmissionColor"
            ? EmissionColorId
            : Shader.PropertyToID(emissionColorProperty);
        int hdrpEmissionId = Shader.PropertyToID("_EmissiveColor");

        for (int i = 0; i < pbrMaterials.Length; i++)
        {
            Material material = pbrMaterials[i]?.material;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(configuredPropertyId))
            {
                material.SetColor(configuredPropertyId, Color.black);
            }

            if (material.HasProperty(hdrpEmissionId))
            {
                material.SetColor(hdrpEmissionId, Color.black);
            }

            material.DisableKeyword("_EMISSION");
        }
    }

    public float SampleHeatAtWorldPosition(Vector3 worldPosition, bool includeEnvironment = true)
    {
        float heat = SampleTerrainBaseHeat(worldPosition);
        if (includeEnvironment)
        {
            heat *= GetEnvironmentHeat();
        }

        return Mathf.Clamp01(heat);
    }

    public Color EvaluateTerrainEmissionColor(Vector3 worldPosition, bool includeEnvironment = true)
    {
        float heat = SampleHeatAtWorldPosition(worldPosition, includeEnvironment);
        Color color = Color.black;
        float weightSum = 0f;

        Terrain terrain = FindTerrain(worldPosition);
        if (terrain == null || terrain.terrainData == null || terrainLayers == null)
        {
            return Color.Lerp(new Color(0.05f, 0.08f, 0.22f), new Color(1f, 0.18f, 0.02f), heat);
        }

        float[] weights = SampleTerrainLayerWeights(terrain, worldPosition);
        TerrainLayer[] layers = terrain.terrainData.terrainLayers;
        for (int i = 0; i < weights.Length && i < layers.Length; i++)
        {
            TerrainLayerThermalProfile profile = FindProfile(layers[i]);
            if (profile == null)
            {
                continue;
            }

            float weightedHeat = weights[i] * profile.heat;
            color += profile.emissionColor * weightedHeat * profile.emissionMultiplier;
            weightSum += weightedHeat;
        }

        if (weightSum <= 0.0001f)
        {
            return Color.Lerp(new Color(0.05f, 0.08f, 0.22f), new Color(1f, 0.18f, 0.02f), heat);
        }

        color /= weightSum;
        return color * (includeEnvironment ? GetEnvironmentHeat() : 1f);
    }

    private float SampleTerrainBaseHeat(Vector3 worldPosition)
    {
        Terrain terrain = FindTerrain(worldPosition);
        if (terrain == null || terrain.terrainData == null)
        {
            return defaultTerrainHeat;
        }

        float[] weights = SampleTerrainLayerWeights(terrain, worldPosition);
        TerrainLayer[] layers = terrain.terrainData.terrainLayers;
        float heat = 0f;
        float weightSum = 0f;

        for (int i = 0; i < weights.Length && i < layers.Length; i++)
        {
            TerrainLayerThermalProfile profile = FindProfile(layers[i]);
            float layerHeat = profile != null ? profile.heat : defaultTerrainHeat;
            heat += weights[i] * layerHeat;
            weightSum += weights[i];
        }

        return weightSum > 0.0001f ? Mathf.Clamp01(heat / weightSum) : defaultTerrainHeat;
    }

    private Terrain FindTerrain(Vector3 worldPosition)
    {
        if (terrains == null)
        {
            return null;
        }

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            bool insideX = worldPosition.x >= terrainPosition.x && worldPosition.x <= terrainPosition.x + size.x;
            bool insideZ = worldPosition.z >= terrainPosition.z && worldPosition.z <= terrainPosition.z + size.z;
            if (insideX && insideZ)
            {
                return terrain;
            }
        }

        return null;
    }

    private float[] SampleTerrainLayerWeights(Terrain terrain, Vector3 worldPosition)
    {
        TerrainData data = terrain.terrainData;
        Vector3 local = worldPosition - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.RoundToInt((local.x / data.size.x) * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt((local.z / data.size.z) * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
        float[,,] map = data.GetAlphamaps(x, z, 1, 1);
        float[] weights = new float[data.alphamapLayers];

        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = map[0, 0, i];
        }

        return weights;
    }

    private TerrainLayerThermalProfile FindProfile(TerrainLayer terrainLayer)
    {
        if (terrainLayer == null || terrainLayers == null)
        {
            return null;
        }

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            TerrainLayerThermalProfile profile = terrainLayers[i];
            if (profile != null && profile.terrainLayer == terrainLayer)
            {
                return profile;
            }
        }

        return null;
    }

    private float GetEnvironmentHeat()
    {
        return environment != null ? Mathf.Clamp01(environment.CurrentHeatIntensity) : 1f;
    }
}
