using System;
using UnityEngine;
using Kind = FEMeshPart.Kind;

/// <summary>
/// 攻撃エフェクト (攻撃者の位置) と 被弾エフェクト (当たった位置) を別々に作る。
/// FEBattleFX.PlayAttack / PlayHit から使う。
///
///  攻撃: 原点 = 攻撃者の足元 (矢は矢の先端) / +Z = 攻撃方向
///  被弾: 原点 = 当たった点 / +Z = 攻撃が抜けていく方向 / 斬線はローカルX方向 (Z軸まわりの回転で角度を変える)
/// </summary>
public static partial class FEEffectFactory
{
    public static readonly string[] AttackIds = { "ATK_Kesa", "ATK_Thrust", "ATK_Sweep", "ATK_Rising", "ATK_Arrow" };
    public static readonly string[] AttackNames = { "袈裟斬り", "刺突", "横薙ぎ", "斬り上げ", "弓" };
    public static readonly string[] HitIds = { "HIT_Slash", "HIT_SlashCrit", "HIT_Pierce", "HIT_PierceCrit", "HIT_Blunt", "HIT_BluntCrit" };
    public static readonly string[] HitNames = { "斬撃ヒット", "斬撃クリティカル", "刺突ヒット", "刺突クリティカル", "打撃ヒット", "打撃クリティカル" };

    // 攻撃エフェクトから取り除く「当たった瞬間」の部品
    static readonly string[] HitPartNames =
    {
        "Skill_CutLine", "Skill_CutFlash", "Skill_CutSparks", "Skill_TipFlash",
        "Thrust_TipSparks", "Thrust_HexEmblem", "Thrust_Glitter", "Thrust_Shards",
    };

    public static GameObject BuildBattle(string id)
    {
        var root = new GameObject(id);
        root.SetActive(false);
        var fx = root.AddComponent<FEEffect>();
        fx.effectId = id;

        switch (id)
        {
            // ---------------- 攻撃
            case "ATK_Kesa":   BuildSlash(fx, id, 115f, -65f, new Vector3(0f, 0f, 28f), 1.3f, 1.0f, 0.42f); MakeAttackOnly(fx, SkillWindup + 0.42f * 0.22f); break;
            case "ATK_Thrust": BuildThrust(fx, id); MakeAttackOnly(fx, SkillWindup + 0.08f); break;
            case "ATK_Sweep":  BuildSlash(fx, id, 150f, -70f, new Vector3(12f, 0f, 90f), 1.65f, 1.12f, 0.46f); MakeAttackOnly(fx, SkillWindup + 0.46f * 0.22f); break;
            case "ATK_Rising": BuildSlash(fx, id, -80f, 110f, new Vector3(0f, 0f, -28f), 1.3f, 1.0f, 0.42f); MakeAttackOnly(fx, SkillWindup + 0.42f * 0.22f); break;
            case "ATK_Arrow":  BuildArrow(fx, id); MakeAttackOnly(fx, 0f); break;   // 矢は飛ぶ距離で決まるので 0 (FEBattleFX 側で計算)
            // ---------------- 被弾
            case "HIT_Slash":      BuildHitSlash(fx, id, false); break;
            case "HIT_SlashCrit":  BuildHitSlash(fx, id, true); break;
            case "HIT_Pierce":     BuildHitPierce(fx, id, false); break;
            case "HIT_PierceCrit": BuildHitPierce(fx, id, true); break;
            case "HIT_Blunt":      BuildImpact(fx, id, true, false); break;
            case "HIT_BluntCrit":  BuildSmashImpact(fx, id); break;
            default:
                Debug.LogError("[FE] Unknown battle effect id: " + id);
                UnityEngine.Object.DestroyImmediate(root);
                return null;
        }
        return root;
    }

