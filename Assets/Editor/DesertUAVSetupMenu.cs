#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DesertUAVSetupMenu
{
    [MenuItem("Tools/Desert UAV/Setup Day Night Preview")]
    public static void SetupDayNightPreview()
    {
        DesertEnvironmentController controller = Object.FindFirstObjectByType<DesertEnvironmentController>();
        if (controller == null)
        {
            GameObject environmentObject = new GameObject("DesertEnvironment");
            controller = environmentObject.AddComponent<DesertEnvironmentController>();
            Undo.RegisterCreatedObjectUndo(environmentObject, "Create Desert Environment");
        }

        Light sun = FindDirectionalLight();
        if (sun != null)
        {
            Undo.RecordObject(controller, "Assign Sun");
            controller.sun = sun;
        }

        Material heatHazeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/MAT_HeatHaze.mat");
        if (heatHazeMaterial != null)
        {
            Undo.RecordObject(controller, "Assign Heat Haze Material");
            controller.heatHazeMaterial = heatHazeMaterial;
        }

        controller.timeOfDay = 12f;
        controller.animateTime = false;
        controller.dayLengthMinutes = 2f;
        controller.rainIntensity = 0f;

        WetSurfaceController wetSurfaceController = Object.FindFirstObjectByType<WetSurfaceController>();
        if (wetSurfaceController == null)
        {
            GameObject wetSurfaceObject = new GameObject("WetSurfaceController");
            wetSurfaceController = wetSurfaceObject.AddComponent<WetSurfaceController>();
            Undo.RegisterCreatedObjectUndo(wetSurfaceObject, "Create Wet Surface Controller");
        }

        wetSurfaceController.terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        wetSurfaceController.RebuildTerrainLayerTargetsFromTerrains();
        controller.wetSurfaceController = wetSurfaceController;

        ParticleSystem rainParticleSystem = FindOrCreateRainSystem();
        controller.rainParticleSystem = rainParticleSystem;
        controller.ApplyEnvironment();

        Selection.activeObject = controller.gameObject;
        EditorGUIUtility.PingObject(controller.gameObject);
        EditorUtility.SetDirty(controller);

        Debug.Log("Day/night preview is ready. Select DesertEnvironment and adjust timeOfDay from 0 to 24.");
    }

    [MenuItem("Tools/Desert UAV/Setup Rain System")]
    public static void SetupRainSystem()
    {
        ParticleSystem rainParticleSystem = FindOrCreateRainSystem();
        DesertEnvironmentController controller = Object.FindFirstObjectByType<DesertEnvironmentController>();
        if (controller != null)
        {
            Undo.RecordObject(controller, "Assign Rain Particle System");
            controller.rainParticleSystem = rainParticleSystem;
            controller.ApplyEnvironment();
            EditorUtility.SetDirty(controller);
        }

        Selection.activeObject = rainParticleSystem.gameObject;
        EditorGUIUtility.PingObject(rainParticleSystem.gameObject);
    }

    [MenuItem("Tools/Desert UAV/Rain/Enable Random Rain")]
    public static void EnableRandomRain()
    {
        DesertEnvironmentController controller = EnsureEnvironmentController();
        Undo.RecordObject(controller, "Enable Random Rain");
        controller.randomizeRain = true;
        controller.rainChance = 0.35f;
        controller.rainHoldSeconds = new Vector2(20f, 90f);
        controller.dryHoldSeconds = new Vector2(30f, 120f);
        controller.randomRainIntensityRange = new Vector2(0.35f, 1f);
        controller.rainTransitionSpeed = 0.35f;
        EditorUtility.SetDirty(controller);
        Selection.activeObject = controller.gameObject;
    }

    [MenuItem("Tools/Desert UAV/Rain/Enable Scheduled Rain 14-16")]
    public static void EnableScheduledRainAfternoon()
    {
        DesertEnvironmentController controller = EnsureEnvironmentController();
        Undo.RecordObject(controller, "Enable Scheduled Rain");
        controller.useScheduledRain = true;
        controller.randomizeRain = false;
        controller.rainStartTimeOfDay = 14f;
        controller.rainEndTimeOfDay = 16f;
        controller.scheduledRainIntensity = 1f;
        controller.scheduledRainTransitionSpeed = 0.5f;
        controller.ApplyEnvironment();
        EditorUtility.SetDirty(controller);
        Selection.activeObject = controller.gameObject;
    }

    [MenuItem("Tools/Desert UAV/Rain/Enable Scheduled Rain 22-02")]
    public static void EnableScheduledRainOvernight()
    {
        DesertEnvironmentController controller = EnsureEnvironmentController();
        Undo.RecordObject(controller, "Enable Scheduled Rain Overnight");
        controller.useScheduledRain = true;
        controller.randomizeRain = false;
        controller.rainStartTimeOfDay = 22f;
        controller.rainEndTimeOfDay = 2f;
        controller.scheduledRainIntensity = 1f;
        controller.scheduledRainTransitionSpeed = 0.5f;
        controller.ApplyEnvironment();
        EditorUtility.SetDirty(controller);
        Selection.activeObject = controller.gameObject;
    }

    [MenuItem("Tools/Desert UAV/Rain/Force Heavy Rain")]
    public static void ForceHeavyRain()
    {
        DesertEnvironmentController controller = EnsureEnvironmentController();
        Undo.RecordObject(controller, "Force Heavy Rain");
        controller.useScheduledRain = false;
        controller.randomizeRain = false;
        controller.ForceRain(1f);
        EditorUtility.SetDirty(controller);
        Selection.activeObject = controller.gameObject;
    }

    [MenuItem("Tools/Desert UAV/Rain/Force Dry")]
    public static void ForceDry()
    {
        DesertEnvironmentController controller = EnsureEnvironmentController();
        Undo.RecordObject(controller, "Force Dry");
        controller.useScheduledRain = false;
        controller.randomizeRain = false;
        controller.ForceRain(0f);
        EditorUtility.SetDirty(controller);
        Selection.activeObject = controller.gameObject;
    }

    [MenuItem("Tools/Desert UAV/Set Time/Morning 06:00")]
    public static void SetMorning()
    {
        SetTimeOfDay(6f);
    }

    [MenuItem("Tools/Desert UAV/Set Time/Noon 12:00")]
    public static void SetNoon()
    {
        SetTimeOfDay(12f);
    }

    [MenuItem("Tools/Desert UAV/Set Time/Sunset 18:00")]
    public static void SetSunset()
    {
        SetTimeOfDay(18f);
    }

    [MenuItem("Tools/Desert UAV/Set Time/Night 00:00")]
    public static void SetNight()
    {
        SetTimeOfDay(0f);
    }

    private static void SetTimeOfDay(float hour)
    {
        DesertEnvironmentController controller = Object.FindFirstObjectByType<DesertEnvironmentController>();
        if (controller == null)
        {
            SetupDayNightPreview();
            controller = Object.FindFirstObjectByType<DesertEnvironmentController>();
        }

        if (controller == null)
        {
            Debug.LogWarning("Could not create DesertEnvironment.");
            return;
        }

        Undo.RecordObject(controller, "Set Time Of Day");
        controller.timeOfDay = hour;
        controller.ApplyEnvironment();
        EditorUtility.SetDirty(controller);
        Selection.activeObject = controller.gameObject;
    }

    private static Light FindDirectionalLight()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                return lights[i];
            }
        }

        return null;
    }

    private static DesertEnvironmentController EnsureEnvironmentController()
    {
        DesertEnvironmentController controller = Object.FindFirstObjectByType<DesertEnvironmentController>();
        if (controller == null)
        {
            SetupDayNightPreview();
            controller = Object.FindFirstObjectByType<DesertEnvironmentController>();
        }

        return controller;
    }

    private static ParticleSystem FindOrCreateRainSystem()
    {
        GameObject existing = GameObject.Find("RainSystem");
        ParticleSystem rainParticleSystem;

        if (existing == null)
        {
            existing = new GameObject("RainSystem");
            Undo.RegisterCreatedObjectUndo(existing, "Create Rain System");
            rainParticleSystem = existing.AddComponent<ParticleSystem>();
        }
        else
        {
            rainParticleSystem = existing.GetComponent<ParticleSystem>();
            if (rainParticleSystem == null)
            {
                rainParticleSystem = existing.AddComponent<ParticleSystem>();
            }
        }

        existing.transform.position = new Vector3(0f, 120f, 0f);
        ConfigureRainParticleSystem(rainParticleSystem);
        return rainParticleSystem;
    }

    private static void ConfigureRainParticleSystem(ParticleSystem rainParticleSystem)
    {
        Undo.RecordObject(rainParticleSystem, "Configure Rain Particle System");

        ParticleSystem.MainModule main = rainParticleSystem.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(45f, 70f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.065f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.65f, 0.78f, 1f, 0.22f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12000;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = rainParticleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = rainParticleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(500f, 1f, 500f);

        ParticleSystem.VelocityOverLifetimeModule velocity = rainParticleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
        velocity.y = new ParticleSystem.MinMaxCurve(-55f, -75f);
        velocity.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

        ParticleSystemRenderer renderer = rainParticleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 6f;
        renderer.velocityScale = 0.08f;
        renderer.cameraVelocityScale = 0f;
        renderer.material = GetOrCreateRainMaterial();

        EditorUtility.SetDirty(rainParticleSystem);
        EditorUtility.SetDirty(renderer);
    }

    private static Material GetOrCreateRainMaterial()
    {
        const string materialPath = "Assets/MAT_RainStreak.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/RainStreak_Builtin.shader");
        if (shader == null)
        {
            shader = Shader.Find("Custom/RainStreak_Builtin");
        }

        material = new Material(shader)
        {
            name = "MAT_RainStreak"
        };
        material.SetColor("_Color", new Color(0.65f, 0.78f, 1f, 0.35f));
        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
#endif
