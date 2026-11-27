Shader "Ares/WaterSurface"
{
    Properties
    {
        _MainTex ("Water Tile", 2D) = "white" {}
        _NoiseTex ("Distortion Noise", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Distort ("Distortion", Range(0,0.2)) = 0.045
        _ScrollX ("Scroll X", Float) = 0.012
        _ScrollY ("Scroll Y", Float) = 0.03
        _Shimmer ("Shimmer", Range(0,1)) = 0.35
        _Fade ("Edge Fade", Range(0,4)) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            fixed4 _Color;
            float _Distort;
            float _ScrollX;
            float _ScrollY;
            float _Shimmer;
            float _Fade;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;

                // two noise layers scrolling in opposite directions drive a small UV wobble
                float2 n1 = tex2D(_NoiseTex, i.uv * 1.7 + float2(t * 0.03, t * 0.05)).rg - 0.5;
                float2 n2 = tex2D(_NoiseTex, i.uv * 3.1 - float2(t * 0.02, t * 0.04)).rg - 0.5;

                float2 uv = i.uv + float2(_ScrollX * t, _ScrollY * t) + (n1 + n2 * 0.6) * _Distort;

                fixed3 col = tex2D(_MainTex, uv).rgb;
                fixed3 col2 = tex2D(_MainTex, uv + float2(0.004, 0.006)).rgb;

                // cheap "specular" sheen from the wobble gradient
                float sheen = saturate(length(col - col2) * 6.0) * _Shimmer;
                col += sheen * 0.9;

                // slightly lighter near the top of each tile => ripples read as ridges
                float band = 0.5 + 0.5 * sin((uv.y + n1.x * 0.4) * 42.0);
                col += band * 0.06 * _Shimmer;

                fixed4 outc;
                outc.rgb = col * _Color.rgb;
                outc.a = _Color.a * i.color.a;
                return outc;
            }
            ENDCG
        }
    }
    Fallback Off
}
