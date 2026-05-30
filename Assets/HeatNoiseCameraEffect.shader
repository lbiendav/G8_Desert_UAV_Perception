Shader "Hidden/HeatNoiseCameraEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.1
        _HeatAmount ("Heat Amount", Range(0, 1)) = 1
        _TimeSeed ("Time Seed", Float) = 0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _NoiseStrength;
            float _HeatAmount;
            float _TimeSeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32 + _TimeSeed);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float lineNoise = hash(float2(floor(i.uv.y * 720.0), floor(_TimeSeed * 24.0))) - 0.5;
                float pixelNoise = hash(i.uv * _ScreenParams.xy + _TimeSeed) - 0.5;
                float2 shimmer = float2(lineNoise, pixelNoise) * _NoiseStrength * _MainTex_TexelSize.xy * 12.0;

                fixed4 color = tex2D(_MainTex, i.uv + shimmer);
                float grain = pixelNoise * _NoiseStrength;
                color.rgb += grain;
                color.rgb = lerp(color.rgb, color.rgb * color.rgb, _HeatAmount * 0.15);
                return color;
            }
            ENDCG
        }
    }
}
