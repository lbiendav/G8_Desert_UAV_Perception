using UnityEngine;

public class DroneThermalSignature : MonoBehaviour
{
    [Header("Environment")]
    public DesertEnvironmentController environment;
    public Terrain terrain;

    [Header("Renderers")]
    public Renderer[] droneRenderers;
    public string emissionColorProperty = "_EmissionColor";

    [Header("Altitude Heat")]
    public float fullHeatAltitude = 5f;
    public float noHeatAltitude = 120f;
    public Color coldEmission = Color.black;
    public Color hotEmission = new Color(1f, 0.18f, 0.02f);
    public float maxEmissionMultiplier = 2.5f;

    private void Reset()
    {
        droneRenderers = GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        float environmentalHeat = environment != null ? environment.CurrentHeatIntensity : 1f;
        float altitudeHeat = GetAltitudeHeatFactor();
        float heat = Mathf.Clamp01(environmentalHeat * altitudeHeat);
        Color emission = Color.Lerp(coldEmission, hotEmission * maxEmissionMultiplier, heat);

        if (droneRenderers == null)
        {
            return;
        }

        for (int i = 0; i < droneRenderers.Length; i++)
        {
            Renderer targetRenderer = droneRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            Material material = Application.isPlaying ? targetRenderer.material : targetRenderer.sharedMaterial;
            if (material == null || !material.HasProperty(emissionColorProperty))
            {
                continue;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor(emissionColorProperty, emission);
        }
    }

    private float GetAltitudeHeatFactor()
    {
        float groundHeight = 0f;
        if (terrain != null)
        {
            groundHeight = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
        }

        float altitude = Mathf.Max(0f, transform.position.y - groundHeight);
        return 1f - Mathf.InverseLerp(fullHeatAltitude, noHeatAltitude, altitude);
    }
}
