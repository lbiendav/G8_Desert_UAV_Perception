Shader "Custom/WettablePBR_Builtin"
{
    Properties
    {
        _Color ("Albedo Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _Roughness ("Roughness", Range(0, 1)) = 0.75
        _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _OcclusionMap;

        fixed4 _Color;
        half _BumpScale;
        half _Metallic;
        half _Smoothness;
        half _Roughness;
        half _OcclusionStrength;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_OcclusionMap;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half occlusion = tex2D(_OcclusionMap, IN.uv_OcclusionMap).r;

            o.Albedo = albedo.rgb;
            o.Alpha = albedo.a;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Smoothness * (1.0h - _Roughness));
            o.Occlusion = lerp(1.0h, occlusion, _OcclusionStrength);
        }
        ENDCG
    }

    FallBack "Standard"
}
