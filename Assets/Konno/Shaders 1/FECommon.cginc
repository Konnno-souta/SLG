// =============================================================
//  FE Effects - 共通関数
//  ・トゥーン3段階 (芯 / 本体 / 縁) の色分け
//  ・プロシージャルノイズ (テクスチャ不要)
//  ・プリマルチプライドαで「通常合成」と「加算」を1つの値で切り替え
// =============================================================
#ifndef FE_COMMON_INCLUDED
#define FE_COMMON_INCLUDED

#include "UnityCG.cginc"

float4 _CoreColor;
float4 _MainColor;
float4 _MainColor2;
float4 _OutlineColor;
float  _CoreTh;
float  _MainTh;
float  _OutlineTh;
float  _Intensity;
float  _Additive;
float  _Dissolve;
float  _NoiseAmt;
float4 _NoiseScale;
float4 _GlowColor;   // Engage風の発光フチ (a = 強さ)

float feHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// X方向に周期を持つバリューノイズ (筒やリングの継ぎ目対策)
float feNoiseP(float2 p, float period)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float per = max(round(period), 1.0);
    float x0 = i.x - per * floor(i.x / per);
    float x1 = (i.x + 1.0) - per * floor((i.x + 1.0) / per);
    float a = feHash(float2(x0, i.y));
    float b = feHash(float2(x1, i.y));
    float c = feHash(float2(x0, i.y + 1.0));
    float d = feHash(float2(x1, i.y + 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float feFbmP(float2 p, float period)
{
    return feNoiseP(p, period) * 0.65
         + feNoiseP(p * 2.0 + float2(0.0, 17.3), period * 2.0) * 0.35;
}

// アンチエイリアス付きステップ (トゥーンの境界をギザギザにしない)
float feAA(float th, float v)
{
    float w = max(fwidth(v) * 0.75, 1e-4);
    return smoothstep(th - w, th + w, v);
}

// s : 形状値 (大きいほど内側)。しきい値で 縁 → 本体 → 芯 に塗り分ける
float4 feToon(float s, float3 tint)
{
    float o = feAA(_OutlineTh, s);
    float m = feAA(_MainTh, s);
    float c = feAA(_CoreTh, s);

    // 本体帯の中を Main → Main2 へグラデーション (エンゲージ風のツヤ)
    float k = saturate((s - _MainTh) / max(_CoreTh - _MainTh, 1e-3));
    float3 mainCol = lerp(_MainColor.rgb, _MainColor2.rgb, k);

    float3 col = lerp(_OutlineColor.rgb, mainCol * tint, m);
    col = lerp(col, _CoreColor.rgb * _Intensity, c);

    float a = o * lerp(_OutlineColor.a, _MainColor.a, m);
    a = lerp(a, o * _CoreColor.a, c);

    // 縁の外側にやわらかい発光 (加算)。幅は Outline Threshold で決まる
    float g = smoothstep(0.0, max(_OutlineTh, 1e-3), s) * _GlowColor.a * (1.0 - a);

    // Blend One OneMinusSrcAlpha 前提。αを0にすると加算になる
    return float4(col * a + _GlowColor.rgb * tint * g, a * (1.0 - _Additive));
}

#endif
