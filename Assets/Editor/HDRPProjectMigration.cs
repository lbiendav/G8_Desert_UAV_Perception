using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class HDRPProjectMigration
{
    private const string SettingsFolder = "Assets/Settings/HDRP";
    private const string PipelineAssetPath = SettingsFolder + "/DesertUAV_HDRPAsset.asset";
    private const string VolumeProfilePath = SettingsFolder + "/DesertUAV_HDRPVolumeProfile.asset";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string AutoMigrationKey = "DesertUAV.HDRP.AutoMigration.2026-06-18";

    [InitializeOnLoadMethod]
    private static void ScheduleAutomaticMigration()
    {
        if (SessionState.GetBool(AutoMigrationKey, false) ||
            GraphicsSettings.defaultRenderPipeline is HDRenderPipelineAsset)
        {
            return;
        }

        SessionState.SetBool(AutoMigrationKey, true);
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
                Run();
        };
    }

    [MenuItem("Tools/Desert UAV/Migrate Project to HDRP")]
    public static void Run()
    {
        try
        {
            EnsureFolder("Assets/Settings");
            EnsureFolder(SettingsFolder);

            HDRenderPipelineAsset pipelineAsset =
                AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                pipelineAsset.name = "DesertUAV_HDRPAsset";
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            ClearQualityPipelineOverrides();
            PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UpgradeBuiltInMaterials();
            ConvertUnsupportedCustomMaterials();
            ConfigureMainScene();
            RegisterHeatNoisePostProcess();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HDRP migration completed successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunBatchMode()
    {
        Run();
        EditorApplication.Exit(0);
    }

    private static void ClearQualityPipelineOverrides()
    {
        int currentQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = null;
        }

        QualitySettings.SetQualityLevel(currentQuality, false);
    }

    private static void UpgradeBuiltInMaterials()
    {
        List<MaterialUpgrader> upgraders =
            MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(HDRenderPipelineAsset));
        MaterialUpgrader.UpgradeProjectFolder(
            upgraders,
            "Converting Built-in materials to HDRP",
            MaterialUpgrader.UpgradeFlags.LogMessageWhenNoUpgraderFound);
    }

    private static void ConvertUnsupportedCustomMaterials()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        Shader hdrpUnlit = Shader.Find("HDRP/Unlit");

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Samples/"))
                continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null)
                continue;

            string shaderName = material.shader.name;
            bool water = shaderName.StartsWith("AQUAS", StringComparison.OrdinalIgnoreCase);
            bool rain = shaderName == "Custom/RainStreak_Builtin";
            bool thermal = shaderName == "Custom/TerrainThermalEmissionOverlay_Builtin";
            bool heatHaze = shaderName == "Custom/HeatHaze_Builtin";
            bool wettable = shaderName == "Custom/WettablePBR_Builtin";

            if (!water && !rain && !thermal && !heatHaze && !wettable)
                continue;

            MaterialSnapshot snapshot = new MaterialSnapshot(material);
            material.shader = (rain || thermal || heatHaze) ? hdrpUnlit : hdrpLit;
            snapshot.ApplyToHDRP(material);

            if (water || rain || thermal || heatHaze)
            {
                material.SetFloat("_SurfaceType", 1f);
                material.SetFloat("_BlendMode", 0f);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (thermal && material.HasProperty("_EmissiveColor"))
            {
                Color emission = snapshot.color.maxColorComponent > 0f
                    ? snapshot.color
                    : new Color(1f, 0.25f, 0.05f, 1f);
                material.SetColor("_EmissiveColor", emission * 2f);
            }

            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            Debug.Log($"Converted custom material to HDRP: {path}");
        }
    }

    private static void ConfigureMainScene()
    {
        if (!File.Exists(ScenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.GetComponent<HDAdditionalCameraData>() == null)
                camera.gameObject.AddComponent<HDAdditionalCameraData>();
        }

        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.GetComponent<HDAdditionalLightData>() == null)
                light.gameObject.AddComponent<HDAdditionalLightData>();
        }

        VolumeProfile profile = CreateOrUpdateVolumeProfile();
        GameObject volumeObject = GameObject.Find("HDRP Global Volume");
        if (volumeObject == null)
            volumeObject = new GameObject("HDRP Global Volume");

        Volume volume = volumeObject.GetComponent<Volume>();
        if (volume == null)
            volume = volumeObject.AddComponent<Volume>();

        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = profile;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static VolumeProfile CreateOrUpdateVolumeProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "DesertUAV_HDRPVolumeProfile";
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        profile.components.RemoveAll(component => component == null);

        VisualEnvironment environment = GetOrAdd<VisualEnvironment>(profile);
        environment.skyType.Override(SkySettings.GetUniqueID(typeof(GradientSky)));

        GradientSky sky = GetOrAdd<GradientSky>(profile);
        sky.top.Override(new Color(0.16f, 0.37f, 0.72f));
        sky.middle.Override(new Color(0.72f, 0.64f, 0.51f));
        sky.bottom.Override(new Color(0.31f, 0.22f, 0.14f));
        sky.gradientDiffusion.Override(1.2f);

        Exposure exposure = GetOrAdd<Exposure>(profile);
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(0f);

        HeatNoiseHDRP heatNoise = GetOrAdd<HeatNoiseHDRP>(profile);
        heatNoise.enabledEffect.Override(true);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
            return component;

        component = profile.Add<T>(true);
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static void RegisterHeatNoisePostProcess()
    {
        UnityEngine.Object settings = AssetDatabase.LoadMainAssetAtPath(
            "Assets/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset");
        if (settings == null)
            return;

        SerializedObject serializedSettings = new SerializedObject(settings);
        SerializedProperty iterator = serializedSettings.GetIterator();
        string typeName = typeof(HeatNoiseHDRP).AssemblyQualifiedName;
        bool registered = false;

        while (iterator.NextVisible(true))
        {
            if (!iterator.isArray ||
                iterator.name != "m_CustomPostProcessTypesAsString" ||
                !iterator.propertyPath.Contains("m_BeforePostProcessCustomPostProcesses"))
            {
                continue;
            }

            bool alreadyPresent = false;
            for (int i = 0; i < iterator.arraySize; i++)
            {
                if (iterator.GetArrayElementAtIndex(i).stringValue == typeName)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                int index = iterator.arraySize;
                iterator.InsertArrayElementAtIndex(index);
                iterator.GetArrayElementAtIndex(index).stringValue = typeName;
            }

            registered = true;
        }

        if (!registered)
        {
            Debug.LogWarning(
                "Could not register HeatNoiseHDRP automatically. Add it under Graphics > HDRP > Custom Post Process Orders > Before Post Process.");
            return;
        }

        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, name);
    }

    private readonly struct MaterialSnapshot
    {
        public readonly Color color;
        private readonly Texture mainTexture;
        private readonly Texture normalTexture;
        private readonly Texture occlusionTexture;
        private readonly float metallic;
        private readonly float smoothness;

        public MaterialSnapshot(Material material)
        {
            color = ReadColor(material, "_Color", Color.white);
            mainTexture = ReadTexture(material, "_MainTex");
            normalTexture = ReadTexture(material, "_BumpMap");
            occlusionTexture = ReadTexture(material, "_OcclusionMap");
            metallic = ReadFloat(material, "_Metallic", 0f);
            smoothness = ReadFloat(material, "_Smoothness", 0.5f);
        }

        public void ApplyToHDRP(Material material)
        {
            SetColor(material, "_BaseColor", color);
            SetTexture(material, "_BaseColorMap", mainTexture);
            SetTexture(material, "_NormalMap", normalTexture);
            SetTexture(material, "_MaskMap", occlusionTexture);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_Smoothness", smoothness);
        }

        private static Color ReadColor(Material material, string property, Color fallback) =>
            material.HasProperty(property) ? material.GetColor(property) : fallback;

        private static Texture ReadTexture(Material material, string property) =>
            material.HasProperty(property) ? material.GetTexture(property) : null;

        private static float ReadFloat(Material material, string property, float fallback) =>
            material.HasProperty(property) ? material.GetFloat(property) : fallback;

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
                material.SetColor(property, value);
        }

        private static void SetTexture(Material material, string property, Texture value)
        {
            if (value != null && material.HasProperty(property))
                material.SetTexture(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }
    }
}
