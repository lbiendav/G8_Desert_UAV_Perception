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
                fixed heat = saturate(i.color.a * _GlobalHeatIntensity);
                fixed3 emission = i.color.rgb * heat * _EmissionMultiplier;
                return fixed4(emission, heat);
            }
            ENDCG
        }
    }
}
