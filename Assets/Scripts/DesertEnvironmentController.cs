using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

[ExecuteAlways]
public class DesertEnvironmentController : MonoBehaviour
{
    [Header("Time")]
    [Range(0f, 24f)]
    public float timeOfDay = 12f;
    public bool animateTime;
    public float dayLengthMinutes = 10f;

    [Header("Weather - Rain")]
    [Range(0f, 1f)]
    public float rainIntensity;
    public bool rainKillsHeatImmediately = true;
    public ParticleSystem rainParticleSystem;
    public float maxRainEmissionRate = 500f;
    public WetSurfaceController wetSurfaceController;

    [Header("Weather - Scheduled Rain")]
    public bool useScheduledRain;
    [Range(0f, 24f)]
    public float rainStartTimeOfDay = 14f;
    [Range(0f, 24f)]
    public float rainEndTimeOfDay = 16f;
    [Range(0f, 1f)]
    public float scheduledRainIntensity = 0.7f;
    public float scheduledRainTransitionSpeed = 0.5f;

    [Header("Weather - Random Rain")]
    public bool randomizeRain;
    [Range(0f, 1f)]
    public float rainChance = 0.35f;
    public Vector2 rainHoldSeconds = new Vector2(20f, 90f);
    public Vector2 dryHoldSeconds = new Vector2(30f, 120f);
    public Vector2 randomRainIntensityRange = new Vector2(0.35f, 1f);
    public float rainTransitionSpeed = 0.35f;

    [Header("Weather - Advanced")]
    [Range(0f, 1f)]
    public float windIntensity = 0.3f;
    [Range(0f, 1f)]
    public float humidity = 0.5f;
    [Range(0f, 1f)]
    public float dustIntensity = 0.2f;
    public ParticleSystem dustParticleSystem;
    public float maxDustEmissionRate = 300f;
    public Vector3 windDirection = Vector3.forward;

    [Header("Environment Objects")]
    public bool enableEnvironmentObjects = true;
    public int treeCount = 20;
    public Vector2 treeSpawnRadius = new Vector2(50f, 200f);
    public int animalCount = 5;
    public Vector2 animalSpawnRadius = new Vector2(30f, 150f);

    [Header("Day/Night Cycle")]
    public float sunriseStartTime = 5.5f;
    public float sunriseEndTime = 7.5f;
    public float sunsetStartTime = 16.5f;
    public float sunsetEndTime = 18.5f;
    public Gradient sunColor;
    public AnimationCurve sunIntensityByTime = AnimationCurve.EaseInOut(0f, 0f, 24f, 0f);

    [Header("Lighting - Sun")]
    public Light sun;
    [Tooltip("HDRP directional-light intensity in lux.")]
    public float maxSunIntensity = 100000f;
    [Range(45f, 90f)]
    public float maxSunElevation = 90f;
    [Range(1500f, 20000f)]
    public float sunriseSunsetTemperature = 2800f;
    [Range(1500f, 20000f)]
    public float middaySunTemperature = 5400f;
    [Range(0f, 1f)]
    public float sunShadowStrength = 0.15f;

    [Header("Lighting - Fill Light")]
    public bool useTerrainFillLight = true;
    public float terrainFillIntensity = 25000f;
    public Color terrainFillColor = new Color(1f, 0.9f, 0.78f);
    public float nightFillIntensity = 8000f;

    [Header("Lighting - Ambient")]
    public float nightAmbientIntensity = 0.6f;
    public float dayAmbientIntensity = 1.2f;

    [Header("Lighting - Skylight")]
    public bool useSkylightEmulation = true;
    [Range(0f, 1f)]
    public float skylightIntensity = 0.4f;
    public Color skylightColor = new Color(0.7f, 0.8f, 1f);

    [Header("Heat & Thermal")]
    public Material heatHazeMaterial;
    [Range(0f, 1f)]
    public float dayHeatIntensity = 1f;
    [Range(0f, 1f)]
    public float nightHeatIntensity = 0.1f;
    public AnimationCurve heatByTime = AnimationCurve.EaseInOut(0f, 0.05f, 24f, 0.05f);

