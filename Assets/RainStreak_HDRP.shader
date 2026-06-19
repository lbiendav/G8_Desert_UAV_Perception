Shader "DesertUAV/HDRP/Rain Streak"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.65, 0.78, 1, 0.35)
        _BaseColorMap ("Particle Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);
            float4 _BaseColorMap_ST;
            float4 _BaseColor;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.uv = input.uv * _BaseColorMap_ST.xy + _BaseColorMap_ST.zw;
                output.color = input.color * _BaseColor;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv) * input.color;
                color.a *= saturate(1.0 - abs(input.uv.x - 0.5) * 1.8);
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
