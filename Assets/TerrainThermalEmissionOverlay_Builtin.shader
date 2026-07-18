Shader "Custom/TerrainThermalEmissionOverlay_Builtin"
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
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _GlobalHeatIntensity;
            float _EmissionMultiplier;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed environmentTemperature = lerp(0.12, 1.0, saturate(_GlobalHeatIntensity));
                fixed heat = saturate(i.color.a * environmentTemperature);
                fixed3 cold = fixed3(0.08, 0.0, 0.12);
                fixed3 warm = fixed3(0.95, 0.08, 0.01);
                fixed3 hot = fixed3(1.0, 0.72, 0.08);
                fixed3 thermalColor = heat < 0.65
                    ? lerp(cold, warm, heat / 0.65)
                    : lerp(warm, hot, (heat - 0.65) / 0.35);
                return fixed4(thermalColor * heat * _EmissionMultiplier, heat);
            }
            ENDCG
        }
    }
}
