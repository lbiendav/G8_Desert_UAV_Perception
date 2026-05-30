Shader "Custom/HeatHaze_Builtin"
{
    Properties
    {
        _NoiseTex ("Soft Cloud Noise", 2D) = "white" {}
        _Distortion ("Distortion Strength", Range(0, 0.08)) = 0.018
        _GlobalHeatIntensity ("Global Heat Intensity", Range(0, 1)) = 1
        _Speed ("Noise Speed", Range(0, 3)) = 0.75
        _RisePower ("Rise Power", Range(0, 3)) = 1.15
        _SideWave ("Side Wave", Range(0, 1)) = 0.18
        _NearFadeDistance ("Near Fade Distance", Float) = 8
        _FarFullDistance ("Far Full Distance", Float) = 45
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        GrabPass { "_HeatGrabTexture" }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend One Zero
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _HeatGrabTexture;
            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;

            float _Distortion;
            float _GlobalHeatIntensity;
            float _Speed;
            float _RisePower;
            float _SideWave;
            float _NearFadeDistance;
            float _FarFullDistance;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 grabPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float mask : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _NoiseTex);
                o.mask = v.color.a;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float dist = distance(_WorldSpaceCameraPos, i.worldPos);
                float distanceMask = smoothstep(_NearFadeDistance, _FarFullDistance, dist);

                float finalMask = saturate(i.mask * distanceMask * _GlobalHeatIntensity);

                float2 uv1 = i.uv * 2.2;
                uv1.y += _Time.y * _Speed * 0.45;
                uv1.x += sin(_Time.y * 0.7 + i.uv.y * 7.0) * 0.018;

                float2 uv2 = i.uv * 5.5;
                uv2.y += _Time.y * _Speed * 0.28;
                uv2.x += sin(_Time.y * 0.9 + i.uv.y * 13.0) * 0.025;

                float n1 = tex2D(_NoiseTex, uv1).r;
                float n2 = tex2D(_NoiseTex, uv2).r;

                float noise = (n1 + n2) * 0.5;
                float softNoise = noise - 0.5;

                float wave = sin(_Time.y * _Speed * 1.1 + i.uv.y * 10.0);

                float2 offset;
                offset.x = (softNoise + wave * 0.08) * _Distortion * _SideWave;
                offset.y = abs(softNoise) * _Distortion * _RisePower;

                offset *= finalMask;

                i.grabPos.xy += offset * i.grabPos.w;

                return tex2Dproj(_HeatGrabTexture, UNITY_PROJ_COORD(i.grabPos));
            }

            ENDCG
        }
    }
}
