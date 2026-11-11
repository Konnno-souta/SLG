// 斬撃・刺突用リボン。UV.x = 軌跡方向(0→1), UV.y = 幅方向(0=内側, 1=刃側)
// _Head を 0 → 1+TailLen に動かすと「振り抜き」になる
Shader "FE/Slash"
{
    Properties
    {
        [Header(Toon Colors)]
        [HDR] _CoreColor ("Core", Color) = (1,1,1,1)
        [HDR] _MainColor ("Main", Color) = (0.15,0.5,1,1)
        [HDR] _MainColor2 ("Main Gradient (near core)", Color) = (0.45,0.88,1,1)
        _OutlineColor ("Outline", Color) = (0.02,0.07,0.32,1)
        _CoreTh ("Core Threshold", Range(0,1.5)) = 0.62
        _MainTh ("Main Threshold", Range(0,1)) = 0.26
        _OutlineTh ("Outline Threshold", Range(0,1)) = 0.08
        _Intensity ("Core HDR Intensity", Float) = 1.8
        _Additive ("Additive (0 Alpha - 1 Add)", Range(0,1)) = 0
        _Dissolve ("Dissolve", Range(-1,1.5)) = 0
        [HDR] _GlowColor ("Glow Fringe (a = amount)", Color) = (0.2,0.7,1,0)

        [Header(Slash Shape)]
        _Head ("Head Position", Float) = 1
        _TailLen ("Tail Length", Range(0.01,2)) = 0.75
        _HeadTaper ("Head Taper", Range(0.001,0.5)) = 0.22
        _TailPow ("Tail Power", Range(0.1,4)) = 1.1
        _OuterSoft ("Blade Edge Softness", Range(0.01,1)) = 0.12
        _Symmetric ("Symmetric (thrust)", Range(0,1)) = 0
        _FlipU ("Flip U (TrailRenderer)", Range(0,1)) = 0

        [Header(3D Shading)]
        _Shade ("Grazing Shade (side faces darker)", Range(0,1)) = 0
        _BackShade ("Back Face Shade", Range(0,1)) = 0
        [Header(Extrusion Side Layer)]
        _Extrude ("Extrude Offset (view space m: x right, y up, z back)", Vector) = (0,0,0,0)

        [Header(Noise)]
        _NoiseAmt ("Noise Amount", Range(0,1)) = 0.35
        _NoiseScale ("Noise (X along, Y across, Z speed)", Vector) = (5,2,3,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
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

            float _Head, _TailLen, _HeadTaper, _TailPow, _OuterSoft, _Symmetric, _FlipU, _Shade, _BackShade;
            float4 _Extrude;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                // 押し出し層: カメラ基準で下・奥へずらす (どの角度から見ても側面=厚みに見える)
                float3 vp = UnityObjectToViewPos(v.vertex.xyz);
                vp += float3(_Extrude.x, _Extrude.y, -_Extrude.z);
                o.pos = mul(UNITY_MATRIX_P, float4(vp, 1.0));
                o.uv = v.uv;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            float4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                float u = lerp(i.uv.x, 1.0 - i.uv.x, _FlipU);
                float v = i.uv.y;

                // ヘッドからの距離 (0=先端, 1=尻尾の終わり)
                float t = (_Head - u) / max(_TailLen, 1e-3);
                // 三日月の太さ: 先端は尖り、尻尾に向かって細る
                float w = pow(saturate(t / _HeadTaper), 0.8) * pow(saturate(1.0 - t), _TailPow);

                // 幅方向: 通常は刃側(v=1)に寄せる / 刺突は中心線に寄せる
                float a = lerp(v, 1.0 - abs(v * 2.0 - 1.0), _Symmetric);
                float inner = saturate((a - (1.0 - w)) / max(w, 1e-3));
                float outer = lerp(saturate((1.0 - a) / max(_OuterSoft * w, 1e-4)), 1.0, _Symmetric);
                float s = inner * outer * (1.0 - 0.45 * saturate(t)) * saturate(u / 0.05);

                // 風っぽい筋
                float n = feFbmP(float2(u * _NoiseScale.x - _Time.y * _NoiseScale.z, v * _NoiseScale.y), 100000.0);
                s *= lerp(1.0, n * 1.6, _NoiseAmt);
                s -= _Dissolve;

                float4 col = feToon(s, i.color.rgb) * i.color.a;

                // 立体感: 斜めを向いた面・裏面を暗くする (帯がねじれて見える)
                float ndv = abs(dot(normalize(i.wn), normalize(_WorldSpaceCameraPos - i.wp)));
                float shade = lerp(1.0 - _Shade, 1.0, ndv) * (facing > 0 ? 1.0 : 1.0 - _BackShade);
                col.rgb *= shade;
                return col;
            }
            ENDCG
        }
    }
}