    [Header("Temperature")]
    public float nightTemperatureCelsius = 18f;
    public float dayTemperatureCelsius = 46f;

    [Header("Runtime State")]
    [SerializeField]
    private float currentHeatIntensity;
    [SerializeField]
    private float currentTemperatureCelsius;
    [SerializeField]
    private float currentSunlight;

    public float CurrentHeatIntensity => currentHeatIntensity;
    public float CurrentTemperatureCelsius => currentTemperatureCelsius;
    public float DayFactor => currentSunlight;

    private GameObject treeContainer;
    private GameObject animalContainer;
    private static Shader cachedStandardShader;
    private static Material cachedTreeTrunkMaterial;
    private static Material cachedTreeFoliageMaterial;
    private static Material cachedAnimalMaterial;
    private float targetRainIntensity;
    private float nextRainDecisionTime;
    private bool isValidating;

    private Light terrainFillLight;
    private Light skylightLight;
    private static readonly int GlobalHeatIntensityId = Shader.PropertyToID("_GlobalHeatIntensity");

    private void Reset()
    {
        sunColor = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.5f, 0.6f, 0.85f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.6f), 0.25f),
                new GradientColorKey(new Color(1f, 0.98f, 0.85f), 0.5f),
                new GradientColorKey(new Color(1f, 0.75f, 0.55f), 0.75f),
                new GradientColorKey(new Color(0.5f, 0.6f, 0.85f), 1f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        };

        sunIntensityByTime = new AnimationCurve(
            new Keyframe(0f, 0.05f),
            new Keyframe(6f, 0.25f),
            new Keyframe(12f, 1f),
            new Keyframe(18f, 0.25f),
            new Keyframe(24f, 0.05f));

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
        UpdateWeatherEffects();
        UpdateEnvironmentObjects();
        ApplyEnvironment();
    }

    private void OnValidate()
    {
        isValidating = true;
        dayLengthMinutes = Mathf.Max(0.01f, dayLengthMinutes);
        rainHoldSeconds = SanitizeRange(rainHoldSeconds, 0.1f);
        dryHoldSeconds = SanitizeRange(dryHoldSeconds, 0.1f);
        randomRainIntensityRange = SanitizeRange(randomRainIntensityRange, 0f);
        randomRainIntensityRange.x = Mathf.Clamp01(randomRainIntensityRange.x);
        randomRainIntensityRange.y = Mathf.Clamp01(randomRainIntensityRange.y);
        rainTransitionSpeed = Mathf.Max(0.01f, rainTransitionSpeed);
        scheduledRainTransitionSpeed = Mathf.Max(0.01f, scheduledRainTransitionSpeed);
        rainIntensity = Mathf.Clamp01(rainIntensity);
        scheduledRainIntensity = Mathf.Clamp01(scheduledRainIntensity);
        rainChance = Mathf.Clamp01(rainChance);
        maxSunElevation = Mathf.Clamp(maxSunElevation, 45f, 90f);
        terrainFillIntensity = Mathf.Max(0f, terrainFillIntensity);
        nightFillIntensity = Mathf.Max(0f, nightFillIntensity);
        windIntensity = Mathf.Clamp01(windIntensity);
        humidity = Mathf.Clamp01(humidity);
        dustIntensity = Mathf.Clamp01(dustIntensity);
        treeCount = Mathf.Max(0, treeCount);
        animalCount = Mathf.Max(0, animalCount);
        ApplyEnvironment();
        isValidating = false;
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
        if (sun != null)
        {
            CalculateSunPositionAndIntensity();
            UpdateTerrainFillLight(sun.transform.eulerAngles.y);
        }

        UpdateSkylightEmulation();
        CalculateTemperatureAndHeat();
        UpdateWeatherUI();
        
        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, dayAmbientIntensity, currentSunlight);
    }

    private void CalculateSunPositionAndIntensity()
    {
        bool isRising = timeOfDay >= sunriseStartTime && timeOfDay < sunriseEndTime;
        float sunriseFactor = isRising ? Mathf.InverseLerp(sunriseStartTime, sunriseEndTime, timeOfDay) : 0f;

        bool isSetting = timeOfDay >= sunsetStartTime && timeOfDay < sunsetEndTime;
        float sunsetFactor = isSetting ? Mathf.InverseLerp(sunsetStartTime, sunsetEndTime, timeOfDay) : 0f;

        bool isDaytime = timeOfDay >= sunriseEndTime && timeOfDay < sunsetStartTime;

        float dayLightPhase;
        if (isRising)
            dayLightPhase = sunriseFactor;
        else if (isDaytime)
            dayLightPhase = 1f;
        else if (isSetting)
            dayLightPhase = 1f - sunsetFactor;
        else
            dayLightPhase = 0f;

        currentSunlight = Mathf.Clamp01(dayLightPhase);

        float daylightProgress = Mathf.InverseLerp(sunriseEndTime, sunsetStartTime, timeOfDay);
        float solarElevation = Mathf.Sin(Mathf.Clamp01(daylightProgress) * Mathf.PI) * maxSunElevation;

        if (timeOfDay < sunriseStartTime || timeOfDay > sunsetEndTime)
        {
            solarElevation = -25f;
        }

        float solarAzimuth = Mathf.Lerp(90f, 270f, daylightProgress);

        sun.transform.rotation = Quaternion.Euler(solarElevation, solarAzimuth, 0f);

        float rainFactor = 1f - (rainIntensity * 0.65f);
        sun.intensity = maxSunIntensity * currentSunlight * rainFactor;

        sun.useColorTemperature = true;
        float sunColorFactor = 0f;
        
        if (isRising)
            sunColorFactor = sunriseFactor;
        else if (isDaytime)
            sunColorFactor = 1f;
        else if (isSetting)
            sunColorFactor = 1f - sunsetFactor;

        sun.colorTemperature = Mathf.Lerp(
            sunriseSunsetTemperature,
            middaySunTemperature,
            Mathf.SmoothStep(0f, 1f, sunColorFactor));
        
        sun.color = sunColor.Evaluate(sunColorFactor);
        sun.shadowStrength = Mathf.Lerp(0.3f, sunShadowStrength, currentSunlight);
    }

    private void CalculateTemperatureAndHeat()
    {
        float tempCurve = heatByTime.Evaluate(timeOfDay);
        
        currentTemperatureCelsius = Mathf.Lerp(
            nightTemperatureCelsius,
            dayTemperatureCelsius,
            tempCurve);

        float rainMod = rainKillsHeatImmediately && rainIntensity > 0.01f ? 0f : (1f - rainIntensity * 0.8f);
        float windMod = 1f - (windIntensity * 0.1f);
        
        float baseHeat = Mathf.InverseLerp(nightTemperatureCelsius, dayTemperatureCelsius, currentTemperatureCelsius);
        currentHeatIntensity = Mathf.Clamp01(baseHeat * rainMod * windMod);

        if (heatHazeMaterial != null)
        {
            heatHazeMaterial.SetFloat(GlobalHeatIntensityId, currentHeatIntensity);
        }

        #if UNITY_EDITOR
        if (Application.isPlaying && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Thermal] Time: {timeOfDay:F1}h | Temp: {currentTemperatureCelsius:F1}°C | Heat: {currentHeatIntensity:F2} | Rain: {rainIntensity:F2}");
        }
        #endif
    }

    private void UpdateWeatherUI()
    {
        if (rainParticleSystem != null)
        {
            var emission = rainParticleSystem.emission;

            if (rainIntensity > 0.01f)
            {
                if (!rainParticleSystem.isPlaying) rainParticleSystem.Play();
                emission.rateOverTime = rainIntensity * maxRainEmissionRate;
            }
            else
            {
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

    private void UpdateTerrainFillLight(float solarAzimuth)
    {
        if (!useTerrainFillLight)
        {
            if (terrainFillLight != null)
            {
                terrainFillLight.enabled = false;
            }
            return;
        }

        if (terrainFillLight == null)
        {
            Transform existing = transform.Find("HDRP Terrain Sky Fill");
            if (existing != null)
            {
                terrainFillLight = existing.GetComponent<Light>();
            }
        }

        if (terrainFillLight == null)
        {
            if (isValidating)
            {
                return;
            }

            GameObject fillObject = new GameObject("HDRP Terrain Sky Fill");
            fillObject.transform.SetParent(transform, false);
            terrainFillLight = fillObject.AddComponent<Light>();
            fillObject.AddComponent<HDAdditionalLightData>();
        }

        terrainFillLight.enabled = true;
        terrainFillLight.type = LightType.Directional;
        terrainFillLight.lightUnit = UnityEngine.Rendering.LightUnit.Lux;
        terrainFillLight.intensity = Mathf.Lerp(
            nightFillIntensity,
            terrainFillIntensity,
            currentSunlight);
        terrainFillLight.color = terrainFillColor;
        terrainFillLight.useColorTemperature = false;
        terrainFillLight.shadows = LightShadows.None;
        terrainFillLight.transform.rotation = Quaternion.Euler(35f, solarAzimuth + 180f, 0f);
    }

    private void UpdateWeatherEffects()
    {
        if (dustParticleSystem != null)
        {
            var emission = dustParticleSystem.emission;
            float dustLevel = dustIntensity * (1f - rainIntensity * 0.7f) * currentSunlight;

            if (dustLevel > 0.01f)
            {
                if (!dustParticleSystem.isPlaying) dustParticleSystem.Play();
                emission.rateOverTime = dustLevel * maxDustEmissionRate;
            }
            else
            {
                if (dustParticleSystem.isPlaying) dustParticleSystem.Stop();
            }
        }

        if (windDirection.sqrMagnitude < 0.001f)
        {
            windDirection = Vector3.forward;
        }
        else
        {
            windDirection.Normalize();
        }

        Shader.SetGlobalVector("_GlobalWindDirection", windDirection);
        Shader.SetGlobalFloat("_GlobalWindIntensity", windIntensity);
        Shader.SetGlobalFloat("_GlobalHumidity", humidity);
    }

    private void UpdateEnvironmentObjects()
    {
        if (!enableEnvironmentObjects || !Application.isPlaying)
        {
            return;
        }

        if (treeContainer == null)
        {
            treeContainer = transform.Find("Trees")?.gameObject;
            if (treeContainer == null)
            {
                treeContainer = new GameObject("Trees");
                treeContainer.transform.SetParent(transform, false);
                GenerateTrees();
            }
        }

        if (animalContainer == null)
        {
            animalContainer = transform.Find("Animals")?.gameObject;
            if (animalContainer == null)
            {
                animalContainer = new GameObject("Animals");
                animalContainer.transform.SetParent(transform, false);
                GenerateAnimals();
            }
        }
    }

    private void GenerateTrees()
    {
        if (treeContainer == null || treeContainer.transform.childCount > 0)
        {
            return;
        }

        if (cachedStandardShader == null)
        {
            cachedStandardShader = Shader.Find("Standard");
        }

        if (cachedTreeTrunkMaterial == null)
        {
            cachedTreeTrunkMaterial = new Material(cachedStandardShader);
            cachedTreeTrunkMaterial.color = new Color(0.5f, 0.3f, 0.1f);
        }

        if (cachedTreeFoliageMaterial == null)
        {
            cachedTreeFoliageMaterial = new Material(cachedStandardShader);
            cachedTreeFoliageMaterial.color = new Color(0.2f, 0.6f, 0.2f);
        }

        for (int i = 0; i < treeCount; i++)
        {
            float randomRadius = Random.Range(treeSpawnRadius.x, treeSpawnRadius.y);
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 spawnPos = new Vector3(
                Mathf.Cos(randomAngle) * randomRadius,
                0f,
                Mathf.Sin(randomAngle) * randomRadius
            );

            GameObject tree = CreateSimpleTree();
            tree.name = $"Tree_{i}";
            tree.transform.SetParent(treeContainer.transform, false);
            tree.transform.position = spawnPos;
        }
    }

    private GameObject CreateSimpleTree()
    {
        GameObject tree = new GameObject("SimpleTree");

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(tree.transform, false);
        trunk.transform.localScale = new Vector3(0.3f, 3f, 0.3f);
        trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        trunk.GetComponent<Renderer>().sharedMaterial = cachedTreeTrunkMaterial;

        Collider trunkCollider = trunk.GetComponent<Collider>();
        if (trunkCollider != null)
        {
            DestroyImmediate(trunkCollider);
        }

        GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage.name = "Foliage";
        foliage.transform.SetParent(tree.transform, false);
        foliage.transform.localPosition = new Vector3(0f, 3.5f, 0f);
        foliage.transform.localScale = new Vector3(2f, 2f, 2f);
        foliage.GetComponent<Renderer>().sharedMaterial = cachedTreeFoliageMaterial;

        Collider foliageCollider = foliage.GetComponent<Collider>();
        if (foliageCollider != null)
        {
            DestroyImmediate(foliageCollider);
        }

        return tree;
    }

    private void GenerateAnimals()
    {
        if (animalContainer == null || animalContainer.transform.childCount > 0)
        {
            return;
        }

        if (cachedStandardShader == null)
        {
            cachedStandardShader = Shader.Find("Standard");
        }

        if (cachedAnimalMaterial == null)
        {
            cachedAnimalMaterial = new Material(cachedStandardShader);
            cachedAnimalMaterial.color = new Color(0.8f, 0.7f, 0.6f);
        }

        for (int i = 0; i < animalCount; i++)
        {
            float randomRadius = Random.Range(animalSpawnRadius.x, animalSpawnRadius.y);
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 spawnPos = new Vector3(
                Mathf.Cos(randomAngle) * randomRadius,
                0f,
                Mathf.Sin(randomAngle) * randomRadius
            );

            GameObject animal = CreateSimpleAnimal();
            animal.name = $"Animal_{i}";
            animal.transform.SetParent(animalContainer.transform, false);
            animal.transform.position = spawnPos;
        }
    }

    private GameObject CreateSimpleAnimal()
    {
        GameObject animal = new GameObject("SimpleAnimal");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(animal.transform, false);
        body.transform.localScale = new Vector3(0.5f, 0.4f, 1f);
        body.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        body.GetComponent<Renderer>().sharedMaterial = cachedAnimalMaterial;

        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
        {
            DestroyImmediate(bodyCollider);
        }

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(animal.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.3f, 0.5f);
        head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        head.GetComponent<Renderer>().sharedMaterial = cachedAnimalMaterial;

        Collider headCollider = head.GetComponent<Collider>();
        if (headCollider != null)
        {
            DestroyImmediate(headCollider);
        }

        animal.AddComponent<AnimalWanderer>();

        return animal;
    }

    private void UpdateSkylightEmulation()
    {
        if (!useSkylightEmulation)
        {
            if (skylightLight != null)
            {
                skylightLight.enabled = false;
            }
            return;
        }

        if (skylightLight == null)
        {
            Transform existing = transform.Find("HDRP Skylight Emulation");
            if (existing != null)
            {
                skylightLight = existing.GetComponent<Light>();
            }
        }

        if (skylightLight == null)
        {
            if (isValidating)
            {
                return;
            }

            GameObject skylightObject = new GameObject("HDRP Skylight Emulation");
            skylightObject.transform.SetParent(transform, false);
            skylightLight = skylightObject.AddComponent<Light>();
            skylightObject.AddComponent<HDAdditionalLightData>();
        }

        skylightLight.enabled = true;
        skylightLight.type = LightType.Directional;
        skylightLight.lightUnit = UnityEngine.Rendering.LightUnit.Lux;
        skylightLight.intensity = Mathf.Lerp(
            nightFillIntensity * 0.5f,
            skylightIntensity * 15000f,
            currentSunlight);
        skylightLight.color = skylightColor;
        skylightLight.useColorTemperature = false;
        skylightLight.shadows = LightShadows.None;
        skylightLight.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }
}
