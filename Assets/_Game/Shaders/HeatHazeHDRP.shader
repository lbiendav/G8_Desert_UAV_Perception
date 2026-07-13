Shader "DesertEnv/HeatHazeHDRP"
{
    // HDRP port of the built-in GrabPass heat haze. HDRP has no GrabPass, so
    // this pass is drawn by a DrawRenderers Custom Pass (injection point
    // BeforeTransparent) and reads the scene through
    // CustomPassSampleCameraColor with a noise-driven UV offset.
    // The pass has a custom LightMode so the regular HDRP loop never draws
    // it - only the custom pass (via override material) does.
    Properties
    {
        _NoiseTex ("Soft Cloud Noise", 2D) = "gray" {}
        _Distortion ("Distortion Strength", Range(0, 0.08)) = 0.018
        _Speed ("Noise Speed", Range(0, 3)) = 0.75
        _RisePower ("Rise Power", Range(0, 3)) = 1.15
        _SideWave ("Side Wave", Range(0, 1)) = 0.18
        _NearFadeDistance ("Near Fade Distance", Float) = 8
        _FarFullDistance ("Far Full Distance", Float) = 45
        _GlobalStrength ("Global Strength (script driven)", Range(0, 1)) = 1
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
    #pragma multi_compile_instancing
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "HeatHazeForward"
            Tags { "LightMode" = "HeatHazeForward" }

            Blend Off
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_COLOR
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_COLOR
            #define VARYINGS_NEED_POSITION_WS

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassRenderers.hlsl"

            TEXTURE2D(_NoiseTex);
            float4 _NoiseTex_ST;

            float _Distortion;
            float _Speed;
            float _RisePower;
            float _SideWave;
            float _NearFadeDistance;
            float _FarFullDistance;
            float _GlobalStrength;

            void GetSurfaceAndBuiltinData(FragInputs fragInputs, float3 viewDirection, inout PositionInputs posInput, out SurfaceData surfaceData, out BuiltinData builtinData)
            {
                // positionWS is camera-relative in HDRP, so its length is the
                // distance to the camera.
                float dist = length(posInput.positionWS);
                float distanceMask = smoothstep(_NearFadeDistance, _FarFullDistance, dist);

                // vertex alpha holds the ground mask baked by TerrainHeatHazeMesh
                float finalMask = saturate(fragInputs.color.a * distanceMask) * saturate(_GlobalStrength);

                float2 baseUv = fragInputs.texCoord0.xy * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                float t = _Time.y;

                float2 uv1 = baseUv * 2.2;
                uv1.y += t * _Speed * 0.45;
                uv1.x += sin(t * 0.7 + baseUv.y * 7.0) * 0.018;

                float2 uv2 = baseUv * 5.5;
                uv2.y += t * _Speed * 0.28;
                uv2.x += sin(t * 0.9 + baseUv.y * 13.0) * 0.025;

                float n1 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, s_linear_repeat_sampler, uv1, 0).r;
                float n2 = SAMPLE_TEXTURE2D_LOD(_NoiseTex, s_linear_repeat_sampler, uv2, 0).r;

                float noise = (n1 + n2) * 0.5;
                float softNoise = noise - 0.5;

                float wave = sin(t * _Speed * 1.1 + baseUv.y * 10.0);

                float2 offset;
                offset.x = (softNoise + wave * 0.08) * _Distortion * _SideWave;
                offset.y = abs(softNoise) * _Distortion * _RisePower;
                offset *= finalMask;

                float2 screenUv = posInput.positionNDC.xy + offset;
                float3 sceneColor = CustomPassSampleCameraColor(screenUv, 0);

                ZERO_INITIALIZE(BuiltinData, builtinData);
                ZERO_INITIALIZE(SurfaceData, surfaceData);
                builtinData.opacity = 1.0;
                builtinData.emissiveColor = float3(0.0, 0.0, 0.0);
                surfaceData.color = sceneColor;
            }

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }
}
