using UnityEngine;

[ExecuteAlways]
public class DesertEnvironmentController : MonoBehaviour
{
    [Header("Time")]
    [Range(0f, 24f)]
    public float timeOfDay = 12f;
    public bool animateTime;
    public float dayLengthMinutes = 10f;

    [Header("Weather")]
    [Range(0f, 50f)]
    public float rainIntensity;
    public bool rainKillsHeatImmediately = true;
    public ParticleSystem rainParticleSystem;
    public float maxRainEmissionRate = 500f;
    public WetSurfaceController wetSurfaceController;

    [Header("Scheduled Rain")]
    public bool useScheduledRain;
    [Range(0f, 24f)]
    public float rainStartTimeOfDay = 14f;
    [Range(0f, 24f)]
    public float rainEndTimeOfDay = 16f;
    [Range(0f, 50f)]
    public float scheduledRainIntensity = 50f;
    public float scheduledRainTransitionSpeed = 0.5f;

    [Header("Random Rain")]
    public bool randomizeRain;
    [Range(0f, 100f)]
    public float rainChance = 0.35f;
    public Vector2 rainHoldSeconds = new Vector2(20f, 90f);
    public Vector2 dryHoldSeconds = new Vector2(30f, 120f);
    public Vector2 randomRainIntensityRange = new Vector2(0.35f, 1f);
    public float rainTransitionSpeed = 0.35f;

    private float targetRainIntensity;
    private float nextRainDecisionTime;

    [Header("Lighting")]
    public Light sun;
    public Gradient sunColor;
    public AnimationCurve sunIntensityByTime = AnimationCurve.EaseInOut(0f, 0f, 24f, 0f);
    [Tooltip("HDRP directional-light intensity in lux.")]
    public float maxSunIntensity = 15000f;
    [Range(1500f, 20000f)]
    public float sunriseSunsetTemperature = 2800f;
    [Range(1500f, 20000f)]
    public float middaySunTemperature = 5600f;
    [Range(0f, 1f)]
    public float sunShadowStrength = 0.55f;
    public float nightAmbientIntensity = 0.08f;
    public float dayAmbientIntensity = 1f;

    [Header("Heat Haze")]
    public Material heatHazeMaterial;
    [Range(0f, 1f)]
    public float dayHeatIntensity = 1f;
    [Range(0f, 1f)]
    public float nightHeatIntensity = 0.1f;
    public AnimationCurve heatByTime = AnimationCurve.EaseInOut(0f, 0.05f, 24f, 0.05f);

    public float CurrentHeatIntensity { get; private set; }
    public float CurrentTemperatureCelsius { get; private set; }
    public float DayFactor { get; private set; }

    [Header("Air Temperature")]
    public float nightTemperatureCelsius = 18f;
    public float dayTemperatureCelsius = 46f;

    private static readonly int GlobalHeatIntensityId = Shader.PropertyToID("_GlobalHeatIntensity");

