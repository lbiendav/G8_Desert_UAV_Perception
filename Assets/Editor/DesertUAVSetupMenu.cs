#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DesertUAVSetupMenu
{
    public static void BatchSetupThermalScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SetupGroundThermalEmission();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public static void BatchRebuildSovaStyleDrone()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        RebuildSovaStyleDrone();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

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

    [MenuItem("Tools/Desert UAV/Thermal/Setup Ground Thermal Emission")]
    public static void SetupGroundThermalEmission()
    {
        int thermalLayer = EnsureLayer("ThermalOverlay");
        DesertEnvironmentController environment = EnsureEnvironmentController();
        Terrain terrain = Object.FindFirstObjectByType<Terrain>();

        ThermalGroundProfile profile = Object.FindFirstObjectByType<ThermalGroundProfile>();
        if (profile == null)
        {
            GameObject profileObject = new GameObject("ThermalGroundProfile");
            profile = profileObject.AddComponent<ThermalGroundProfile>();
            Undo.RegisterCreatedObjectUndo(profileObject, "Create Thermal Ground Profile");
        }

        Undo.RecordObject(profile, "Configure Thermal Ground Profile");
        profile.environment = environment;
        profile.terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        profile.defaultTerrainHeat = 0.45f;
        profile.globalEmissionMultiplier = 1.8f;
        profile.RebuildTerrainLayerProfilesFromTerrains();
        ApplyDefaultTerrainLayerHeat(profile);
        profile.pbrMaterials = BuildSandMaterialProfiles();
        profile.ApplyMaterialEmission();
        EditorUtility.SetDirty(profile);

        TerrainThermalEmissionOverlay overlay = Object.FindFirstObjectByType<TerrainThermalEmissionOverlay>();
        if (overlay == null)
        {
            GameObject overlayObject = new GameObject("TerrainThermalEmissionOverlay");
            overlay = overlayObject.AddComponent<TerrainThermalEmissionOverlay>();
            Undo.RegisterCreatedObjectUndo(overlayObject, "Create Terrain Thermal Emission Overlay");
        }

        SetLayerRecursively(overlay.gameObject, thermalLayer);
        Undo.RecordObject(overlay, "Configure Terrain Thermal Emission Overlay");
        overlay.terrain = terrain;
        overlay.thermalProfile = profile;
        overlay.overlayShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/TerrainThermalEmissionOverlay_Builtin.shader");
        overlay.resolution = 128;
        overlay.overlayAlpha = 0.5f;
        overlay.emissionMultiplier = 1.7f;
        overlay.Generate();
        EditorUtility.SetDirty(overlay);

        ConfigureThermalCameraVisibility(thermalLayer);
        EditorSceneManager.MarkSceneDirty(overlay.gameObject.scene);

        Selection.activeObject = profile.gameObject;
        EditorGUIUtility.PingObject(profile.gameObject);
        Debug.Log("Ground thermal emission is ready. Thermal_IR_Cam sees ThermalOverlay; Visible_EO_Cam does not.");
    }

    [MenuItem("Tools/Desert UAV/Thermal/Apply Thermal Camera Visibility")]
    public static void ApplyThermalCameraVisibility()
    {
        int thermalLayer = EnsureLayer("ThermalOverlay");
        TerrainThermalEmissionOverlay overlay = Object.FindFirstObjectByType<TerrainThermalEmissionOverlay>();
        if (overlay != null)
        {
            SetLayerRecursively(overlay.gameObject, thermalLayer);
            EditorUtility.SetDirty(overlay.gameObject);
        }

        ConfigureThermalCameraVisibility(thermalLayer);
        if (overlay != null)
        {
            EditorSceneManager.MarkSceneDirty(overlay.gameObject.scene);
        }

        Debug.Log("Thermal camera visibility applied to all scene cameras named Thermal_IR_Cam and Visible_EO_Cam.");
    }

    [MenuItem("Tools/Desert UAV/Thermal/Create Checkpoint Drone Demo")]
    public static void CreateCheckpointDroneDemo()
    {
        SetupGroundThermalEmission();

        Terrain terrain = Object.FindFirstObjectByType<Terrain>();
        ThermalGroundProfile profile = Object.FindFirstObjectByType<ThermalGroundProfile>();
        DesertEnvironmentController environment = EnsureEnvironmentController();

        GameObject drone = GameObject.Find("Thermal_Checkpoint_Drone");
        if (drone == null)
        {
            drone = new GameObject("Thermal_Checkpoint_Drone");
            Undo.RegisterCreatedObjectUndo(drone, "Create Thermal Checkpoint Drone");
        }

        BuildSovaStyleDroneMesh(drone.transform);

        Undo.RecordObject(drone.transform, "Position Thermal Checkpoint Drone");
        drone.transform.position = GetTerrainWorldPoint(terrain, 0.35f, 0.35f, 12f);

        DroneCheckpointMover mover = drone.GetComponent<DroneCheckpointMover>();
        if (mover == null)
        {
            mover = drone.AddComponent<DroneCheckpointMover>();
        }

        DroneThermalSignature signature = drone.GetComponent<DroneThermalSignature>();
        if (signature == null)
        {
            signature = drone.AddComponent<DroneThermalSignature>();
        }

        GameObject routeRoot = GameObject.Find("Drone_Checkpoints");
        if (routeRoot == null)
        {
            routeRoot = new GameObject("Drone_Checkpoints");
            Undo.RegisterCreatedObjectUndo(routeRoot, "Create Drone Checkpoints");
        }

        Transform[] checkpoints = CreateDefaultCheckpoints(routeRoot.transform, terrain);

        Undo.RecordObject(mover, "Configure Drone Checkpoint Mover");
        mover.checkpoints = checkpoints;
        mover.terrain = terrain;
        mover.speed = 20f;
        mover.reachDistance = 2f;
        mover.minimumGroundClearance = 2f;
        mover.loopMode = DroneCheckpointMover.LoopMode.Loop;
        mover.playOnStart = true;
        EditorUtility.SetDirty(mover);

        Undo.RecordObject(signature, "Configure Drone Thermal Signature");
        signature.environment = environment;
        signature.thermalGroundProfile = profile;
        signature.terrain = terrain;
        signature.droneRenderers = drone.GetComponentsInChildren<Renderer>();
        signature.fullHeatAltitude = 5f;
        signature.noHeatAltitude = 120f;
        signature.groundHeatInfluence = 1f;
        signature.maxEmissionMultiplier = 3f;
        EditorUtility.SetDirty(signature);

        Selection.activeObject = drone;
        EditorGUIUtility.PingObject(drone);
        Debug.Log("Checkpoint drone demo is ready. Move Drone_Checkpoints children to change low/high thermal passes.");
    }

    [MenuItem("Tools/Desert UAV/Thermal/Rebuild SOVA-style Drone")]
    public static void RebuildSovaStyleDrone()
    {
        GameObject drone = GameObject.Find("Thermal_Checkpoint_Drone");
        if (drone == null)
        {
            drone = new GameObject("Thermal_Checkpoint_Drone");
            Undo.RegisterCreatedObjectUndo(drone, "Create Thermal Checkpoint Drone");
        }

        BuildSovaStyleDroneMesh(drone.transform);

        DroneThermalSignature signature = drone.GetComponent<DroneThermalSignature>();
        if (signature != null)
        {
            signature.droneRenderers = drone.GetComponentsInChildren<Renderer>();
            EditorUtility.SetDirty(signature);
        }

        EditorSceneManager.MarkSceneDirty(drone.scene);
        Selection.activeObject = drone;
        EditorGUIUtility.PingObject(drone);
        Debug.Log("SOVA-style ducted drone mesh rebuilt.");
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

    private static int EnsureLayer(string layerName)
    {
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer >= 0)
        {
            return existingLayer;
        }

        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return i;
            }
        }

        Debug.LogWarning($"Could not create layer '{layerName}'. All user layer slots are full.");
        return 0;
    }

    private static void ConfigureThermalCameraVisibility(int thermalLayer)
    {
        int thermalMask = 1 << thermalLayer;
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || EditorUtility.IsPersistent(camera))
            {
                continue;
            }

            string cameraName = camera.gameObject.name;
            bool isThermalCamera = cameraName.Contains("Thermal_IR_Cam");
            bool isVisibleCamera = cameraName.Contains("Visible_EO_Cam");

            if (!isThermalCamera && !isVisibleCamera)
            {
                continue;
            }

            Undo.RecordObject(camera, "Configure Thermal Overlay Visibility");
            if (isThermalCamera)
            {
                camera.cullingMask |= thermalMask;
            }
            else
            {
                camera.cullingMask &= ~thermalMask;
            }

            EditorUtility.SetDirty(camera);
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        Undo.RecordObject(target, "Set Thermal Overlay Layer");
        target.layer = layer;

        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }

    private static void ApplyDefaultTerrainLayerHeat(ThermalGroundProfile profile)
    {
        if (profile.terrainLayers == null)
        {
            return;
        }

        for (int i = 0; i < profile.terrainLayers.Length; i++)
        {
            ThermalGroundProfile.TerrainLayerThermalProfile target = profile.terrainLayers[i];
            if (target == null)
            {
                continue;
            }

            float heat = i switch
            {
                0 => 0.85f,
                1 => 0.65f,
                2 => 0.35f,
                _ => 0.2f
            };

            target.heat = heat;
            target.emissionColor = Color.Lerp(new Color(0.05f, 0.08f, 0.25f), new Color(1f, 0.18f, 0.02f), heat);
            target.emissionMultiplier = 1f;
        }
    }

    private static ThermalGroundProfile.PbrMaterialThermalProfile[] BuildSandMaterialProfiles()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/YughuesFreeSandMaterials/Materials" });
        List<ThermalGroundProfile.PbrMaterialThermalProfile> profiles = new List<ThermalGroundProfile.PbrMaterialThermalProfile>();

        for (int i = 0; i < materialGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                continue;
            }

            float heat = Mathf.Lerp(0.35f, 0.9f, (i % 6) / 5f);
            profiles.Add(new ThermalGroundProfile.PbrMaterialThermalProfile
            {
                material = material,
                heat = heat,
                emissionColor = Color.Lerp(new Color(0.05f, 0.08f, 0.25f), new Color(1f, 0.18f, 0.02f), heat),
                emissionMultiplier = 1f
            });
        }

        return profiles.ToArray();
    }

    private static void BuildSovaStyleDroneMesh(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        Material bodyMaterial = GetOrCreateDroneMaterial("MAT_SOVA_Drone_Black", new Color(0.015f, 0.016f, 0.016f), 0.78f, 0.35f);
        Material panelMaterial = GetOrCreateDroneMaterial("MAT_SOVA_Drone_Gunmetal", new Color(0.18f, 0.18f, 0.17f), 0.65f, 0.25f);
        Material glassMaterial = GetOrCreateDroneMaterial("MAT_SOVA_Drone_Lens", new Color(0.02f, 0.06f, 0.09f), 0.95f, 0.1f);

        CreateBox(parent, "Main_Faceted_Body", new Vector3(0f, 0f, 0f), new Vector3(1.35f, 0.28f, 0.82f), Quaternion.identity, bodyMaterial);
        CreateBox(parent, "Forward_Nose", new Vector3(0f, -0.02f, 0.55f), new Vector3(0.74f, 0.32f, 0.62f), Quaternion.Euler(0f, 0f, 0f), bodyMaterial);
        CreateBox(parent, "Top_Spine", new Vector3(0f, 0.21f, -0.08f), new Vector3(0.28f, 0.16f, 1.15f), Quaternion.identity, panelMaterial);
        CreateBox(parent, "Rear_Power_Block", new Vector3(0f, 0.02f, -0.58f), new Vector3(0.88f, 0.28f, 0.42f), Quaternion.identity, bodyMaterial);
        CreateBox(parent, "Camera_Housing", new Vector3(0f, -0.16f, 0.88f), new Vector3(0.34f, 0.24f, 0.24f), Quaternion.identity, bodyMaterial);

        GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(lens, "Create Drone Lens");
        lens.name = "Forward_IR_Lens";
        lens.transform.SetParent(parent, false);
        lens.transform.localPosition = new Vector3(0f, -0.16f, 1.02f);
        lens.transform.localScale = new Vector3(0.16f, 0.16f, 0.08f);
        lens.GetComponent<Renderer>().sharedMaterial = glassMaterial;

        CreateBox(parent, "Left_Duct_Arm", new Vector3(-0.98f, 0f, 0.18f), new Vector3(1.35f, 0.12f, 0.18f), Quaternion.Euler(0f, 18f, 0f), bodyMaterial);
        CreateBox(parent, "Right_Duct_Arm", new Vector3(0.98f, 0f, 0.18f), new Vector3(1.35f, 0.12f, 0.18f), Quaternion.Euler(0f, -18f, 0f), bodyMaterial);

        CreateRotorAssembly(parent, "Left", new Vector3(-1.55f, 0.03f, 0.2f), bodyMaterial, panelMaterial);
        CreateRotorAssembly(parent, "Right", new Vector3(1.55f, 0.03f, 0.2f), bodyMaterial, panelMaterial);

        CreateBox(parent, "Left_Rear_Fin", new Vector3(-0.34f, 0.24f, -0.72f), new Vector3(0.18f, 0.55f, 0.58f), Quaternion.Euler(-18f, 0f, -18f), panelMaterial);
        CreateBox(parent, "Right_Rear_Fin", new Vector3(0.34f, 0.24f, -0.72f), new Vector3(0.18f, 0.55f, 0.58f), Quaternion.Euler(-18f, 0f, 18f), panelMaterial);
        CreateBox(parent, "Center_Tail_Fin", new Vector3(0f, 0.34f, -0.86f), new Vector3(0.16f, 0.72f, 0.5f), Quaternion.Euler(-12f, 0f, 0f), panelMaterial);

        CreateBox(parent, "Left_Front_Guard", new Vector3(-0.45f, -0.03f, 0.56f), new Vector3(0.08f, 0.58f, 0.13f), Quaternion.Euler(0f, 0f, -22f), panelMaterial);
        CreateBox(parent, "Right_Front_Guard", new Vector3(0.45f, -0.03f, 0.56f), new Vector3(0.08f, 0.58f, 0.13f), Quaternion.Euler(0f, 0f, 22f), panelMaterial);
    }

    private static void CreateRotorAssembly(Transform parent, string side, Vector3 center, Material bodyMaterial, Material panelMaterial)
    {
        GameObject duct = new GameObject($"{side}_Duct_Ring");
        Undo.RegisterCreatedObjectUndo(duct, "Create Drone Duct Ring");
        duct.transform.SetParent(parent, false);
        duct.transform.localPosition = center;
        duct.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        MeshFilter ductFilter = duct.AddComponent<MeshFilter>();
        ductFilter.sharedMesh = CreateTorusMesh($"{side}_Duct_Ring_Mesh", 0.62f, 0.07f, 64, 10);
        MeshRenderer ductRenderer = duct.AddComponent<MeshRenderer>();
        ductRenderer.sharedMaterial = bodyMaterial;

        CreateBox(parent, $"{side}_Rotor_Crossbar", center, new Vector3(1.02f, 0.08f, 0.1f), Quaternion.identity, bodyMaterial);

        GameObject rotorPivot = new GameObject($"{side}_Rotor_Spin_Pivot");
        Undo.RegisterCreatedObjectUndo(rotorPivot, "Create Drone Rotor Spin Pivot");
        rotorPivot.transform.SetParent(parent, false);
        rotorPivot.transform.localPosition = center;
        SovaRotorSpinner spinner = rotorPivot.AddComponent<SovaRotorSpinner>();
        spinner.flightDegreesPerSecond = side == "Left" ? 2400f : -2400f;

        CreateBox(rotorPivot.transform, $"{side}_Rotor_Motor", new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.14f, 0.24f), Quaternion.identity, panelMaterial);
        CreateBox(rotorPivot.transform, $"{side}_Rotor_Blade_A", new Vector3(0f, 0.035f, 0f), new Vector3(0.88f, 0.025f, 0.12f), Quaternion.Euler(0f, 18f, 0f), panelMaterial);
        CreateBox(rotorPivot.transform, $"{side}_Rotor_Blade_B", new Vector3(0f, 0.04f, 0f), new Vector3(0.88f, 0.025f, 0.12f), Quaternion.Euler(0f, 108f, 0f), panelMaterial);
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(box, $"Create {name}");
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = localRotation;
        box.transform.localScale = localScale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static Mesh CreateTorusMesh(string name, float majorRadius, float minorRadius, int majorSegments, int minorSegments)
    {
        Vector3[] vertices = new Vector3[majorSegments * minorSegments];
        Vector3[] normals = new Vector3[vertices.Length];
        int[] triangles = new int[majorSegments * minorSegments * 6];

        for (int major = 0; major < majorSegments; major++)
        {
            float majorAngle = major * Mathf.PI * 2f / majorSegments;
            Vector3 majorCenter = new Vector3(Mathf.Cos(majorAngle) * majorRadius, Mathf.Sin(majorAngle) * majorRadius, 0f);

            for (int minor = 0; minor < minorSegments; minor++)
            {
                float minorAngle = minor * Mathf.PI * 2f / minorSegments;
                Vector3 normal = new Vector3(Mathf.Cos(majorAngle) * Mathf.Cos(minorAngle), Mathf.Sin(majorAngle) * Mathf.Cos(minorAngle), Mathf.Sin(minorAngle));
                int vertexIndex = major * minorSegments + minor;
                vertices[vertexIndex] = majorCenter + normal * minorRadius;
                normals[vertexIndex] = normal;
            }
        }

        int triangleIndex = 0;
        for (int major = 0; major < majorSegments; major++)
        {
            int nextMajor = (major + 1) % majorSegments;
            for (int minor = 0; minor < minorSegments; minor++)
            {
                int nextMinor = (minor + 1) % minorSegments;
                int a = major * minorSegments + minor;
                int b = nextMajor * minorSegments + minor;
                int c = nextMajor * minorSegments + nextMinor;
                int d = major * minorSegments + nextMinor;

                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = b;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = c;
                triangles[triangleIndex++] = d;
            }
        }

        Mesh mesh = new Mesh
        {
            name = name,
            vertices = vertices,
            normals = normals,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material GetOrCreateDroneMaterial(string materialName, Color color, float smoothness, float metallic)
    {
        string materialPath = $"Assets/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            bool usingHDRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            Shader shader = Shader.Find(usingHDRP ? "HDRP/Lit" : "Standard");
            material = new Material(shader)
            {
                name = materialName
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else
        {
            material.color = color;
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", smoothness);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform[] CreateDefaultCheckpoints(Transform routeRoot, Terrain terrain)
    {
        Vector3[] positions =
        {
            GetTerrainWorldPoint(terrain, 0.35f, 0.35f, 10f),
            GetTerrainWorldPoint(terrain, 0.55f, 0.35f, 95f),
            GetTerrainWorldPoint(terrain, 0.65f, 0.58f, 8f),
            GetTerrainWorldPoint(terrain, 0.42f, 0.7f, 120f)
        };

        Transform[] checkpoints = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            string checkpointName = $"Checkpoint_{i + 1:00}";
            Transform checkpoint = routeRoot.Find(checkpointName);
            if (checkpoint == null)
            {
                GameObject checkpointObject = new GameObject(checkpointName);
                Undo.RegisterCreatedObjectUndo(checkpointObject, "Create Drone Checkpoint");
                checkpoint = checkpointObject.transform;
                checkpoint.SetParent(routeRoot, false);
            }

            checkpoint.position = positions[i];
            checkpoints[i] = checkpoint;
        }

        return checkpoints;
    }

    private static Vector3 GetTerrainWorldPoint(Terrain terrain, float normalizedX, float normalizedZ, float altitude)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            return new Vector3(normalizedX * 100f, altitude, normalizedZ * 100f);
        }

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        Vector3 point = new Vector3(
            terrainPosition.x + size.x * normalizedX,
            terrainPosition.y,
            terrainPosition.z + size.z * normalizedZ);
        point.y = terrain.SampleHeight(point) + terrainPosition.y + altitude;
        return point;
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

        bool usingHDRP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader shader = usingHDRP
            ? Shader.Find("DesertUAV/HDRP/Rain Streak")
            : AssetDatabase.LoadAssetAtPath<Shader>("Assets/RainStreak_Builtin.shader");
        if (shader == null && !usingHDRP)
        {
            shader = Shader.Find("Custom/RainStreak_Builtin");
        }

        material = new Material(shader)
        {
            name = "MAT_RainStreak"
        };
        Color rainColor = new Color(0.65f, 0.78f, 1f, 0.35f);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", rainColor);
            material.SetFloat("_SurfaceType", 1f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            material.SetColor("_Color", rainColor);
        }
        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
#endif
