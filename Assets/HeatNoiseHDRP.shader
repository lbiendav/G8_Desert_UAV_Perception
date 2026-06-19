Shader "Hidden/DesertUAV/HeatNoiseHDRP"
{
    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FullscreenShader.hlsl"

    TEXTURE2D_X(_InputTexture);

    float _NoiseStrength;
    float _HeatAmount;
    float _TimeSeed;

    float Hash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32 + _TimeSeed);
        return frac(p.x * p.y);
    }

    float4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv = ClampAndScaleUVForBilinearPostProcessTexture(input.texcoord.xy);
        float lineNoise = Hash(float2(floor(uv.y * 720.0), floor(_TimeSeed * 24.0))) - 0.5;
        float pixelNoise = Hash(uv * _ScreenSize.xy + _TimeSeed) - 0.5;
        float groundMask = 1.0 - smoothstep(0.38, 0.92, uv.y);
        float wave = sin(uv.y * 55.0 + _TimeSeed * 2.4) * 0.5 + 0.5;
        float2 shimmerDirection = float2(
            lineNoise * 0.7 + pixelNoise * 0.3,
            abs(pixelNoise) * 0.7 + wave * 0.3);
        float2 shimmer = shimmerDirection * _NoiseStrength * groundMask * _ScreenSize.zw * 30.0;

        float4 color = SAMPLE_TEXTURE2D_X(_InputTexture, s_linear_clamp_sampler, uv + shimmer);
        color.rgb += pixelNoise * _NoiseStrength * groundMask * 0.12;
        color.rgb = lerp(color.rgb, color.rgb * color.rgb, _HeatAmount * groundMask * 0.08);
        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Desert UAV Heat Noise"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma fragment Frag
            ENDHLSL
        }
    }

    Fallback Off
}