    private void Reset()
    {
        sunColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.35f, 0.45f, 0.75f), 0f),
                new GradientColorKey(new Color(1f, 0.62f, 0.35f), 0.25f),
                new GradientColorKey(new Color(1f, 0.95f, 0.72f), 0.5f),
                new GradientColorKey(new Color(1f, 0.48f, 0.28f), 0.75f),
                new GradientColorKey(new Color(0.35f, 0.45f, 0.75f), 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };

        sunIntensityByTime = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(6f, 0.1f),
            new Keyframe(12f, 1f),
            new Keyframe(18f, 0.1f),
            new Keyframe(24f, 0f));

        heatByTime = new AnimationCurve(
            new Keyframe(0f, nightHeatIntensity),
            new Keyframe(7f, 0.25f),
            new Keyframe(13f, dayHeatIntensity),
            new Keyframe(18f, 0.35f),
            new Keyframe(24f, nightHeatIntensity));
    }

    private void Update()
    {
        if (animateTime && Application.isPlaying)
        {
            float hoursPerSecond = 24f / Mathf.Max(0.01f, dayLengthMinutes * 60f);
            timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime * hoursPerSecond, 24f);
        }

        UpdateRainControl();

        ApplyEnvironment();
    }

    private void OnValidate()
    {
        dayLengthMinutes = Mathf.Max(0.01f, dayLengthMinutes);
        rainHoldSeconds = SanitizeRange(rainHoldSeconds, 0.1f);
        dryHoldSeconds = SanitizeRange(dryHoldSeconds, 0.1f);
        randomRainIntensityRange = SanitizeRange(randomRainIntensityRange, 0f);
        randomRainIntensityRange.x = Mathf.Clamp01(randomRainIntensityRange.x);
        randomRainIntensityRange.y = Mathf.Clamp01(randomRainIntensityRange.y);
        rainTransitionSpeed = Mathf.Max(0.01f, rainTransitionSpeed);
        scheduledRainTransitionSpeed = Mathf.Max(0.01f, scheduledRainTransitionSpeed);
        ApplyEnvironment();
    }

    public void ForceRain(float intensity)
    {
        targetRainIntensity = Mathf.Clamp01(intensity);
        rainIntensity = targetRainIntensity;
        ScheduleNextRainDecision(targetRainIntensity > 0.01f);
        ApplyEnvironment();
    }

    public void ApplyEnvironment()
    {
        float normalizedTime = Mathf.Repeat(timeOfDay, 24f) / 24f;
        DayFactor = Mathf.Clamp01(sunIntensityByTime.Evaluate(timeOfDay));

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler((normalizedTime * 360f) - 90f, 170f, 0f);
            sun.intensity = maxSunIntensity * DayFactor * (1f - rainIntensity * 0.65f);
            sun.useColorTemperature = true;
            sun.colorTemperature = Mathf.Lerp(
                sunriseSunsetTemperature,
                middaySunTemperature,
                Mathf.SmoothStep(0f, 1f, DayFactor));
            sun.color = Color.white;
            sun.shadowStrength = sunShadowStrength;
        }

        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, DayFactor);

        float timeHeat = Mathf.Clamp01(heatByTime.Evaluate(timeOfDay));
        float rainFactor = rainKillsHeatImmediately && rainIntensity > 0.01f ? 0f : 1f - rainIntensity;
        CurrentTemperatureCelsius = Mathf.Lerp(
            nightTemperatureCelsius,
            dayTemperatureCelsius,
            timeHeat);
        CurrentHeatIntensity = Mathf.Clamp01(
            Mathf.InverseLerp(22f, dayTemperatureCelsius, CurrentTemperatureCelsius) * rainFactor);

        if (heatHazeMaterial != null)
        {
            heatHazeMaterial.SetFloat(GlobalHeatIntensityId, CurrentHeatIntensity);
        }

        if (rainParticleSystem != null)
        {
            var emission = rainParticleSystem.emission;

            if (rainIntensity > 0.01f)
            {
                // Nếu Particle đang tắt thì bật lên
                if (!rainParticleSystem.isPlaying) rainParticleSystem.Play();

                // Tăng giảm số lượng hạt mưa dựa theo độ mạnh/yếu của Rain Intensity
                emission.rateOverTime = rainIntensity * maxRainEmissionRate;
            }
            else
            {
                // Nếu intensity bằng 0 thì dừng phun hạt mới
                if (rainParticleSystem.isPlaying) rainParticleSystem.Stop();
            }
        }

        if (wetSurfaceController != null)
        {
            wetSurfaceController.ApplyWetness(rainIntensity);
        }
    }

    private void UpdateRainControl()
    {
        if (useScheduledRain)
        {
            UpdateScheduledRain();
            return;
        }

        UpdateRandomRain();
    }

    private void UpdateScheduledRain()
    {
        float desiredRainIntensity = IsInsideScheduledRainWindow(timeOfDay) ? scheduledRainIntensity : 0f;

        if (Application.isPlaying)
        {
            rainIntensity = Mathf.MoveTowards(
                rainIntensity,
                desiredRainIntensity,
                scheduledRainTransitionSpeed * Time.deltaTime);
        }
        else
        {
            rainIntensity = desiredRainIntensity;
        }

        targetRainIntensity = rainIntensity;
    }

    private bool IsInsideScheduledRainWindow(float hour)
    {
        float start = Mathf.Repeat(rainStartTimeOfDay, 24f);
        float end = Mathf.Repeat(rainEndTimeOfDay, 24f);
        hour = Mathf.Repeat(hour, 24f);

        if (Mathf.Approximately(start, end))
        {
            return true;
        }

        if (start < end)
        {
            return hour >= start && hour < end;
        }

        return hour >= start || hour < end;
    }

    private void UpdateRandomRain()
    {
        if (!randomizeRain || !Application.isPlaying)
        {
            targetRainIntensity = rainIntensity;
            return;
        }

        if (Time.time >= nextRainDecisionTime)
        {
            bool shouldRain = Random.value < rainChance;
            targetRainIntensity = shouldRain ? Random.Range(randomRainIntensityRange.x, randomRainIntensityRange.y) : 0f;
            ScheduleNextRainDecision(shouldRain);
        }

        rainIntensity = Mathf.MoveTowards(rainIntensity, targetRainIntensity, rainTransitionSpeed * Time.deltaTime);
    }

    private void ScheduleNextRainDecision(bool raining)
    {
        Vector2 holdRange = raining ? rainHoldSeconds : dryHoldSeconds;
        nextRainDecisionTime = Time.time + Random.Range(holdRange.x, holdRange.y);
    }

    private static Vector2 SanitizeRange(Vector2 range, float minimum)
    {
        range.x = Mathf.Max(minimum, range.x);
        range.y = Mathf.Max(minimum, range.y);
        if (range.y < range.x)
        {
            range.y = range.x;
        }

        return range;
    }
}