    // 当たった瞬間の部品・ヒットストップ・揺れを外して、攻撃側だけにする
    static void MakeAttackOnly(FEEffect fx, float hitTime)
    {
        foreach (var n in HitPartNames)
        {
            var t = fx.transform.Find(n);
            if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        fx.hitStopDuration = 0f;
        fx.shakeAmplitude = 0f;
        fx.hitTime = hitTime;
    }

    // ================================================================ 斬撃ヒット
    //  0.00 ヒットストップ + 斬線が走る + 十字の光 + 細いリング
    //  クリティカル: 斬線が X 字に2本 + 強打の光 + 青い発光 + 長めのヒットストップ
    static void BuildHitSlash(FEEffect fx, string id, bool crit)
    {
        var root = fx.transform;
        fx.lifetime = crit ? 0.8f : 0.55f;
        fx.shakeDelay = 0f; fx.shakeAmplitude = crit ? 0.22f : 0.1f; fx.shakeDuration = crit ? 0.25f : 0.14f;
        fx.hitStopDelay = 0f; fx.hitStopDuration = crit ? 0.1f : 0.05f; fx.hitStopTimeScale = crit ? 0.03f : 0.05f;

        var mCut = SlashMat(id + "_CutLine", SkillWhite, 0.2f).Th(0.55f, 0.25f, 0.07f)
            .F("_Symmetric", 1f).F("_TailLen", 1f).F("_HeadTaper", 0.1f).F("_TailPow", 0.8f)   // 尻尾を全長にして、線全体が標的を横切って見えるように
            .F("_NoiseAmt", 0.15f).F("_Intensity", 2.4f);
        var mFlash = ParticleMat(id + "_Flash", SkillWhite, 3, 0.25f).Th(0.55f, 0.3f, 0.1f).F("_Intensity", 2.2f);
        var mRing = ParticleMat(id + "_ThinRing", SkillWhite, 8, 0.3f).Th(0.75f, 0.35f, 0.08f).F("_Intensity", 2f);
        var mSpark = ParticleMat(id + "_Spark", SkillWhite, 1, 0.3f);
        var mShard = ParticleMat(id + "_Shard", Crystal, 6).Th(0.62f, 0.3f, 0.1f);

        float len = crit ? 3f : 2.2f;
        float width = crit ? 0.14f : 0.1f;
        CutLine(root, "Hit_CutLine1", mCut, 0f, len, width, 0f);
        if (crit) CutLine(root, "Hit_CutLine2", mCut, 70f, len * 0.9f, width, 0.05f);

        var flash = PS(root, "Hit_Flash", mFlash, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(flash, 0.14f, 0.14f, 0f, 0f, crit ? 2.2f : 1.1f, crit ? 2.2f : 1.1f, true);
        Burst(flash, 0f, 1, 1); FadeOut(flash, 0.3f);
        SizeLife(flash, t => 0.6f + 0.6f * EaseOut(t, 2f));
        Fudge(flash, -22f);

        var ring = PS(root, "Hit_ThinRing", mRing, Vector3.zero, Vector3.zero, 0.01f, 0.1f);
        Main(ring, 0.2f, 0.2f, 0f, 0f, crit ? 3f : 1.6f, crit ? 3f : 1.6f, true);
        Burst(ring, 0f, 1, 1); FadeOut(ring, 0.35f);
        SizeLife(ring, t => Mathf.Lerp(0.7f, 1.25f, EaseOut(t, 3f)));
        Fudge(ring, -15f);

        var sparks = PS(root, "Hit_Sparks", mSpark, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(sparks, 0.12f, 0.25f, 6f, 11f, 0.05f, 0.09f);
        ShapeSphere(sparks, 0.08f);
        Burst(sparks, 0f, crit ? 18 : 10, crit ? 24 : 14); FadeOut(sparks, 0.3f); Drag(sparks, 4f); Stretch(sparks, 3f, 0.04f);

        var shards = PS(root, "Hit_Shards", mShard, Vector3.zero, Vector3.zero, 0.02f, 0.1f);
        Main(shards, 0.3f, 0.5f, 2.5f, 5f, 0.1f, 0.18f, true);
        ShapeCone(shards, 60f, 0.1f);
        Burst(shards, 0f, crit ? 7 : 4, crit ? 10 : 6); FadeOut(shards, 0.5f); Drag(shards, 2f); Gravity(shards, 1.2f);
        Spin(shards, 14f);

        if (crit) AddCritExtras(root, id, SkillWhite);
    }

    // 斬線: ローカルX方向 (roll だけZ軸まわりに回す) に中心を通る白い直線
    static void CutLine(Transform root, string name, Material mat, float roll, float len, float width, float delay)
    {
        var rot = Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.Euler(0f, 90f, 0f);   // Line の +Z を X 方向へ
        var start = rot * new Vector3(0f, 0f, -len * 0.5f);
        var cut = P(root, name, Kind.Line, mat, start, rot.eulerAngles, delay, 0.32f);
        cut.length = len; cut.lineWidth = width; cut.segments = 32;
        cut.tracks.Add(T("_Head", t => t < 0.18f ? EaseOut(t / 0.18f, 3f) * 1.02f : Mathf.Lerp(1.02f, 2.02f, EaseIn(Seg(t, 0.45f, 1f), 1.5f))));
        cut.tracks.Add(T("_Dissolve", t => Seg(t, 0.5f, 1f) * 0.9f));
        Func<float, float> w = t => 1f + 0.5f * EaseOut(Seg(t, 0.15f, 0.6f), 2f);
        cut.scaleX = Fn(w); cut.scaleY = Fn(w);
    }

    // ================================================================ 刺突ヒット
    //  0.00 ヒットストップ + 十字の光 + 正面の衝撃リング + 突き抜ける火花
    //  クリティカル: 突き抜ける光の筋 + リング3枚が奥へ連なる + 強打の光
    static void BuildHitPierce(FEEffect fx, string id, bool crit)
    {
        var root = fx.transform;
        fx.lifetime = crit ? 0.8f : 0.5f;
        fx.shakeDelay = 0f; fx.shakeAmplitude = crit ? 0.22f : 0.1f; fx.shakeDuration = crit ? 0.25f : 0.14f;
        fx.hitStopDelay = 0f; fx.hitStopDuration = crit ? 0.1f : 0.05f; fx.hitStopTimeScale = crit ? 0.03f : 0.05f;

        var mFlash = ParticleMat(id + "_Flash", SkillWhite, 3, 0.25f).Th(0.55f, 0.3f, 0.1f).F("_Intensity", 2.2f);
        var mRing = ColumnMat(id + "_Ring", SkillWhite, 0.2f).Th(0.7f, 0.4f, 0.14f)
            .F("_BottomSoft", 0.5f).F("_TopSoft", 0.22f).F("_NoiseAmt", 0.3f)
            .V("_NoiseScale", new Vector4(12f, 1f, 0f, 3f));
        var mSpark = ParticleMat(id + "_Spark", SkillWhite, 1, 0.3f);
        var mShard = ParticleMat(id + "_Shard", Crystal, 6).Th(0.62f, 0.3f, 0.1f);

        var flash = PS(root, "Hit_Flash", mFlash, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(flash, 0.14f, 0.14f, 0f, 0f, crit ? 2.2f : 1.6f, crit ? 2.2f : 1.6f, true);
        Burst(flash, 0f, 1, 1); FadeOut(flash, 0.3f);
        SizeLife(flash, t => 0.6f + 0.6f * EaseOut(t, 2f));
        Fudge(flash, -22f);

        int rings = crit ? 3 : 1;
        for (int k = 0; k < rings; k++)
        {
            float max = (crit ? 1.4f : 1.1f) + 0.4f * k;
            var r = P(root, "Hit_Ring" + (k + 1), Kind.Ring, mRing, new Vector3(0f, 0f, 0.1f + 0.4f * k), new Vector3(90f, 0f, 0f), 0.025f * k, 0.3f);
            r.innerRadius = 0.72f; r.outerRadius = 1f; r.segments = 64;
            ScaleXZ(r, t => Mathf.Lerp(0.2f, max, EaseOut(t, 3f)));
            r.tracks.Add(T("_Dissolve", t => Seg(t, 0.25f, 1f)));
        }

        // 突き抜ける火花 (+Z 方向)
        var sparks = PS(root, "Hit_PierceSparks", mSpark, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(sparks, 0.12f, 0.22f, 9f, 15f, 0.05f, 0.09f);
        ShapeCone(sparks, 30f, 0.08f);
        Burst(sparks, 0f, crit ? 18 : 10, crit ? 24 : 14); FadeOut(sparks, 0.3f); Drag(sparks, 3f); Stretch(sparks, 3.5f, 0.04f);

        var shards = PS(root, "Hit_Shards", mShard, Vector3.zero, Vector3.zero, 0.02f, 0.1f);
        Main(shards, 0.3f, 0.5f, 3f, 6f, 0.1f, 0.16f, true);
        ShapeCone(shards, 45f, 0.1f);
        Burst(shards, 0f, crit ? 6 : 3, crit ? 9 : 5); FadeOut(shards, 0.5f); Drag(shards, 2f); Gravity(shards, 1.2f);
        Spin(shards, 14f);

        if (crit)
        {
            // 突き抜ける光の筋
            var mBeam = SlashMat(id + "_Beam", SkillWhite, 0.2f).Th(0.55f, 0.25f, 0.07f)
                .F("_Symmetric", 1f).F("_TailLen", 0.7f).F("_HeadTaper", 0.12f).F("_TailPow", 0.7f)
                .F("_NoiseAmt", 0.2f).F("_Intensity", 2.4f);
            var beam = P(root, "Hit_PierceBeam", Kind.Line, mBeam, new Vector3(0f, 0f, -0.2f), Vector3.zero, 0f, 0.35f);
            beam.length = 2.6f; beam.lineWidth = 0.22f; beam.segments = 32;
            beam.tracks.Add(T("_Head", t => t < 0.2f ? EaseOut(t / 0.2f, 3f) * 1.02f : Mathf.Lerp(1.02f, 1.7f, EaseIn(Seg(t, 0.4f, 1f), 1.5f))));
            beam.tracks.Add(T("_Dissolve", t => Seg(t, 0.45f, 1f) * 0.9f));
            AddCritExtras(root, id, SkillWhite);
        }
    }

    // クリティカル共通: 強打の光 (X字の針) + 青い発光 + キラキラ + 光
    static void AddCritExtras(Transform root, string id, Pal white)
    {
        var mBurst = ParticleMat(id + "_CritBurst", white, 7, 0.25f).Th(0.55f, 0.25f, 0.06f).F("_Intensity", 2.4f);
        var mGlow = ParticleMat(id + "_CritGlow", CritBlueGlow, 0, 1f).Th(9f, 9f, 1f);
        var mGlitter = GlitterMat(id + "_CritGlitter", Sparkle);

        var glow = PS(root, "Crit_Glow", mGlow, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(glow, 0.35f, 0.35f, 0f, 0f, 5.5f, 5.5f);
        Burst(glow, 0f, 1, 1); FadeOut(glow, 0.3f);
        SizeLife(glow, t => 0.8f + 0.3f * EaseOut(t, 2f));
        Fudge(glow, 40f);

        var burst = PS(root, "Crit_Burst", mBurst, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(burst, 0.2f, 0.2f, 0f, 0f, 3.8f, 3.8f);
        var bm = burst.main; bm.startRotation = new UnityEngine.ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        Burst(burst, 0f, 1, 1); FadeOut(burst, 0.35f);
        SizeLife(burst, t => 0.85f + 0.3f * EaseOut(t, 2f));
        Fudge(burst, -25f);

        GlitterBurst(root, "Crit_Glitter", mGlitter, Vector3.zero, 0.03f, 0.6f, 12, 16);
        LightFlash(root, new Vector3(0f, 0f, -0.4f), new Color(0.6f, 0.8f, 1f), 0f, 0.3f, 6f, 6f);
    }

    static readonly Pal CritBlueGlow = new Pal(Color.black, Color.black, new Color(0f, 0f, 0f, 0f), new Color(0.15f, 0.45f, 1.5f, 0.9f));
}
