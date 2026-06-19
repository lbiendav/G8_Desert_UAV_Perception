using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
[VolumeComponentMenu("Post-processing/Custom/Desert UAV Heat Noise")]
public sealed class HeatNoiseHDRP : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    public BoolParameter enabledEffect = new BoolParameter(false);

    private const string ShaderName = "Hidden/DesertUAV/HeatNoiseHDRP";
    private Material material;

    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.BeforePostProcess;

    public bool IsActive() => enabledEffect.value && material != null;

    public override void Setup()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader != null)
            material = CoreUtils.CreateEngineMaterial(shader);
        else
            Debug.LogError($"Unable to find HDRP heat-noise shader '{ShaderName}'.");
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        HeatNoiseCameraEffect controller = camera.camera.GetComponent<HeatNoiseCameraEffect>();
        if (controller == null || !controller.isActiveAndEnabled)
        {
            HDUtils.BlitCameraTexture(cmd, source, destination);
            return;
        }

        float heat = controller.GetCurrentHeatAmount();
        float baseNoise = controller.infraredCamera
            ? controller.infraredNoiseStrength
            : controller.opticalNoiseStrength;

        material.SetFloat("_NoiseStrength", baseNoise * heat);
        material.SetFloat("_HeatAmount", heat);
        material.SetFloat("_TimeSeed", Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
        material.SetTexture("_InputTexture", source);
        HDUtils.DrawFullScreen(cmd, material, destination);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(material);
    }
}
