Shader "DesertUAV/HDRP/Terrain Thermal Overlay"
{
    Properties
    {
        _GlobalHeatIntensity ("Global Heat Intensity", Range(0, 1)) = 1
        _EmissionMultiplier ("Emission Multiplier", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            float _GlobalHeatIntensity;
            float _EmissionMultiplier;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float heat = saturate(input.color.a * _GlobalHeatIntensity);
                float3 emission = max(input.color.rgb, float3(0.3, 0.03, 0.0));
                return float4(emission * heat * _EmissionMultiplier, heat * 0.35);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
