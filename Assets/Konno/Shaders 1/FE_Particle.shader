// パーティクル用トゥーンシェーダー
// 頂点カラーのα = 侵食量 (Color over Lifetime のαを下げると、縁から削れて消える)
// _Shape: 0=煙玉 1=ひし形(火花/筋) 2=リング 3=十字星 4=六角 5=キラキラ 6=破片 7=強打の光 8=細いリング
Shader "FE/Particle"
{
    Properties
    {
        [Header(Toon Colors)]
        [HDR] _CoreColor ("Core", Color) = (1,1,1,1)
        [HDR] _MainColor ("Main", Color) = (0.15,0.5,1,1)
        [HDR] _MainColor2 ("Main Gradient (near core)", Color) = (0.45,0.88,1,1)
        _OutlineColor ("Outline", Color) = (0.02,0.07,0.32,1)
        _CoreTh ("Core Threshold", Range(0,1.5)) = 0.6
        _MainTh ("Main Threshold", Range(0,1)) = 0.3
        _OutlineTh ("Outline Threshold", Range(0,1)) = 0.1
        _Intensity ("Core HDR Intensity", Float) = 1.6
        _Additive ("Additive (0 Alpha - 1 Add)", Range(0,1)) = 0
        _Dissolve ("Dissolve", Range(-1,1.5)) = 0
        [HDR] _GlowColor ("Glow Fringe (a = amount)", Color) = (0.2,0.7,1,0)

        [Header(Shape)]
        _Shape ("Shape 0Puff 1Diamond 2Ring 3Star 4Hex 5Twinkle 6Shard 7Burst 8ThinRing", Range(0,8)) = 1
        _NoiseAmt ("Noise Amount", Range(0,1)) = 0
        _NoiseScale ("Noise (XY scale, Z speed)", Vector) = (2,2,1,0)
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

            float _Shape;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 十字の針 (L = 長さ, W = 根元の太さ)。rot だけ回した向き
            float feNeedles(float2 p, float rot, float L, float W)
            {
                float c = cos(rot), sn = sin(rot);
                float2 q = abs(float2(p.x * c + p.y * sn, -p.x * sn + p.y * c));
                return max(1.0 - q.x / L - q.y / W, 1.0 - q.y / L - q.x / W);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);

                float sC = 1.0 - r;
                float sD = 1.0 - (abs(p.x) + abs(p.y));
                float sR = 1.0 - abs(r - 0.7) / 0.3;
                float sS = max(1.0 - (abs(p.x) * 4.0 + abs(p.y)), 1.0 - (abs(p.y) * 4.0 + abs(p.x)));
                sS = max(sS, 1.0 - r * 2.2);

                float2 ap = abs(p);
                float hexD = max(ap.x * 0.866 + ap.y * 0.5, ap.y);
                float sH = 1.0 - abs(hexD - 0.72) / 0.22;                       // 六角リング (紋章)
                float sT = max(max(1.0 - (ap.x * 9.0 + ap.y), 1.0 - (ap.y * 9.0 + ap.x)), 1.0 - r * 3.5); // キラキラ
                float sK = 1.0 - (ap.x * 2.6 + ap.y);                           // 結晶の破片
                // 強打の光 (X字の長い針 + 十字 + 短い針 + 中心の光)
                float sB = feNeedles(p, 0.7854, 1.0, 0.11);
                sB = max(sB, feNeedles(p, 0.0, 0.62, 0.06));
                sB = max(sB, feNeedles(p, 0.3927, 0.42, 0.045));
                sB = max(sB, feNeedles(p, 1.1781, 0.36, 0.04));
                sB = max(sB, 0.55 - r * 2.8) * 1.5;
                float sN = 1.0 - abs(r - 0.86) / 0.045;                         // 細いリング

                float s = _Shape < 0.5 ? sC : (_Shape < 1.5 ? sD : (_Shape < 2.5 ? sR :
                         (_Shape < 3.5 ? sS : (_Shape < 4.5 ? sH : (_Shape < 5.5 ? sT :
                         (_Shape < 6.5 ? sK : (_Shape < 7.5 ? sB : sN)))))));

                float n = feFbmP(p * _NoiseScale.xy + float2(0.0, _Time.y * _NoiseScale.z), 100000.0);
                s *= lerp(1.0, n * 1.6, _NoiseAmt);
                s -= (1.0 - i.color.a) + _Dissolve;

                float4 c = feToon(s, i.color.rgb);
                // 破片は左右で明暗を分けて結晶の面っぽく
                c.rgb *= (_Shape > 5.5 && _Shape < 6.5 && p.x < 0.0) ? 0.7 : 1.0;
                return c;
            }
            ENDCG
        }
    }
}
