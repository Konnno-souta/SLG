// 筒・円錐・リング用。UV.x = 周方向(0→1), UV.y = 高さ or 半径(0→1)
// 光の柱 / 衝撃波リング / 魔法陣 / 渦 / 水柱 などに使う
Shader "FE/Column"
{
    Properties
    {
        [Header(Toon Colors)]
        [HDR] _CoreColor ("Core", Color) = (1,1,1,1)
        [HDR] _MainColor ("Main", Color) = (1,0.84,0.35,1)
        [HDR] _MainColor2 ("Main Gradient (near core)", Color) = (0.45,0.88,1,1)
        _OutlineColor ("Outline", Color) = (1,0.5,0.08,1)
        _CoreTh ("Core Threshold", Range(0,1.5)) = 0.65
        _MainTh ("Main Threshold", Range(0,1)) = 0.35
        _OutlineTh ("Outline Threshold", Range(0,1)) = 0.12
        _Intensity ("Core HDR Intensity", Float) = 1.6
        _Additive ("Additive (0 Alpha - 1 Add)", Range(0,1)) = 0
        _Dissolve ("Dissolve", Range(-1,1.5)) = 0
        [HDR] _GlowColor ("Glow Fringe (a = amount)", Color) = (0.2,0.7,1,0)

        [Header(Shape)]
        _Fill ("Fill (grow along V)", Range(-0.2,1.5)) = 1.1
        _FillSoft ("Fill Softness", Range(0.001,1)) = 0.08
        _EdgeNoise ("Fill Edge Noise (splash)", Range(0,1)) = 0
        _BottomSoft ("Bottom or Inner Softness", Range(0.001,1)) = 0.05
        _TopSoft ("Top or Outer Softness", Range(0.001,1)) = 0.2
        _Brightness ("Brightness", Float) = 1

        [Header(Pattern)]
        _NoiseAmt ("Noise Amount", Range(0,1)) = 0.5
        _NoiseScale ("Noise (X around int, Y along, Z scrollV, W scrollU)", Vector) = (8,2,2,0)
        _Twist ("Twist (spiral)", Float) = 0
        _SpokeCount ("Spoke Count", Float) = 8
        _SpokeAmt ("Spoke Amount", Range(0,1)) = 0

        [Header(View)]
        _Fresnel ("Fresnel Amount", Range(0,1)) = 0
        _Rim ("Rim Mode (0 center bright, 1 edge bright)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "FECommon.cginc"

            float _Fill, _FillSoft, _EdgeNoise, _BottomSoft, _TopSoft, _Brightness;
            float _Twist, _SpokeCount, _SpokeAmt, _Fresnel, _Rim;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                float4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float u = i.uv.x;
                float v = i.uv.y;
                float uu = u + v * _Twist;

                float2 np = float2(uu * _NoiseScale.x + _Time.y * _NoiseScale.w,
                                   v * _NoiseScale.y - _Time.y * _NoiseScale.z);
                float n = feFbmP(np, _NoiseScale.x);
                float spoke = 1.0 - abs(frac(uu * _SpokeCount) * 2.0 - 1.0);
                float pattern = lerp(n, spoke, _SpokeAmt);

                // 伸びる先端のギザギザ (水しぶきの王冠など)
                float edgeN = feNoiseP(float2(u * _NoiseScale.x * 2.0, _Time.y * 3.0), _NoiseScale.x * 2.0);
                float fillEdge = _Fill - edgeN * _EdgeNoise;

                float prof = saturate(v / _BottomSoft)
                           * saturate((fillEdge - v) / _FillSoft)
                           * saturate((1.0 - v) / _TopSoft);

                float3 N = normalize(i.wn);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float f = abs(dot(N, V));
                float fr = lerp(f, 1.0 - f, _Rim);

                float s = prof * lerp(1.0, pattern * 1.6, _NoiseAmt) * lerp(1.0, fr, _Fresnel) * _Brightness;
                s -= _Dissolve;

                return feToon(s, i.color.rgb) * i.color.a;
            }
            ENDCG
        }
    }
}
