using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class HDRPMaterialRepair
{
    private const string SessionKey = "DesertUAV.HDRPMaterialRepair.v8";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string HeatHazeNoisePath = "Assets/Settings/HDRP/HeatHazeDistortion.asset";
    private static int sceneViewResetFrames;

    static HDRPMaterialRepair()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
                RepairAll();
        };
    }

    [InitializeOnLoadMethod]
    private static void EnableSceneViewImageEffects()
    {
        sceneViewResetFrames = 120;
        EditorApplication.update -= ResetSceneViewToShaded;
        EditorApplication.update += ResetSceneViewToShaded;
    }

    private static void ResetSceneViewToShaded()
    {
        if (--sceneViewResetFrames <= 0)
        {
            EditorApplication.update -= ResetSceneViewToShaded;
            return;
        }

        foreach (SceneView sceneView in SceneView.sceneViews)
        {
            sceneView.SetSceneViewShaderReplace(null, null);
            sceneView.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
            sceneView.sceneLighting = true;
            sceneView.sceneViewState.showFog = true;
            sceneView.sceneViewState.showImageEffects = true;
            sceneView.Repaint();
        }
    }

    [MenuItem("Tools/Desert UAV/HDRP/Repair All Materials")]
    public static void RepairAll()
    {
        try
        {
            RepairTrackedMaterials();
            RepairTerrainLayers();
            RepairSpecialMaterials();
            RepairSceneObjects();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Desert UAV HDRP material repair completed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void RepairTrackedMaterials()
    {
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit == null)
            throw new InvalidOperationException("HDRP/Lit shader is unavailable.");

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Samples/") ||
                path.Contains("/AQUAS-Lite/Materials/") ||
                path.EndsWith("MAT_HeatHaze.mat") ||
                path.EndsWith("MAT_RainStreak.mat"))
            {
                continue;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            string original = ReadGitVersion(path);
            if (string.IsNullOrEmpty(original))
                continue;

            string originalShader = ReadShaderGuid(original);
            bool wasBuiltIn = originalShader == "0000000000000000f000000000000000";
            if (!wasBuiltIn)
                continue;

            Texture albedo = LoadTexture(ReadTextureGuid(original, "_MainTex"));
            Texture normal = LoadTexture(ReadTextureGuid(original, "_BumpMap"));
            Color color = ReadColor(original, "_Color", Color.white);
            float metallic = ReadFloat(original, "_Metallic", 0f);
            float smoothness = Mathf.Clamp(ReadFloat(original, "_Glossiness", 0.25f), 0f, 0.35f);

            material.shader = hdrpLit;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseColorMap", albedo);
            material.SetTexture("_NormalMap", normal);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_SurfaceType", 0f);
            material.SetFloat("_AlphaCutoffEnable", 0f);
            material.SetFloat("_DoubleSidedEnable", 0f);
            material.SetMaterialType(MaterialId.LitStandard);
            material.renderQueue = -1;

            material.DisableKeyword("_MATERIAL_FEATURE_SPECULAR_COLOR");
            material.DisableKeyword("_SPECULARCOLORMAP");
            material.DisableKeyword("_EMISSION");
            if (normal != null)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");

            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
        }
    }

    private static void RepairTerrainLayers()
    {
        string[] guids = AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
                continue;

            layer.metallic = 0f;
            layer.smoothness = Mathf.Min(layer.smoothness, 0.25f);
            layer.normalScale = Mathf.Clamp(layer.normalScale, 0.4f, 1f);
            EditorUtility.SetDirty(layer);
        }
    }

    private static void RepairSpecialMaterials()
    {
        RepairWaterMaterial("Assets/AQUAS-Lite/Materials/AQUAS_Lite_Water.mat", false);
        RepairWaterMaterial("Assets/AQUAS-Lite/Materials/AQUAS_Lite_Water_Backface.mat", true);
        RepairWaterMaterial("Assets/AQUAS-Lite/Models/Materials/WaterSimpleDaylight.mat", false);

        Material rain = AssetDatabase.LoadAssetAtPath<Material>("Assets/MAT_RainStreak.mat");
        Shader rainShader = Shader.Find("DesertUAV/HDRP/Rain Streak");
        if (rain != null && rainShader != null)
        {
            rain.shader = rainShader;
            rain.SetColor("_BaseColor", new Color(0.65f, 0.78f, 1f, 0.35f));
            rain.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(rain);
        }

        Material heatHaze = AssetDatabase.LoadAssetAtPath<Material>("Assets/MAT_HeatHaze.mat");
        if (heatHaze != null)
        {
            heatHaze.shader = Shader.Find("HDRP/Unlit");
            heatHaze.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 0f));
            heatHaze.SetFloat("_SurfaceType", 1f);
            heatHaze.SetFloat("_BlendMode", 0f);
            heatHaze.SetFloat("_DistortionEnable", 1f);
            heatHaze.SetFloat("_DistortionOnly", 1f);
            heatHaze.SetFloat("_DistortionDepthTest", 1f);
            heatHaze.SetFloat("_DistortionScale", 3f);
            heatHaze.SetFloat("_DistortionVectorScale", 2f);
            heatHaze.SetFloat("_DistortionVectorBias", -1f);
            heatHaze.SetFloat("_DistortionBlurScale", 0.08f);
            heatHaze.SetTexture("_DistortionVectorMap", GetOrCreateHeatHazeNoise());
            heatHaze.renderQueue = (int)RenderQueue.Transparent;
            HDMaterial.ValidateMaterial(heatHaze);
            heatHaze.SetShaderPassEnabled("DistortionVectors", true);
            EditorUtility.SetDirty(heatHaze);
        }
    }

    private static void RepairWaterMaterial(string path, bool backface)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("HDRP/Lit");
        if (material == null || shader == null)
            return;

        string original = ReadGitVersion(path);
        Color deepColor = ReadColor(original, "_DeepWaterColor", new Color(0.08f, 0.32f, 0.42f, 0.55f));
        deepColor.a = backface ? 0.18f : 0.48f;

        material.shader = shader;
        material.SetColor("_BaseColor", deepColor);
        material.SetFloat("_SurfaceType", 1f);
        material.SetFloat("_BlendMode", 0f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.94f);
        material.SetFloat("_DoubleSidedEnable", 1f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_TransparentSortPriority", backface ? -1f : 0f);
        material.renderQueue = (int)RenderQueue.Transparent;
        HDMaterial.ValidateMaterial(material);
        EditorUtility.SetDirty(material);
    }

    private static void RepairSceneObjects()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        TerrainHeatHazeMesh hazeMesh =
            UnityEngine.Object.FindFirstObjectByType<TerrainHeatHazeMesh>(
                FindObjectsInactive.Include);
        if (hazeMesh != null)
        {
            GameObject heatHaze = hazeMesh.gameObject;
            heatHaze.SetActive(true);
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/MAT_HeatHaze.mat");
            hazeMesh.environment =
                UnityEngine.Object.FindFirstObjectByType<DesertEnvironmentController>(
                    FindObjectsInactive.Include);
            hazeMesh.distortionStrength = 3f;
            hazeMesh.distortionTiling = new Vector2(7f, 7f);
            hazeMesh.distortionScroll = new Vector2(0.035f, 0.11f);
            hazeMesh.minimumTriangleHeat = 0.04f;
            hazeMesh.Generate();
            EditorUtility.SetDirty(hazeMesh);

            MeshRenderer renderer = heatHaze.GetComponent<MeshRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        ConfigureCameraDistortion();

        TerrainThermalEmissionOverlay overlay =
            UnityEngine.Object.FindFirstObjectByType<TerrainThermalEmissionOverlay>(
                FindObjectsInactive.Include);
        Shader overlayShader = Shader.Find("DesertUAV/HDRP/Terrain Thermal Overlay");
        if (overlay != null && overlayShader != null)
        {
            overlay.overlayShader = overlayShader;
            MeshRenderer renderer = overlay.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = new Material(overlayShader)
                {
                    name = "Runtime_Terrain_Thermal_Emission_Overlay_HDRP"
                };
                renderer.sharedMaterial = material;
            }
            EditorUtility.SetDirty(overlay);
        }

        GameObject waterPlane = GameObject.Find("WaterPlane");
        Material water = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/AQUAS-Lite/Materials/AQUAS_Lite_Water.mat");
        if (waterPlane != null && water != null)
        {
            foreach (Renderer renderer in waterPlane.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = water;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureCameraDistortion()
    {
        foreach (HDAdditionalCameraData cameraData in
                 UnityEngine.Object.FindObjectsByType<HDAdditionalCameraData>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            cameraData.customRenderingSettings = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.Distortion, true);
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.RoughDistortion, true);
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.TransparentObjects, true);
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.Distortion] = true;
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.RoughDistortion] = true;
            cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[
                (uint)FrameSettingsField.TransparentObjects] = true;
            EditorUtility.SetDirty(cameraData);
        }
    }

    private static Texture2D GetOrCreateHeatHazeNoise()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HeatHazeNoisePath);
        if (texture != null)
            return texture;

        const int size = 128;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
        {
            name = "HeatHazeDistortion",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x / (float)size;
                float ny = y / (float)size;
                float a = Mathf.PerlinNoise(nx * 4.2f + 11.3f, ny * 4.2f + 7.7f);
                float b = Mathf.PerlinNoise(nx * 9.1f + 31.1f, ny * 9.1f + 19.4f);
                float dx = Mathf.Clamp01(0.5f + (a - 0.5f) * 0.9f + (b - 0.5f) * 0.35f);
                float dy = Mathf.Clamp01(0.5f + (b - 0.5f) * 0.7f - (a - 0.5f) * 0.3f);
                pixels[y * size + x] = new Color(dx, dy, 0.08f, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        AssetDatabase.CreateAsset(texture, HeatHazeNoisePath);
        return texture;
    }

    private static string ReadGitVersion(string assetPath)
    {
        try
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"show HEAD:\"{assetPath.Replace('\\', '/')}\"",
                WorkingDirectory = Directory.GetParent(Application.dataPath)?.FullName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(info);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadShaderGuid(string yaml)
    {
        Match match = Regex.Match(yaml ?? string.Empty, @"m_Shader:.*guid:\s*([0-9a-f]{32})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ReadTextureGuid(string yaml, string property)
    {
        Match match = Regex.Match(
            yaml ?? string.Empty,
            @"-\s+" + Regex.Escape(property) +
            @":\s*\r?\n\s+m_Texture:\s+\{fileID:\s+\d+,\s+guid:\s+([0-9a-f]{32})",
            RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static Texture LoadTexture(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return null;
        return AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static float ReadFloat(string yaml, string property, float fallback)
    {
        Match match = Regex.Match(
            yaml ?? string.Empty,
            @"-\s+" + Regex.Escape(property) + @":\s+([-+]?[0-9]*\.?[0-9]+)");
        return match.Success && float.TryParse(
            match.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float value)
            ? value
            : fallback;
    }

    private static Color ReadColor(string yaml, string property, Color fallback)
    {
        Match match = Regex.Match(
            yaml ?? string.Empty,
            @"-\s+" + Regex.Escape(property) +
            @":\s+\{r:\s*([^,]+),\s*g:\s*([^,]+),\s*b:\s*([^,]+),\s*a:\s*([^}]+)\}");
        if (!match.Success)
            return fallback;

        if (float.TryParse(match.Groups[1].Value, out float r) &&
            float.TryParse(match.Groups[2].Value, out float g) &&
            float.TryParse(match.Groups[3].Value, out float b) &&
            float.TryParse(match.Groups[4].Value, out float a))
        {
            return new Color(r, g, b, a);
        }

        return fallback;
    }
}
