using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class HeatNoiseCameraEffect : MonoBehaviour
{
    [Header("Environment")]
    public DesertEnvironmentController environment;
    public Transform drone;
    public Terrain terrain;

    [Header("Effect")]
    public Shader heatNoiseShader;
    [Range(0f, 1f)]
    public float opticalNoiseStrength = 0.12f;
    [Range(0f, 1f)]
    public float infraredNoiseStrength = 0.25f;
    public bool infraredCamera;

    [Header("Altitude")]
    public float fullHeatAltitude = 5f;
    public float noHeatAltitude = 120f;

    private Material material;
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int HeatAmountId = Shader.PropertyToID("_HeatAmount");
    private static readonly int TimeSeedId = Shader.PropertyToID("_TimeSeed");

    private void OnEnable()
    {
        EnsureMaterial();
    }

    private void OnDisable()
    {
        if (material != null)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        EnsureMaterial();

        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        float heat = environment != null ? environment.CurrentHeatIntensity : 1f;
        heat *= GetDroneAltitudeHeatFactor();

        float baseNoise = infraredCamera ? infraredNoiseStrength : opticalNoiseStrength;
        material.SetFloat(HeatAmountId, heat);
        material.SetFloat(NoiseStrengthId, baseNoise * heat);
        material.SetFloat(TimeSeedId, Application.isPlaying ? Time.time : Time.realtimeSinceStartup);

        Graphics.Blit(source, destination, material);
    }

    private void EnsureMaterial()
    {
        if (heatNoiseShader == null)
        {
            heatNoiseShader = Shader.Find("Hidden/HeatNoiseCameraEffect");
        }

        if (material == null && heatNoiseShader != null)
        {
            material = new Material(heatNoiseShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }

    private float GetDroneAltitudeHeatFactor()
    {
        if (drone == null)
        {
            return 1f;
        }

        float groundHeight = 0f;
        if (terrain != null)
        {
            Vector3 dronePosition = drone.position;
            groundHeight = terrain.SampleHeight(dronePosition) + terrain.transform.position.y;
        }

        float altitude = Mathf.Max(0f, drone.position.y - groundHeight);
        return 1f - Mathf.InverseLerp(fullHeatAltitude, noHeatAltitude, altitude);
    }
}
