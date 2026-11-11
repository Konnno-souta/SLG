using System;
using UnityEngine;
using MMC = UnityEngine.ParticleSystem.MinMaxCurve;
using Kind = FEMeshPart.Kind;

/// <summary>
/// EF01〜EF08 をコードで組み立てる (ファイアーエムブレム エンゲージ寄りの見た目)。
/// エディタの「Tools/FE Effects/1. Generate Prefabs」でプレハブ化される。
/// 返り値のルートは非アクティブ。位置を決めてから SetActive(true) で再生開始。
///
/// エンゲージ寄りにするための共通ルール:
///   ・彩度の高い本体色 + 暗色ではなく「鮮やかな縁」 + 縁の外に加算の発光フチ (Glow)
///   ・キラキラ (Twinkle) と結晶の破片 (Shard) を必ず散らす
///   ・着弾/発動の瞬間に六角形の紋章リングを出す
///
/// 座標の約束:
///   +Z = 攻撃方向 / +Y = 上 / 原点 = 攻撃者の足元 (EF03は着弾点, EF06は矢の先端, EF07/08は地面)
/// </summary>
public static partial class FEEffectFactory
{
    public static readonly string[] Ids = { "EF01", "EF02", "EF03", "EF04", "EF05", "EF06", "EF07", "EF08", "EF07B", "EF03B", "EF03C", "EF03S", "EF09", "EF01F", "EF02F", "EF04F", "EF05F" };
    public static readonly string[] Names = { "袈裟斬り", "刺突", "打撃", "横薙ぎ", "斬り上げ", "弓(刺突)", "光球爆発", "闇の渦", "光球爆発(渦なし)", "打撃(フラッシュなし)", "打撃(リング多重)", "打撃(スマブラ風)", "剣撃(FE風)", "袈裟斬り(厚みなし)", "刺突(厚みなし)", "横薙ぎ(厚みなし)", "斬り上げ(厚みなし)" };
    public static readonly string[] FileNames =
    {
        "EF01_KesaSlash", "EF02_SpearThrust", "EF03_Impact", "EF04_SweepSlash",
        "EF05_RisingSlash", "EF06_ArrowThrust", "EF07_OrbBurst", "EF08_DarkVortex",
        "EF07B_OrbBurstNoStorm", "EF03B_ImpactNoFlash", "EF03C_ImpactMultiRing", "EF03S_SmashImpact", "EF09_FESwordSlash",
        "EF01F_KesaSlashFlat", "EF02F_SpearThrustFlat", "EF04F_SweepSlashFlat", "EF05F_RisingSlashFlat"
    };

    public const string SlashShader = "FE/Slash";
    public const string ColumnShader = "FE/Column";
    public const string ParticleShader = "FE/Particle";

    // ---------------------------------------------------------------- palettes (Engage寄り)
    struct Pal
    {
        public Color core, main, outline, glow;
        public Pal(Color c, Color m, Color o, Color g) { core = c; main = m; outline = o; glow = g; }
    }

    // 青: 紺ではなく鮮やかな青の縁 + シアンの発光
    static readonly Pal Blue = new Pal(Color.white, new Color(0.1f, 0.62f, 1.25f), new Color(0.03f, 0.22f, 1f, 1f), new Color(0.2f, 0.75f, 1.5f, 0.9f));
    // 押し出しの側面 (暗い青の単色)
    static readonly Pal SlashSide = new Pal(new Color(0.32f, 0.56f, 0.98f), new Color(0.32f, 0.56f, 0.98f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
    // 斬撃・刺突: メイン白 / サブ青 (スマブラの剣の軌跡のような配色)
    //   本体はほぼ白、青は縁・発光・残像・側面にだけ使う
    static readonly Pal SwordWhite = new Pal(new Color(1.15f, 1.15f, 1.15f), new Color(1.05f, 1.08f, 1.15f), new Color(0.08f, 0.42f, 1.15f, 1f), new Color(0.2f, 0.6f, 1.5f, 0.9f));
    static readonly Pal SwordAfter = new Pal(Color.white, new Color(0.3f, 0.65f, 1.2f), new Color(0.15f, 0.4f, 1f, 1f), new Color(0.3f, 0.7f, 1.45f, 0.7f));
    static readonly Pal LightBlue = new Pal(Color.white, new Color(0.45f, 0.85f, 1.3f), new Color(0.08f, 0.35f, 1f, 1f), new Color(0.3f, 0.8f, 1.5f, 0.9f));
    static readonly Pal Wind = new Pal(Color.white, new Color(0.78f, 0.95f, 1.2f), new Color(0.4f, 0.8f, 1f, 0f), new Color(0.4f, 0.85f, 1.3f, 0.6f));
    static readonly Pal Sparkle = new Pal(Color.white, new Color(0.8f, 0.97f, 1.4f), new Color(0.25f, 0.7f, 1.2f, 1f), new Color(0.4f, 0.85f, 1.5f, 1f));
    static readonly Pal Crystal = new Pal(new Color(0.92f, 1f, 1f), new Color(0.35f, 0.8f, 1.2f), new Color(0.05f, 0.3f, 1f, 1f), new Color(0.3f, 0.8f, 1.5f, 0.8f));
    // 打撃: 指定どおり黒縁。発光フチはオレンジ
    static readonly Pal Impact = new Pal(Color.white, new Color(1.2f, 0.82f, 0.18f), new Color(0f, 0f, 0f, 1f), new Color(1.5f, 0.6f, 0.15f, 0.9f));
    static readonly Pal ImpactSpark = new Pal(Color.white, new Color(1.3f, 0.9f, 0.4f), new Color(1f, 0.4f, 0.1f, 1f), new Color(1.5f, 0.7f, 0.2f, 1f));
    // 衝撃コマ: 黒い本体 + 金の縁 (白フラッシュの後ろに一瞬だけ出す)
    static readonly Pal ImpactFrame = new Pal(new Color(0.02f, 0.02f, 0.03f), new Color(0.06f, 0.05f, 0.05f), new Color(1.2f, 0.8f, 0.2f, 1f), new Color(1.5f, 0.7f, 0.15f, 0.8f));
    // ソニックブーム: 白〜淡い黄の帯 + 黒縁
    static readonly Pal Boom = new Pal(Color.white, new Color(1.05f, 1f, 0.82f), new Color(0f, 0f, 0f, 1f), new Color(1.3f, 1.1f, 0.6f, 0.6f));
    // EF03S スマブラ風の強打
    static readonly Pal SmashWhite = new Pal(Color.white, new Color(1.3f, 1.25f, 1.1f), new Color(1.2f, 1f, 0.6f, 1f), new Color(1.6f, 1.3f, 0.7f, 0.9f));
    static readonly Pal SmashFire = new Pal(new Color(1.3f, 1.2f, 0.8f), new Color(1.4f, 1f, 0.15f), new Color(1.1f, 0.35f, 0.05f, 1f), new Color(1.6f, 0.5f, 0.1f, 0.8f));
    static readonly Pal SmashRedGlow = new Pal(Color.black, Color.black, new Color(0f, 0f, 0f, 0f), new Color(1.4f, 0.12f, 0.03f, 1f));
    static readonly Pal SmashOrangeGlow = new Pal(Color.black, Color.black, new Color(0f, 0f, 0f, 0f), new Color(1.6f, 0.55f, 0.08f, 0.9f));
    // EF09 FE風の剣撃: 白い芯 + 鮮やかなシアン + 明るいシアンの縁 (暗い縁取りなし)
    static readonly Pal FECyan = new Pal(Color.white, new Color(0.25f, 0.95f, 1.35f), new Color(0.1f, 0.6f, 1.3f, 1f), new Color(0.2f, 0.9f, 1.6f, 0.9f));
    static readonly Pal FERedStreak = new Pal(new Color(1.3f, 0.8f, 0.5f), new Color(1.2f, 0.15f, 0.08f), new Color(0.5f, 0.02f, 0.02f, 1f), new Color(1.4f, 0.2f, 0.05f, 0.7f));
    static readonly Pal Gold = new Pal(new Color(1f, 1f, 0.94f), new Color(1.3f, 0.88f, 0.3f), new Color(1f, 0.45f, 0.05f, 1f), new Color(1.5f, 0.75f, 0.25f, 0.9f));
    static readonly Pal Holy = new Pal(Color.white, new Color(1.2f, 1.1f, 0.85f), new Color(1f, 0.75f, 0.3f, 1f), new Color(1.5f, 1.1f, 0.5f, 0.9f));
    static readonly Pal GoldWind = new Pal(Color.white, new Color(1.2f, 1f, 0.65f), new Color(1f, 0.7f, 0.3f, 0.5f), new Color(1.5f, 1f, 0.4f, 0.7f));
    // 闇: 黒い本体 + マゼンタ紫に光る縁
    static readonly Pal Dark = new Pal(new Color(0.01f, 0f, 0.02f), new Color(0.15f, 0.02f, 0.28f), new Color(0.9f, 0.25f, 1.3f, 1f), new Color(1.2f, 0.2f, 1.5f, 0.9f));
    static readonly Pal DarkCrystal = new Pal(new Color(1f, 0.85f, 1f), new Color(0.7f, 0.25f, 1.1f), new Color(0.2f, 0f, 0.4f, 1f), new Color(1.2f, 0.3f, 1.5f, 1f));

    // ---------------------------------------------------------------- entry
    // false の間は斬撃・刺突の「厚み」(押し出しの側面・断面のくさび) を作らない
    static bool buildThick = true;

    public static GameObject Build(string id)
    {
        int idx = Array.IndexOf(Ids, id);
        if (idx < 0) { Debug.LogError("[FE] Unknown effect id: " + id); return null; }

        var root = new GameObject(FileNames[idx]);
        root.SetActive(false);
        var fx = root.AddComponent<FEEffect>();
        fx.effectId = id;
        fx.displayName = Names[idx];

        switch (idx)
        {
            case 0: BuildSlash(fx, "EF01", 115f, -65f, new Vector3(0f, 0f, 28f), 1.3f, 1.0f, 0.42f); break;
            case 1: BuildThrust(fx, "EF02"); break;
            case 2: BuildImpact(fx, "EF03", true, false); break;
            case 3: BuildSlash(fx, "EF04", 150f, -70f, new Vector3(12f, 0f, 90f), 1.65f, 1.12f, 0.46f); break;
            case 4: BuildSlash(fx, "EF05", -80f, 110f, new Vector3(0f, 0f, -28f), 1.3f, 1.0f, 0.42f); break;
            case 5: BuildArrow(fx, "EF06"); break;
            case 6: BuildOrbBurst(fx, "EF07", true); break;
            case 7: BuildDarkVortex(fx); break;
            case 8: BuildOrbBurst(fx, "EF07B", false); break;
            case 9: BuildImpact(fx, "EF03B", false, false); break;
            case 10: BuildImpact(fx, "EF03C", false, true); break;
            case 11: BuildSmashImpact(fx, "EF03S"); break;
            case 12: BuildFESword(fx, "EF09"); break;
            // 厚みなし版: 側面の押し出しと断面のくさびを外す (奥行きのらせん・剣技レイヤーはそのまま)
            case 13: buildThick = false; BuildSlash(fx, "EF01F", 115f, -65f, new Vector3(0f, 0f, 28f), 1.3f, 1.0f, 0.42f); break;
            case 14: buildThick = false; BuildThrust(fx, "EF02F"); break;
            case 15: buildThick = false; BuildSlash(fx, "EF04F", 150f, -70f, new Vector3(12f, 0f, 90f), 1.65f, 1.12f, 0.46f); break;
            case 16: buildThick = false; BuildSlash(fx, "EF05F", -80f, 110f, new Vector3(0f, 0f, -28f), 1.3f, 1.0f, 0.42f); break;
        }
        buildThick = true;
        return root;
    }

    // ================================================================ EF01 / EF04 / EF05
    // 弧の斬撃。start→end の角度で振る (YZ平面, 0°=前, 90°=上)。euler で傾ける
    static void BuildSlash(FEEffect fx, string id, float start, float end, Vector3 euler, float radius, float width, float dur)
    {
        var root = fx.transform;
        fx.lifetime = dur + 0.35f;
        fx.shakeDelay = 0.06f; fx.shakeAmplitude = 0.05f; fx.shakeDuration = 0.12f;

        var pos = new Vector3(0f, 1.05f, 0.35f);
        float sgn = Mathf.Sign(end - start);

        // 太め: 尻尾が細るのを遅らせる (TailPow 1.1 → 0.7)
        // 白メイン・青サブ: 青い縁の帯を 0.06〜0.3 に広げる (白は 0.3 から上)
        var mMain = SlashMat(id + "_SlashWhite", SwordWhite).Th(0.5f, 0.3f, 0.06f).F("_TailPow", 0.55f).F("_Shade", 0.3f).F("_BackShade", 0.2f);
        var mWind = SlashMat(id + "_SlashWind", Wind, 0.5f)
            .F("_TailLen", 0.95f).F("_NoiseAmt", 0.6f).F("_OuterSoft", 0.3f)
            .V("_NoiseScale", new Vector4(9f, 1.5f, 6f, 0f));
        var mSpark = ParticleMat(id + "_SparkBlue", Blue, 1);
        var mWindP = ParticleMat(id + "_SparkWind", Wind, 1, 0.6f);
        var mGlitter = GlitterMat(id + "_Glitter", Sparkle);
        var mShard = ParticleMat(id + "_Shard", Crystal, 6).Th(0.62f, 0.3f, 0.1f);

        // Engage: 三日月の後ろに広がる光のオーラ (加算)
        //   帯は描かず (しきい値9) 発光フチだけを広く出す
        var mAura = SlashMat(id + "_SlashAura", Blue, 1f).Th(9f, 9f, 0.5f)
            .V("_OutlineColor", new Vector4(0f, 0f, 0f, 0f))
            .F("_OuterSoft", 0.6f).F("_NoiseAmt", 0.45f).F("_TailLen", 0.85f).F("_TailPow", 0.55f);
        var aura = P(root, "Slash_Aura", Kind.Arc, mAura, pos, euler, 0f, dur);
        Arc(aura, radius + 0.12f, width * 1.6f, start, end, 0.18f);
        SlashTracks(aura, 0.85f);

        // 立体感: らせん状の奥行き (振り始めが奥 → 振り終わりが手前)
        float depth3D = radius * 0.25f;
        Arc3D(aura, depth3D, 0f, 0f);

        // 押し出しの側面: 同じ形のコピーをカメラ基準で下・奥へずらして暗い単色で敷く
        //   厚み強化: ずらし量 (下 0.17→0.4 x幅 / 奥 0.06→0.18m) と層数 8→12
        if (buildThick)
        {
            Extrude(root, id + "_SlashSide", "Slash_Side", SlashSide, new Vector3(0f, -width * 0.4f, 0.18f), 12,
                m => m.F("_TailPow", 0.55f),
                k => { var p = P(root, "Slash_Side" + k, Kind.Arc, null, pos, euler, 0f, dur);
                       Arc(p, radius, width, start, end, 0.15f); Arc3D(p, depth3D, 0f, 0f); SlashTracks(p, 0.75f); return p; });
        }

        // 本体: 青い三日月
        var a = P(root, "Slash_Main", Kind.Arc, mMain, pos, euler, 0f, dur);
        Arc(a, radius, width, start, end, 0.15f);
        // 断面をくさび形に (刃側が盛り上がる) → 斜めから見ても板に見えない
        Arc3D(a, depth3D, 0f, buildThick ? width * 0.3f : 0f);
        SlashTracks(a, 0.75f);

        // 外側の風の筋 (少し遅れて、少し長く)
        var b = P(root, "Slash_WindOuter", Kind.Arc, mWind, pos, euler, 0.03f, dur);
        Arc(b, radius + 0.2f, 0.18f, start - 6f * sgn, end + 12f * sgn, 0.3f);
        Arc3D(b, depth3D * 1.1f, 0f, 0f);
        SlashTracks(b, 0.95f);

        // 内側の風の筋
        var c = P(root, "Slash_WindInner", Kind.Arc, mWind, pos, euler, 0.05f, dur * 0.95f);
        Arc(c, radius - width - 0.06f, 0.09f, start + 8f * sgn, end - 4f * sgn, 0.1f);
        Arc3D(c, depth3D * 0.9f, 0f, 0f);
        SlashTracks(c, 0.95f);

        // 弧に沿って飛ぶ火花と風の線
        float sweep = dur * 0.3f;
        var sparks = PS(root, "Slash_Sparks", mSpark, pos, euler, 0.01f, sweep);
        Main(sparks, 0.18f, 0.35f, 2f, 5f, 0.05f, 0.11f);
        ArcShape(sparks, radius - width * 0.3f, start, end, sweep, true);
        Rate(sparks, 70f / sweep * 0.25f);
        FadeOut(sparks, 0.4f); Drag(sparks, 4f); Stretch(sparks, 2.5f, 0.06f);
        Orbit(sparks, -sgn * 4f, 0f);

        var lines = PS(root, "Slash_WindLines", mWindP, pos, euler, 0.02f, sweep);
        Main(lines, 0.15f, 0.28f, 0.3f, 0.8f, 0.035f, 0.06f);
        ArcShape(lines, radius + 0.1f, start, end, sweep, true);
        Rate(lines, 40f / sweep * 0.25f);
        FadeOut(lines, 0.3f); Stretch(lines, 6f, 0.08f);
        Orbit(lines, -sgn * 7f, 0f);

        // Engage: 軌跡に残るキラキラ
        var glit = PS(root, "Slash_Glitter", mGlitter, pos, euler, 0.03f, sweep);
        Main(glit, 0.35f, 0.65f, 0.2f, 0.8f, 0.1f, 0.18f);
        ArcShape(glit, radius - width * 0.15f, start, end, sweep, true);
        Rate(glit, 16f / sweep);
        FadeOut(glit, 0.55f); Twinkle(glit);
        Orbit(glit, -sgn * 1.5f, 0f);

        // Engage: 振り抜きの途中で砕けて飛ぶ結晶片
        var shards = PS(root, "Slash_Shards", mShard, pos, euler, sweep * 0.5f, 0.1f);
        Main(shards, 0.3f, 0.55f, 2.5f, 5f, 0.12f, 0.22f);
        ArcShape(shards, radius - width * 0.2f, start, end, sweep, false);
        Burst(shards, 0f, 5, 8);
        FadeOut(shards, 0.45f); Drag(shards, 3f); Gravity(shards, 0.6f); Stretch(shards, 1.4f, 0f);

        // FE の剣技らしさ: 溜め・刃の線・残像・斬線・ヒットストップ
        AddSlashSkill(fx, id, dur);
    }

    static void SlashTracks(FEMeshPart p, float tail)
    {
        // 前半30%で一気に振り抜き → 残像が尻尾から縮みつつ削れて消える
        p.tracks.Add(T("_Head", t => t < 0.32f
            ? EaseOut(t / 0.32f, 2.5f) * 1.04f
            : Mathf.Lerp(1.04f, 1f + tail, EaseIn(Seg(t, 0.32f, 1f), 1.6f))));
        p.tracks.Add(T("_Dissolve", t => Mathf.Pow(Seg(t, 0.4f, 1f), 1.2f) * 0.85f));
    }

    // ================================================================ 剣技レイヤー (EF01 / EF04 / EF05 / EF02 共通)
    //  0.00        溜め: 振り始めの剣先がキラッと光る
    //  0.09〜      振り (既存の斬撃を丸ごと 0.09 秒うしろへずらす)
    //              + 刃の線 (外側の細く鋭い白線) + 残像2枚
    //  振りの22%   斬線: 斬った場所に白い直線が走る + 十字の光 + 火花 + 短いヒットストップ
    const float SkillWindup = 0.09f;

    static readonly Pal SkillWhite = new Pal(Color.white, new Color(0.9f, 0.97f, 1.25f), new Color(0.35f, 0.7f, 1.2f, 1f), new Color(0.5f, 0.85f, 1.6f, 0.9f));
    static readonly Pal SkillEdge = new Pal(Color.white, new Color(0.85f, 0.95f, 1.3f), new Color(0.4f, 0.75f, 1.2f, 0f), new Color(0.5f, 0.85f, 1.6f, 0.8f));

    static void AddSlashSkill(FEEffect fx, string id, float dur)
    {
        var root = fx.transform;
        var main = root.Find("Slash_Main") ? root.Find("Slash_Main").GetComponent<FEMeshPart>() : null;
        if (main == null) { Debug.LogWarning("[FE] Slash_Main が見つからないため剣技レイヤーを省略: " + id); return; }

        ShiftTiming(fx, SkillWindup);
        float sw = SkillWindup;
        float start = main.startAngle, end = main.endAngle;
        float sgn = Mathf.Sign(end - start);
        var pos = main.transform.localPosition;
        var R = main.transform.localRotation;
        float cutTime = sw + dur * 0.22f;

        var mGlint = ParticleMat(id + "_SkillGlint", Sparkle, 5, 0.4f).Th(0.5f, 0.28f, 0.1f).F("_Intensity", 2.4f);
        var mFlash = ParticleMat(id + "_SkillFlash", SkillWhite, 3, 0.25f).Th(0.55f, 0.3f, 0.1f).F("_Intensity", 2.2f);
        var mSpark = ParticleMat(id + "_SkillSpark", SkillWhite, 1, 0.3f);
        var mEdge = SlashMat(id + "_SkillEdge", SkillEdge, 0.3f).Th(0.5f, 0.2f, 0.05f)
            .F("_TailLen", 0.55f).F("_OuterSoft", 0.35f).F("_NoiseAmt", 0f).F("_Intensity", 2.4f);
        var mCut = SlashMat(id + "_SkillCut", SkillWhite, 0.2f).Th(0.55f, 0.25f, 0.07f)
            .F("_Symmetric", 1f).F("_TailLen", 0.6f).F("_HeadTaper", 0.1f).F("_TailPow", 0.8f)
            .F("_NoiseAmt", 0.15f).F("_Intensity", 2.4f);

        // ---------- 溜め: 振り始めの剣先がキラッ
        var glintPos = pos + R * ArcPoint(main, 0f, main.radius);
        var glint = PS(root, "Skill_Glint", mGlint, glintPos, Vector3.zero, 0f, 0.1f);
        Main(glint, 0.16f, 0.16f, 0f, 0f, 1.1f, 1.1f);
        var gm = glint.main; gm.startRotation = 0f;
        Burst(glint, 0f, 1, 1); FadeOut(glint, 0.6f);
        SizeLife(glint, t => t < 0.35f ? EaseOut(t / 0.35f, 2f) * 1.2f : Mathf.Lerp(1.2f, 0f, EaseIn(Seg(t, 0.35f, 1f), 2f)), 40);
        Fudge(glint, -20f);

        // ---------- 残像 x2: 本体を複製して少し遅らせ、半透明の加算で重ねる
        var mAfter = new Material(main.GetComponent<MeshRenderer>().sharedMaterial) { name = id + "_SkillAfterimage" };
        ApplyAfterimageStyle(mAfter);
        for (int k = 1; k <= 2; k++)
        {
            var go = UnityEngine.Object.Instantiate(main.gameObject, root);
            go.name = "Skill_Afterimage" + k;
            var p = go.GetComponent<FEMeshPart>();
            p.delay = main.delay + 0.035f * k;
            p.radius = main.radius * (1f - 0.06f * k);
            p.width = main.width * (1f - 0.18f * k);
            go.transform.localRotation = main.transform.localRotation * Quaternion.AngleAxis(-sgn * 7f * k, Vector3.right);
            go.GetComponent<MeshRenderer>().sharedMaterial = mAfter;
        }

        // ---------- 刃の線: 本体の外周に沿う細い白線 (先端が一番鋭く光る)
        {
            var go = UnityEngine.Object.Instantiate(main.gameObject, root);
            go.name = "Skill_BladeEdge";
            var p = go.GetComponent<FEMeshPart>();
            p.width = 0.065f;
            p.radius = main.radius + 0.01f;
            p.thickness = 0f;
            p.tracks.Clear();
            SlashTracks(p, 0.55f);
            mEdge.renderQueue = 3005;   // 本体の白の上に刃の線
            go.GetComponent<MeshRenderer>().sharedMaterial = mEdge;
        }

        // ---------- 斬線: 振りの中ほどの接線方向に白い直線
        float mid = Mathf.Lerp(start, end, 0.5f) * Mathf.Deg2Rad;
        var cutCenter = pos + R * ArcPoint(main, 0.5f, main.radius);
        var tangent = (R * new Vector3(0f, Mathf.Cos(mid), -Mathf.Sin(mid)) * sgn).normalized;
        const float cutLen = 3f;
        var cutRot = Quaternion.LookRotation(tangent, R * Vector3.right);
        var cut = P(root, "Skill_CutLine", Kind.Line, mCut, cutCenter - tangent * (cutLen * 0.5f), cutRot.eulerAngles, cutTime, 0.32f);
        cut.length = cutLen; cut.lineWidth = 0.1f; cut.segments = 32;
        cut.tracks.Add(T("_Head", t => t < 0.18f ? EaseOut(t / 0.18f, 3f) * 1.02f : Mathf.Lerp(1.02f, 1.6f, EaseIn(Seg(t, 0.45f, 1f), 1.5f))));
        cut.tracks.Add(T("_Dissolve", t => Seg(t, 0.5f, 1f) * 0.9f));
        Func<float, float> cutW = t => 1f + 0.5f * EaseOut(Seg(t, 0.15f, 0.6f), 2f);
        cut.scaleX = Fn(cutW); cut.scaleY = Fn(cutW);

        // ---------- 斬った瞬間: 十字の光 + 火花 + ヒットストップ
        var flash = PS(root, "Skill_CutFlash", mFlash, cutCenter, Vector3.zero, cutTime, 0.1f);
        Main(flash, 0.14f, 0.14f, 0f, 0f, 1.6f, 1.6f, true);
        Burst(flash, 0f, 1, 1); FadeOut(flash, 0.3f);
        SizeLife(flash, t => 0.6f + 0.6f * EaseOut(t, 2f));
        Fudge(flash, -22f);

        var sparks = PS(root, "Skill_CutSparks", mSpark, cutCenter, Vector3.zero, cutTime, 0.1f);
        Main(sparks, 0.12f, 0.25f, 6f, 11f, 0.05f, 0.09f);
        ShapeSphere(sparks, 0.08f);
        Burst(sparks, 0f, 10, 14); FadeOut(sparks, 0.3f); Drag(sparks, 4f); Stretch(sparks, 3f, 0.04f);

        fx.shakeDelay = cutTime; fx.shakeAmplitude = Mathf.Max(fx.shakeAmplitude, 0.09f);
        fx.hitStopDelay = cutTime; fx.hitStopDuration = 0.05f; fx.hitStopTimeScale = 0.05f;
    }

    // 刺突の剣技: 溜めの光 + 残像2本 + 先端の十字光とヒットストップ
    static void AddThrustSkill(FEEffect fx, string id, Vector3 tip)
    {
        var root = fx.transform;
        var coreT = root.Find("Thrust_Core");
        var core = coreT ? coreT.GetComponent<FEMeshPart>() : null;
        if (core == null) { Debug.LogWarning("[FE] Thrust_Core が見つからないため剣技レイヤーを省略: " + id); return; }

        ShiftTiming(fx, SkillWindup);
        float hitTime = SkillWindup + 0.08f;

        var mGlint = ParticleMat(id + "_SkillGlint", Sparkle, 5, 0.4f).Th(0.5f, 0.28f, 0.1f).F("_Intensity", 2.4f);
        var mFlash = ParticleMat(id + "_SkillFlash", SkillWhite, 3, 0.25f).Th(0.55f, 0.3f, 0.1f).F("_Intensity", 2.2f);

        // 溜め: 構えた槍先 (軌跡の後端の少し前) がキラッ
        var glintPos = core.transform.localPosition + new Vector3(0f, 0f, 0.35f);
        var glint = PS(root, "Skill_Glint", mGlint, glintPos, Vector3.zero, 0f, 0.1f);
        Main(glint, 0.16f, 0.16f, 0f, 0f, 1f, 1f);
        var gm = glint.main; gm.startRotation = 0f;
        Burst(glint, 0f, 1, 1); FadeOut(glint, 0.6f);
        SizeLife(glint, t => t < 0.35f ? EaseOut(t / 0.35f, 2f) * 1.2f : Mathf.Lerp(1.2f, 0f, EaseIn(Seg(t, 0.35f, 1f), 2f)), 40);

        // 残像: 上下に少しずらした細い突きの筋
        var mAfter = new Material(core.GetComponent<MeshRenderer>().sharedMaterial) { name = id + "_SkillAfterimage" };
        ApplyAfterimageStyle(mAfter);
        for (int k = 0; k < 2; k++)
        {
            var go = UnityEngine.Object.Instantiate(core.gameObject, root);
            go.name = "Skill_Afterimage" + (k + 1);
            var p = go.GetComponent<FEMeshPart>();
            p.delay = core.delay + 0.03f * (k + 1);
            p.lineWidth = core.lineWidth * 0.6f;
            p.length = core.length * 0.9f;
            go.transform.localPosition = core.transform.localPosition + new Vector3(0f, k == 0 ? 0.16f : -0.16f, 0.1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mAfter;
        }

        // 突き刺さった瞬間の十字光
        var flash = PS(root, "Skill_TipFlash", mFlash, tip, Vector3.zero, hitTime, 0.1f);
        Main(flash, 0.14f, 0.14f, 0f, 0f, 1.7f, 1.7f, true);
        Burst(flash, 0f, 1, 1); FadeOut(flash, 0.3f);
        SizeLife(flash, t => 0.6f + 0.6f * EaseOut(t, 2f));
        Fudge(flash, -22f);

        fx.shakeDelay = hitTime;
        fx.hitStopDelay = hitTime; fx.hitStopDuration = 0.05f; fx.hitStopTimeScale = 0.05f;
    }

    // 弧の上の点 (FEMeshBuilder.Arc と同じ式)。t: 0=振り始め 1=振り終わり
    static Vector3 ArcPoint(FEMeshPart p, float t, float r)
    {
        float a = Mathf.Lerp(p.startAngle, p.endAngle, t) * Mathf.Deg2Rad;
        float x = p.bend * Mathf.Sin(t * Mathf.PI) + p.depth * (t - 0.5f);
        return new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * r + Vector3.right * x;   // 部品のローカル座標
    }

    // 既存の部品をまとめて dt 秒うしろへずらす (溜めの時間を作る)
    static void ShiftTiming(FEEffect fx, float dt)
    {
        foreach (var p in fx.GetComponentsInChildren<FEMeshPart>(true)) p.delay += dt;
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            m.startDelay = m.startDelay.constant + dt;
        }
        foreach (var l in fx.GetComponentsInChildren<FELightFlash>(true)) l.delay += dt;
        foreach (var s in fx.GetComponentsInChildren<FESpiralMotion>(true)) s.delay += dt;
        fx.shakeDelay += dt;
        fx.hitStopDelay += dt;
        fx.lifetime += dt;
    }

    // ================================================================ EF02 刺突
    static void BuildThrust(FEEffect fx, string id)
    {
        var root = fx.transform;
        fx.lifetime = 0.75f;
        fx.shakeDelay = 0.06f; fx.shakeAmplitude = 0.07f; fx.shakeDuration = 0.1f;
        const float y = 1.1f;
        var tip = new Vector3(0f, y, 2.3f);

        var mCore = SlashMat(id + "_ThrustCore", SwordWhite).Th(0.5f, 0.3f, 0.06f)
            .F("_Symmetric", 1f).F("_TailLen", 0.7f).F("_HeadTaper", 0.15f).F("_TailPow", 0.55f)
            .F("_NoiseAmt", 0.4f).V("_NoiseScale", new Vector4(8f, 1f, 6f, 0f))
            .F("_Shade", 0.5f).F("_BackShade", 0.2f);
        var mCone = WindConeMat(id + "_WindCone");
        var mRing = ColumnMat(id + "_Ring", Blue)
            .F("_BottomSoft", 0.5f).F("_TopSoft", 0.25f).F("_NoiseAmt", 0.35f)
            .V("_NoiseScale", new Vector4(10f, 1f, 0f, 2f));
        var mSpark = ParticleMat(id + "_SparkBlue", Blue, 1);
        var mWindP = ParticleMat(id + "_SparkWind", Wind, 1, 0.6f);
        var mHex = ParticleMat(id + "_HexEmblem", Sparkle, 4, 0.3f).Th(0.8f, 0.45f, 0.15f);
        var mGlitter = GlitterMat(id + "_Glitter", Sparkle);
        var mShard = ParticleMat(id + "_Shard", Crystal, 6);

        // 槍の軌跡 (先端が尖った光の筋)
        var c = P(root, "Thrust_Core", Kind.Line, mCore, new Vector3(0f, y, -0.3f), Vector3.zero, 0f, 0.4f);
        c.length = 2.7f; c.lineWidth = 0.75f; c.segments = 32;
        c.lineFins = 4;   // 4枚の帯を米字に重ねて、どの角度からも太い槍の光に見せる
        c.tracks.Add(T("_Head", t => t < 0.22f ? EaseOut(t / 0.22f, 2.5f) : 1f + Seg(t, 0.22f, 1f) * 0.72f));
        c.tracks.Add(T("_Dissolve", t => Seg(t, 0.35f, 1f) * 0.85f));

        // 押し出しの側面: 槍の光と同じ形のコピーをカメラ基準で下・奥へずらして敷く
        if (buildThick)
        {
            Extrude(root, id + "_ThrustSide", "Thrust_Side", SlashSide, new Vector3(0f, -0.22f, 0.14f), 12,
                m => m.F("_Symmetric", 1f).F("_TailLen", 0.7f).F("_HeadTaper", 0.15f).F("_TailPow", 0.55f),
                k => { var p = P(root, "Thrust_Side" + k, Kind.Line, null, new Vector3(0f, y, -0.3f), Vector3.zero, 0f, 0.4f);
                       p.length = 2.7f; p.lineWidth = 0.75f; p.segments = 32; p.lineFins = 4;
                       p.tracks.Add(T("_Head", t => t < 0.22f ? EaseOut(t / 0.22f, 2.5f) : 1f + Seg(t, 0.22f, 1f) * 0.72f));
                       p.tracks.Add(T("_Dissolve", t => Seg(t, 0.35f, 1f) * 0.85f));
                       return p; });
        }

        // 風切りの円錐 (後ろが広く、先端が細い)
        var cone = P(root, "Thrust_WindCone", Kind.Tube, mCone, new Vector3(0f, y, -0.1f), new Vector3(90f, 0f, 0f), 0.02f, 0.4f);
        cone.bottomRadius = 0.72f; cone.topRadius = 0.1f; cone.height = 2.4f; cone.segments = 32;
        cone.tracks.Add(T("_Fill", t => EaseOut(Seg(t, 0f, 0.28f), 2.5f) * 1.1f));
        cone.tracks.Add(T("_Dissolve", t => Seg(t, 0.3f, 1f) * 0.9f));
        cone.scaleY = Fn(t => 0.7f + 0.3f * EaseOut(t, 2f));

        // 先端の衝撃リング x2
        var r1 = P(root, "Thrust_Ring1", Kind.Ring, mRing, tip, new Vector3(90f, 0f, 0f), 0.07f, 0.28f);
        r1.innerRadius = 0.55f; r1.outerRadius = 1f;
        ScaleXZ(r1, t => Mathf.Lerp(0.25f, 1.8f, EaseOut(t, 3f)));
        r1.tracks.Add(T("_Dissolve", t => Seg(t, 0.2f, 1f)));

        var r2 = P(root, "Thrust_Ring2", Kind.Ring, mRing, new Vector3(0f, y, 1.9f), new Vector3(90f, 0f, 0f), 0.13f, 0.25f);
        r2.innerRadius = 0.6f; r2.outerRadius = 1f;
        ScaleXZ(r2, t => Mathf.Lerp(0.25f, 1.25f, EaseOut(t, 3f)));
        r2.tracks.Add(T("_Dissolve", t => Seg(t, 0.2f, 1f)));

        // 前に飛ぶスピード線
        var lines = PS(root, "Thrust_SpeedLines", mWindP, new Vector3(0f, y, 0f), Vector3.zero, 0.02f, 0.1f);
        Main(lines, 0.12f, 0.2f, 12f, 18f, 0.04f, 0.07f);
        ShapeCone(lines, 3f, 0.35f);
        Burst(lines, 0f, 10, 14); FadeOut(lines, 0.3f); Stretch(lines, 6f, 0.02f);

        // 先端の火花
        var sp = PS(root, "Thrust_TipSparks", mSpark, tip, Vector3.zero, 0.07f, 0.1f);
        Main(sp, 0.15f, 0.3f, 3f, 7f, 0.05f, 0.1f);
        ShapeSphere(sp, 0.1f);
        Burst(sp, 0f, 8, 12); FadeOut(sp, 0.4f); Drag(sp, 3f); Stretch(sp, 2.5f, 0.06f);

        // Engage: 先端の六角紋章 + キラキラ + 結晶片
        HexEmblem(root, "Thrust_HexEmblem", mHex, tip, 0.07f, 0.9f, 1.8f);
        GlitterBurst(root, "Thrust_Glitter", mGlitter, tip, 0.08f, 0.35f, 8, 12);
        var sh = PS(root, "Thrust_Shards", mShard, tip, Vector3.zero, 0.07f, 0.1f);
        Main(sh, 0.3f, 0.5f, 2.5f, 5f, 0.12f, 0.2f);
        ShapeCone(sh, 50f, 0.1f);
        Burst(sh, 0f, 5, 7); FadeOut(sh, 0.45f); Drag(sh, 3f); Gravity(sh, 0.6f); Stretch(sh, 1.4f, 0f);

        // FE の剣技らしさ: 溜め・残像・先端の十字光・ヒットストップ
        AddThrustSkill(fx, id, tip);
    }

    // ================================================================ EF03 / EF03B 打撃
    //  原点 = 着弾点 / +Z = 殴った方向 (力が抜けていく方向)
    //  0.00  ヒットストップ (0.08秒) + 黒い衝撃コマ + 白い十字フラッシュ
    //  0.00〜 前方へソニックブーム (リング3枚が奥へ連なって広がる + 水蒸気の円錐)
    //  0.02〜 黒縁の爆発・トゲ・破片が「殴った方向」へ偏って吹き飛ぶ
    static void BuildImpact(FEEffect fx, string id, bool withFlash, bool multiRing)
    {
        var root = fx.transform;
        fx.lifetime = 0.75f;
        fx.shakeDelay = 0f; fx.shakeAmplitude = 0.26f; fx.shakeDuration = 0.22f;
        fx.hitStopDelay = 0f; fx.hitStopDuration = 0.08f; fx.hitStopTimeScale = 0.03f;

        var mStar = ParticleMat(id + "_Star", Impact, 3).Th(0.55f, 0.3f, 0.12f);
        var mFrame = ParticleMat(id + "_ImpactFrame", ImpactFrame, 3).Th(0.5f, 0.25f, 0.08f);
        var mPuff = ParticleMat(id + "_Puff", Impact, 0).Th(0.62f, 0.38f, 0.18f)
            .F("_NoiseAmt", 0.45f).V("_NoiseScale", new Vector4(2.5f, 2.5f, 1.5f, 0f));
        var mSpike = ParticleMat(id + "_Spike", Impact, 1).Th(0.55f, 0.3f, 0.1f);
        var mRing = ParticleMat(id + "_Ring", Impact, 2).Th(0.8f, 0.45f, 0.15f);
        var mHex = ParticleMat(id + "_HexEmblem", ImpactSpark, 4, 0.3f).Th(0.8f, 0.45f, 0.15f);
        var mGlitter = GlitterMat(id + "_Glitter", ImpactSpark);
        var mBoomLine = ParticleMat(id + "_BoomLines", Boom, 1, 0.3f).Th(0.55f, 0.3f, 0.1f);
        // ソニックブームの輪: 白〜淡黄の帯 + 黒い縁 (マンガ的な衝撃波)
        var mBoom = ColumnMat(id + "_SonicBoom", Boom).Th(0.7f, 0.4f, 0.14f)
            .F("_BottomSoft", 0.5f).F("_TopSoft", 0.22f).F("_NoiseAmt", 0.3f)
            .V("_NoiseScale", new Vector4(12f, 1f, 0f, 3f));
        // 衝撃で空気が押し出される水蒸気の円錐 (輪郭が光る)
        var mVapor = ColumnMat(id + "_VaporCone", Boom, 0.3f).Th(0.75f, 0.45f, 0.2f)
            .F("_Rim", 1f).F("_Fresnel", 0.85f).F("_NoiseAmt", 0.8f)
            .V("_NoiseScale", new Vector4(8f, 1.5f, 8f, 0f)).F("_Twist", 0.4f)
            .F("_BottomSoft", 0.1f).F("_TopSoft", 0.35f).F("_FillSoft", 0.15f);

        // ---------- 衝撃の瞬間 (ヒットストップ中に見える絵)
        // EF03B (withFlash = false) では黒い衝撃コマと白い十字フラッシュを出さない
        if (withFlash)
        {
            // 黒い衝撃コマ (白い十字の後ろに45°ずらした大きな黒い星)
            var frame = PS(root, "Impact_Frame", mFrame, Vector3.zero, Vector3.zero, 0f, 0.1f);
            Main(frame, 0.09f, 0.09f, 0f, 0f, 3.4f, 3.4f);
            var fm = frame.main; fm.startRotation = Mathf.PI * 0.25f;
            Burst(frame, 0f, 1, 1); FadeOut(frame, 0.4f);
            SizeLife(frame, t => 0.85f + 0.3f * EaseOut(t, 2f));
            Fudge(frame, -5f);

            var star = PS(root, "Impact_Flash", mStar, Vector3.zero, Vector3.zero, 0f, 0.1f);
            Main(star, 0.14f, 0.14f, 0f, 0f, 2.6f, 2.6f, true);
            Burst(star, 0f, 1, 1); FadeOut(star, 0.25f);
            SizeLife(star, t => 0.7f + 0.5f * EaseOut(t, 2f));
            Fudge(star, -20f);
        }

        // 正面向きの衝撃リング (ビルボード)
        var ring = PS(root, "Impact_Ring", mRing, Vector3.zero, Vector3.zero, 0.01f, 0.1f);
        Main(ring, 0.25f, 0.25f, 0f, 0f, 0.7f, 0.7f);
        Burst(ring, 0f, 1, 1); FadeOut(ring, 0f);
        SizeLife(ring, t => Mathf.Lerp(1f, 5.5f, EaseOut(t, 3f)));
        Fudge(ring, -10f);

        // EF03C: 正面向きのリングを多重にして、波が何度も押し寄せる感じを出す
        //   0.00 パルス(小・速) → 0.01 メイン(太) → 0.03 発光 → 0.05 細リング
        //   → 0.11 余波(細・最大) → 0.16 跳ね返り(小さくもう一度)
        if (multiRing)
        {
            // しきい値を上げるほどリングが細くなる
            var mRingThin = ParticleMat(id + "_RingThin", ImpactSpark, 2).Th(0.95f, 0.84f, 0.76f)
                .V("_GlowColor", new Vector4(1.5f, 0.7f, 0.2f, 0.2f));   // 細リングは発光を弱く (広がってモヤになるため)
            var mRingGlow = ParticleMat(id + "_RingGlow", ImpactSpark, 2, 0.8f).Th(9f, 0.82f, 0.25f);

            ImpactRing(root, "Impact_RingPulse", mRing, 0f, 0.12f, 0.5f, 3f, 0.15f, -12f);
            ImpactRing(root, "Impact_RingGlow", mRingGlow, 0.03f, 0.3f, 0.9f, 5f, 0.05f, -8f);
            ImpactRing(root, "Impact_RingThin", mRingThin, 0.05f, 0.3f, 0.8f, 6.5f, 0.25f, -11f);
            ImpactRing(root, "Impact_RingAfter", mRingThin, 0.11f, 0.36f, 1f, 8f, 0.1f, -9f);
            ImpactRing(root, "Impact_RingRebound", mRing, 0.16f, 0.2f, 0.5f, 3.5f, 0.2f, -12f);
        }

        // ---------- ソニックブーム: 殴った方向 (+Z) に3枚連なって広がる
        for (int k = 0; k < 3; k++)
        {
            float z = 0.12f + 0.42f * k;
            float max = 1.3f + 0.45f * k;
            var b = P(root, "SonicBoom" + (k + 1), Kind.Ring, mBoom, new Vector3(0f, 0f, z), new Vector3(90f, 0f, 0f), 0.025f * k, 0.3f);
            b.innerRadius = 0.72f; b.outerRadius = 1f; b.segments = 64;
            ScaleXZ(b, t => Mathf.Lerp(0.2f, max, EaseOut(t, 3f)));
            b.tracks.Add(T("_Dissolve", t => Seg(t, 0.25f, 1f)));
        }

        var vapor = P(root, "SonicBoom_VaporCone", Kind.Tube, mVapor, new Vector3(0f, 0f, 0.05f), new Vector3(90f, 0f, 0f), 0f, 0.3f);
        vapor.bottomRadius = 0.12f; vapor.topRadius = 1.3f; vapor.height = 1.8f; vapor.segments = 40;
        vapor.tracks.Add(T("_Fill", t => EaseOut(Seg(t, 0f, 0.35f), 3f) * 1.1f));
        vapor.tracks.Add(T("_Dissolve", t => Seg(t, 0.3f, 1f)));
        ScaleXZ(vapor, t => Mathf.Lerp(0.6f, 1.15f, EaseOut(t, 2f)));

        // 前方に吹き抜けるスピード線
        var lines = PS(root, "SonicBoom_Lines", mBoomLine, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(lines, 0.1f, 0.18f, 14f, 22f, 0.05f, 0.09f);
        ShapeCone(lines, 18f, 0.15f);
        Burst(lines, 0f, 12, 16); FadeOut(lines, 0.3f); Stretch(lines, 5f, 0.025f);

        // ---------- 吹き飛ぶもの (前方へ偏らせて「打ち抜いた」方向を出す)
        var puff = PS(root, "Impact_Puffs", mPuff, Vector3.zero, Vector3.zero, 0.02f, 0.1f);
        Main(puff, 0.3f, 0.5f, 1.5f, 3.2f, 0.4f, 0.75f, true);
        ShapeSphere(puff, 0.15f);
        Burst(puff, 0f, 8, 11); FadeOut(puff, 0.25f); Drag(puff, 5f);
        SizeLife(puff, t => 0.6f + 0.7f * EaseOut(t, 2f));

        var spike = PS(root, "Impact_Spikes", mSpike, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(spike, 0.1f, 0.18f, 8f, 13f, 0.14f, 0.22f);
        ShapeSphere(spike, 0.05f);
        Burst(spike, 0f, 10, 14); FadeOut(spike, 0.2f); Drag(spike, 2f); Stretch(spike, 3.2f, 0.04f);

        var spikeFwd = PS(root, "Impact_SpikesForward", mSpike, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(spikeFwd, 0.12f, 0.22f, 10f, 16f, 0.14f, 0.22f);
        ShapeCone(spikeFwd, 40f, 0.1f);
        Burst(spikeFwd, 0f, 10, 14); FadeOut(spikeFwd, 0.2f); Drag(spikeFwd, 2f); Stretch(spikeFwd, 3.5f, 0.04f);

        // 破片: 結晶形 (面の明暗つき) を回転させながら飛ばす。大きい塊 + 小さい欠片の2種類
        var mDebris = ParticleMat(id + "_Debris", Impact, 6).Th(0.78f, 0.3f, 0.1f);

        // 大きい塊: 少なめ・重い・ゆっくり回る
        var debBig = PS(root, "Impact_DebrisChunks", mDebris, Vector3.zero, Vector3.zero, 0.02f, 0.1f);
        Main(debBig, 0.45f, 0.7f, 2.5f, 5f, 0.12f, 0.2f, true);
        ShapeCone(debBig, 60f, 0.12f);
        Burst(debBig, 0f, 5, 7); FadeOut(debBig, 0.6f); Gravity(debBig, 2.4f); Drag(debBig, 1f);
        Spin(debBig, 10f);
        SizeLife(debBig, t => 1f - 0.35f * t);

        // 小さい欠片: 多め・速い・くるくる回る
        var debSmall = PS(root, "Impact_DebrisChips", mDebris, Vector3.zero, Vector3.zero, 0.03f, 0.1f);
        Main(debSmall, 0.3f, 0.55f, 4f, 8f, 0.05f, 0.09f, true);
        ShapeCone(debSmall, 70f, 0.1f);
        Burst(debSmall, 0f, 8, 12); FadeOut(debSmall, 0.55f); Gravity(debSmall, 1.6f); Drag(debSmall, 2f);
        Spin(debSmall, 22f);

        // Engage: 六角紋章 + 残るキラキラ
        HexEmblem(root, "Impact_HexEmblem", mHex, Vector3.zero, 0.03f, 1.3f, 2.8f);
        GlitterBurst(root, "Impact_Glitter", mGlitter, Vector3.zero, 0.05f, 0.4f, 10, 14);

        LightFlash(root, new Vector3(0f, 0f, -0.4f), new Color(1f, 0.85f, 0.5f), 0f, 0.22f, 6f, 5f);
    }

    // ================================================================ EF03S 打撃 (スマブラ風の強打)
    //  原点 = 着弾点 / +Z = 吹っ飛ぶ方向
    //  0.00  ヒットストップ 0.12秒 (長め) + 赤い背景の発光 + 白い強打の光 (X字の針) + 細いリング
    //  0.01  黄〜オレンジの炎状トゲが吹っ飛ぶ方向へ伸びる
    //  0.05  強打の光がもう一度ずれて瞬く (チラつき)
    //  〜0.6 キラキラ・青い線が残って消える
    static void BuildSmashImpact(FEEffect fx, string id)
    {
        var root = fx.transform;
        fx.lifetime = 0.7f;
        fx.shakeDelay = 0f; fx.shakeAmplitude = 0.32f; fx.shakeDuration = 0.3f;
        fx.hitStopDelay = 0f; fx.hitStopDuration = 0.12f; fx.hitStopTimeScale = 0.02f;

        // 背景の発光: 帯を描かず発光フチだけ (しきい値9) を大きく出す
        var mRedGlow = ParticleMat(id + "_RedGlow", SmashRedGlow, 0, 1f).Th(9f, 9f, 1f);
        var mOrangeGlow = ParticleMat(id + "_OrangeGlow", SmashOrangeGlow, 0, 1f).Th(9f, 9f, 1f);
        var mBurst = ParticleMat(id + "_Burst", SmashWhite, 7, 0.25f).Th(0.55f, 0.25f, 0.06f).F("_Intensity", 2.4f);
        var mRing = ParticleMat(id + "_ThinRing", SmashWhite, 8, 0.25f).Th(0.75f, 0.35f, 0.08f).F("_Intensity", 2f)
            .F("_NoiseAmt", 0.25f).V("_NoiseScale", new Vector4(3f, 3f, 0f, 0f));
        var mFire = ParticleMat(id + "_FireSpike", SmashFire, 1).Th(0.62f, 0.3f, 0.1f)
            .F("_NoiseAmt", 0.25f).V("_NoiseScale", new Vector4(3f, 6f, 4f, 0f));
        var mTwinkle = ParticleMat(id + "_Twinkle", SmashFire, 5, 0.3f).Th(0.55f, 0.3f, 0.12f).F("_Intensity", 2.2f);
        var mBlueLine = ParticleMat(id + "_BlueLine", Blue, 1, 0.5f).Th(0.6f, 0.3f, 0.1f);

        // ---------- 背景の発光 (一番奥に描く)
        var red = PS(root, "Smash_RedGlow", mRedGlow, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(red, 0.42f, 0.42f, 0f, 0f, 7.5f, 7.5f);
        Burst(red, 0f, 1, 1); FadeOut(red, 0.35f);
        SizeLife(red, t => 0.8f + 0.3f * EaseOut(t, 2f));
        Fudge(red, 40f);

        var orange = PS(root, "Smash_OrangeGlow", mOrangeGlow, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(orange, 0.3f, 0.3f, 0f, 0f, 3.8f, 3.8f);
        Burst(orange, 0f, 1, 1); FadeOut(orange, 0.3f);
        SizeLife(orange, t => 0.85f + 0.25f * EaseOut(t, 2f));
        Fudge(orange, 35f);

        // ---------- 炎状のトゲ: 吹っ飛ぶ方向 (+Z) へ多め + 全方向へ少し
        var fireFwd = PS(root, "Smash_FireForward", mFire, Vector3.zero, Vector3.zero, 0.01f, 0.1f);
        Main(fireFwd, 0.22f, 0.35f, 9f, 16f, 0.35f, 0.6f);
        ShapeCone(fireFwd, 50f, 0.2f);
        Burst(fireFwd, 0f, 9, 12); FadeOut(fireFwd, 0.35f); Drag(fireFwd, 6f); Stretch(fireFwd, 2.5f, 0.02f);
        Fudge(fireFwd, 10f);

        var fireAll = PS(root, "Smash_FireAround", mFire, Vector3.zero, Vector3.zero, 0.01f, 0.1f);
        Main(fireAll, 0.18f, 0.28f, 6f, 10f, 0.22f, 0.4f);
        ShapeSphere(fireAll, 0.1f);
        Burst(fireAll, 0f, 5, 7); FadeOut(fireAll, 0.35f); Drag(fireAll, 6f); Stretch(fireAll, 2.2f, 0.02f);
        Fudge(fireAll, 10f);

        // ---------- 細いリング
        var ring = PS(root, "Smash_ThinRing", mRing, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(ring, 0.18f, 0.18f, 0f, 0f, 3.4f, 3.4f, true);
        Burst(ring, 0f, 1, 1); FadeOut(ring, 0.4f);
        SizeLife(ring, t => Mathf.Lerp(0.75f, 1.25f, EaseOut(t, 3f)));
        Fudge(ring, -15f);

        // ---------- 白い強打の光 (手前に描く) + 少しずれた2回目の瞬き
        var burst = PS(root, "Smash_Burst", mBurst, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(burst, 0.2f, 0.2f, 0f, 0f, 4.6f, 4.6f);
        var bm = burst.main; bm.startRotation = new MMC(-0.2f, 0.2f);
        Burst(burst, 0f, 1, 1); FadeOut(burst, 0.35f);
        SizeLife(burst, t => 0.85f + 0.3f * EaseOut(t, 2f));
        Fudge(burst, -25f);

        var flicker = PS(root, "Smash_BurstFlicker", mBurst, Vector3.zero, Vector3.zero, 0.05f, 0.1f);
        Main(flicker, 0.12f, 0.12f, 0f, 0f, 3.4f, 3.4f);
        var fm = flicker.main; fm.startRotation = new MMC(0.3f, 0.5f);
        Burst(flicker, 0f, 1, 1); FadeOut(flicker, 0.2f);
        SizeLife(flicker, t => 0.9f + 0.3f * EaseOut(t, 2f));
        Fudge(flicker, -24f);

        // ---------- 周りに残るキラキラと青い線
        var tw = PS(root, "Smash_Twinkles", mTwinkle, Vector3.zero, Vector3.zero, 0.02f, 0.1f);
        Main(tw, 0.25f, 0.4f, 0.5f, 1.5f, 0.35f, 0.6f);
        ShapeSphere(tw, 1.4f);
        Burst(tw, 0f, 4, 6); FadeOut(tw, 0.5f); Twinkle(tw);

        var blue = PS(root, "Smash_BlueLines", mBlueLine, Vector3.zero, Vector3.zero, 0.02f, 0.1f);
        Main(blue, 0.1f, 0.16f, 14f, 20f, 0.04f, 0.06f);
        ShapeSphere(blue, 0.6f);
        Burst(blue, 0f, 4, 6); FadeOut(blue, 0.3f); Stretch(blue, 6f, 0.02f);

        LightFlash(root, new Vector3(0f, 0f, -0.4f), new Color(1f, 0.45f, 0.2f), 0f, 0.3f, 7f, 6f);
    }

    // ================================================================ EF09 剣撃 (FE風)
    //  原点 = 攻撃者の足元 / +Z = 相手の方向。相手は 2.4m 先・高さ 1.1m を想定 (BuildFESword の target)
    //  細く長いシアンの光の弧が、左下から相手を通って右上へ大きく振り上がる
    //  弧が相手を通過した瞬間に: ヒットストップ + 黄〜オレンジの炎状トゲ + 赤い筋 + 結晶の破片
    static void BuildFESword(FEEffect fx, string id)
    {
        var root = fx.transform;
        const float dur = 0.5f;
        const float radius = 2.4f;
        const float width = 0.34f;          // 細め
        // 左下から振り上げ → 相手を通過 → 右上で跳ね上がる (YZ平面の角度: 0°=前, 90°=上)
        const float start = -105f, end = 55f;
        const float hitAngle = -12f;        // 相手を通過する角度
        var target = new Vector3(0f, 1.1f, 2.4f);   // 相手の胸の高さ (デモの被弾位置)
        var euler = Vector3.zero;
        var rot = Quaternion.Euler(euler);
        float ha = hitAngle * Mathf.Deg2Rad;
        // 弧が必ず相手を通るように、弧の中心を相手の位置から逆算
        var pos = target - rot * new Vector3(0f, Mathf.Sin(ha), Mathf.Cos(ha)) * radius;
        var contact = target;

        // 弧が相手を通過する時刻を、振りのカーブ (SlashTracks と同じ) から逆算
        float uHit = (hitAngle - start) / (end - start);
        float hitX = 1f - Mathf.Pow(1f - Mathf.Clamp01(uHit / 1.04f), 1f / 2.5f);
        float hitTime = dur * 0.32f * hitX;
        var swing = rot * (new Vector3(0f, Mathf.Cos(ha), -Mathf.Sin(ha)) * Mathf.Sign(end - start)); // 当たった瞬間の振りの向き
        var swingEuler = Quaternion.LookRotation(swing).eulerAngles;

        fx.lifetime = dur + 0.35f;
        fx.shakeDelay = hitTime; fx.shakeAmplitude = 0.12f; fx.shakeDuration = 0.18f;
        fx.hitStopDelay = hitTime; fx.hitStopDuration = 0.06f; fx.hitStopTimeScale = 0.05f;

        // ---------- materials
        var mGlow = SlashMat(id + "_Glow", FECyan, 1f).Th(9f, 9f, 0.6f)
            .V("_OutlineColor", new Vector4(0f, 0f, 0f, 0f))
            .F("_TailLen", 0.95f).F("_TailPow", 1.1f).F("_HeadTaper", 0.15f).F("_OuterSoft", 0.7f).F("_NoiseAmt", 0.3f);
        var mMain = SlashMat(id + "_Blade", FECyan, 0.35f).Th(0.55f, 0.24f, 0.07f).F("_Intensity", 2.4f)
            .F("_TailLen", 0.9f).F("_TailPow", 1.25f).F("_HeadTaper", 0.12f).F("_OuterSoft", 0.1f)
            .F("_NoiseAmt", 0.25f).V("_NoiseScale", new Vector4(7f, 1.5f, 5f, 0f))
            .F("_Shade", 0.25f);
        var mEcho = SlashMat(id + "_Echo", FECyan, 0.6f).Th(0.6f, 0.3f, 0.1f)
            .F("_TailLen", 0.8f).F("_TailPow", 1.4f).F("_HeadTaper", 0.1f).F("_OuterSoft", 0.2f).F("_NoiseAmt", 0.5f)
            .V("_NoiseScale", new Vector4(10f, 1.5f, 7f, 0f));
        var mSparkle = GlitterMat(id + "_Glitter", Sparkle);
        var mFire = ParticleMat(id + "_HitFire", SmashFire, 1).Th(0.62f, 0.3f, 0.1f)
            .F("_NoiseAmt", 0.25f).V("_NoiseScale", new Vector4(3f, 6f, 4f, 0f));
        var mRed = ParticleMat(id + "_HitRedStreak", FERedStreak, 1, 0.3f).Th(0.6f, 0.3f, 0.1f);
        var mFlash = ParticleMat(id + "_HitFlash", SmashFire, 3, 0.3f).Th(0.5f, 0.25f, 0.08f).F("_Intensity", 2.4f);
        var mHitGlow = ParticleMat(id + "_HitGlow", SmashOrangeGlow, 0, 1f).Th(9f, 9f, 1f);
        var mShard = ParticleMat(id + "_Crystal", Crystal, 6).Th(0.7f, 0.3f, 0.1f);

        // ---------- 光の弧 (発光 → 本体 → 遅れて追う細い残光)
        var glow = P(root, "Sword_Glow", Kind.Arc, mGlow, pos, euler, 0f, dur);
        Arc(glow, radius + 0.15f, width * 2.6f, start, end, 0.2f);
        SlashTracks(glow, 0.95f);

        var blade = P(root, "Sword_Blade", Kind.Arc, mMain, pos, euler, 0f, dur);
        Arc(blade, radius, width, start, end, 0.2f);
        SlashTracks(blade, 0.9f);

        var echo = P(root, "Sword_Echo", Kind.Arc, mEcho, pos + rot * new Vector3(0.04f, 0f, 0f), euler, 0.035f, dur);
        Arc(echo, radius - 0.25f, width * 0.4f, start - 8f, end + 6f, 0.15f);
        SlashTracks(echo, 0.8f);

        // 弧に沿って残るキラキラ
        float sweep = dur * 0.32f;
        var glit = PS(root, "Sword_Glitter", mSparkle, pos, euler, 0.01f, sweep);
        Main(glit, 0.3f, 0.6f, 0.2f, 0.8f, 0.08f, 0.16f);
        ArcShape(glit, radius - width * 0.2f, start, end, sweep, true);
        Rate(glit, 22f / sweep);
        FadeOut(glit, 0.55f); Twinkle(glit);

        // ---------- 当たった瞬間 (相手の位置)
        var hitGlow = PS(root, "Hit_Glow", mHitGlow, contact, Vector3.zero, hitTime, 0.1f);
        Main(hitGlow, 0.25f, 0.25f, 0f, 0f, 2.4f, 2.4f);
        Burst(hitGlow, 0f, 1, 1); FadeOut(hitGlow, 0.3f);
        Fudge(hitGlow, 30f);

        var flash = PS(root, "Hit_Flash", mFlash, contact, Vector3.zero, hitTime, 0.1f);
        Main(flash, 0.12f, 0.12f, 0f, 0f, 1.5f, 1.5f, true);
        Burst(flash, 0f, 1, 1); FadeOut(flash, 0.25f);
        SizeLife(flash, t => 0.7f + 0.5f * EaseOut(t, 2f));
        Fudge(flash, -20f);

        // 炎状のトゲ: 振った方向へ多め + 周りに少し
        var fire = PS(root, "Hit_FireForward", mFire, contact, swingEuler, hitTime, 0.1f);
        Main(fire, 0.18f, 0.3f, 8f, 14f, 0.25f, 0.5f);
        ShapeCone(fire, 45f, 0.1f);
        Burst(fire, 0f, 8, 11); FadeOut(fire, 0.35f); Drag(fire, 6f); Stretch(fire, 2.4f, 0.02f);

        var fireAll = PS(root, "Hit_FireAround", mFire, contact, Vector3.zero, hitTime, 0.1f);
        Main(fireAll, 0.15f, 0.25f, 5f, 9f, 0.18f, 0.32f);
        ShapeSphere(fireAll, 0.1f);
        Burst(fireAll, 0f, 5, 7); FadeOut(fireAll, 0.35f); Drag(fireAll, 6f); Stretch(fireAll, 2.2f, 0.02f);

        // 赤い筋 (振った方向へ細長く)
        var red = PS(root, "Hit_RedStreaks", mRed, contact, swingEuler, hitTime, 0.1f);
        Main(red, 0.12f, 0.2f, 12f, 18f, 0.05f, 0.09f);
        ShapeCone(red, 25f, 0.1f);
        Burst(red, 0f, 5, 8); FadeOut(red, 0.3f); Stretch(red, 5f, 0.025f);

        // 結晶の破片: 回転しながら飛び散る
        var shards = PS(root, "Hit_Crystals", mShard, contact, swingEuler, hitTime + 0.01f, 0.1f);
        Main(shards, 0.45f, 0.75f, 3f, 7f, 0.1f, 0.22f, true);
        ShapeCone(shards, 70f, 0.15f);
        Burst(shards, 0f, 10, 14); FadeOut(shards, 0.6f); Gravity(shards, 1.6f); Drag(shards, 1.5f);
        Spin(shards, 14f);
        SizeLife(shards, t => 1f - 0.3f * t);

        LightFlash(root, contact, new Color(1f, 0.75f, 0.4f), hitTime, 0.22f, 4f, 5f);
    }

    // ================================================================ EF06 弓
    static void BuildArrow(FEEffect fx, string id)
    {
        var root = fx.transform;
        fx.lifetime = 0.6f;

        var mTrail = SlashMat(id + "_Trail", LightBlue)
            .F("_Symmetric", 1f).F("_TailLen", 0.85f).F("_HeadTaper", 0.12f).F("_TailPow", 1.4f)
            .F("_NoiseAmt", 0.3f).V("_NoiseScale", new Vector4(10f, 1f, 8f, 0f));
        var mCone = WindConeMat(id + "_WindCone");
        var mRing = ColumnMat(id + "_Ring", LightBlue).F("_BottomSoft", 0.5f).F("_TopSoft", 0.25f).F("_NoiseAmt", 0.2f);
        var mStar = ParticleMat(id + "_TipStar", LightBlue, 3);
        var mWindP = ParticleMat(id + "_SparkWind", Wind, 1, 0.6f);
        var mHex = ParticleMat(id + "_HexEmblem", Sparkle, 4, 0.3f).Th(0.8f, 0.45f, 0.15f);
        var mGlitter = GlitterMat(id + "_Glitter", Sparkle);

        // 矢の軌跡 (先端=原点, 後ろへ3.2m)
        var tr = P(root, "Arrow_Trail", Kind.Line, mTrail, new Vector3(0f, 0f, -3.2f), Vector3.zero, 0f, 0.45f);
        tr.length = 3.2f; tr.lineWidth = 0.14f; tr.segments = 32;
        tr.tracks.Add(T("_Head", t => t < 0.2f ? EaseOut(t / 0.2f, 2.5f) : 1f + Seg(t, 0.2f, 1f) * 0.85f));
        tr.tracks.Add(T("_Dissolve", t => Seg(t, 0.4f, 1f) * 0.8f));

        // 先端の風切り円錐
        var cone = P(root, "Arrow_WindCone", Kind.Tube, mCone, new Vector3(0f, 0f, -0.9f), new Vector3(90f, 0f, 0f), 0f, 0.3f);
        cone.bottomRadius = 0.28f; cone.topRadius = 0.03f; cone.height = 1f; cone.segments = 24;
        cone.tracks.Add(T("_Fill", t => EaseOut(Seg(t, 0f, 0.25f), 2.5f) * 1.1f));
        cone.tracks.Add(T("_Dissolve", t => Seg(t, 0.3f, 1f) * 0.9f));

        // 先端リング
        var ring = P(root, "Arrow_TipRing", Kind.Ring, mRing, new Vector3(0f, 0f, 0.05f), new Vector3(90f, 0f, 0f), 0.05f, 0.22f);
        ring.innerRadius = 0.5f; ring.outerRadius = 1f;
        ScaleXZ(ring, t => Mathf.Lerp(0.1f, 0.6f, EaseOut(t, 3f)));
        ring.tracks.Add(T("_Dissolve", t => Seg(t, 0.2f, 1f)));

        // 先端の十字の光
        var star = PS(root, "Arrow_TipFlash", mStar, Vector3.zero, Vector3.zero, 0f, 0.1f);
        Main(star, 0.14f, 0.14f, 0f, 0f, 0.7f, 0.7f);
        Burst(star, 0f, 1, 1); FadeOut(star, 0f);
        SizeLife(star, t => 0.6f + 0.7f * EaseOut(t, 2f));

        // 後ろに流れるスピード線
        var lines = PS(root, "Arrow_SpeedLines", mWindP, Vector3.zero, new Vector3(0f, 180f, 0f), 0.01f, 0.1f);
        Main(lines, 0.1f, 0.18f, 8f, 12f, 0.03f, 0.05f);
        ShapeCone(lines, 6f, 0.1f);
        Burst(lines, 0f, 6, 9); FadeOut(lines, 0.3f); Stretch(lines, 5f, 0.02f);

        // Engage: 小さな六角紋章 + 飛行中に落ちるキラキラ (ワールド空間に残る)
        HexEmblem(root, "Arrow_HexEmblem", mHex, Vector3.zero, 0.02f, 0.5f, 1f);
        var g = PS(root, "Arrow_GlitterTrail", mGlitter, Vector3.zero, Vector3.zero, 0f, 0.35f, true);
        Main(g, 0.3f, 0.55f, 0f, 0.3f, 0.07f, 0.13f);
        ShapeSphere(g, 0.08f);
        Rate(g, 45f); FadeOut(g, 0.5f); Twinkle(g);
    }

    // ================================================================ EF07 / EF07B 光球の収束爆発
    //  0.00       魔法陣が広がる
    //  0.35       周囲に3つのミニ光球がポンと出現
    //  0.55〜1.30 ハリケーン状に回りながら中心へ (近づくほど速く、彗星の尾を引く)
    //  1.30       中心で合体 → 1つの光球が膨らんで一瞬縮む (溜め)
    //  1.50       大爆発 (光の殻 + 芯 + 衝撃波3枚 + 火花 + キラキラ + 六角紋章)
    //  〜2.45     魔法陣ごと消える
    //  EF07B = 風の渦 (筒2層) と台風の腕を出さない版
    static void BuildOrbBurst(FEEffect fx, string id, bool withStorm)
    {
        var root = fx.transform;
        const float miniStart = 0.35f;
        const float spiralStart = 0.55f;
        const float merge = 1.3f;
        const float boom = 1.5f;
        const float spiralRadius = 1.9f;
        const float orbHeight = 1.1f;
        var center = new Vector3(0f, orbHeight, 0f);
        fx.lifetime = boom + 0.95f;
        fx.shakeDelay = boom; fx.shakeAmplitude = 0.22f; fx.shakeDuration = 0.45f;

        // ---------- materials
        var mCircle = ColumnMat(id + "_MagicCircle", Gold, 0.3f).Th(0.75f, 0.45f, 0.2f)
            .F("_BottomSoft", 0.05f).F("_TopSoft", 0.05f).F("_SpokeCount", 16f).F("_SpokeAmt", 0.55f)
            .F("_NoiseAmt", 0.5f).V("_NoiseScale", new Vector4(8f, 2f, 0f, 0.2f));
        var mCircleEdge = ColumnMat(id + "_CircleEdge", Gold, 0.3f)
            .F("_BottomSoft", 0.4f).F("_TopSoft", 0.4f).F("_NoiseAmt", 0.2f);
        var mRunes = ColumnMat(id + "_Runes", Gold, 0.4f).Th(0.85f, 0.6f, 0.4f)
            .F("_BottomSoft", 0.3f).F("_TopSoft", 0.3f).F("_SpokeCount", 36f).F("_SpokeAmt", 0.8f)
            .F("_NoiseAmt", 0.6f).V("_NoiseScale", new Vector4(36f, 1f, 0f, 0f));
        // 光球: 正面が白く、輪郭が金。表面がゆっくり流れる
        var mOrb = ColumnMat(id + "_Orb", Holy, 0.15f).Th(0.8f, 0.45f, 0.18f).F("_Intensity", 2.6f)
            .F("_Fresnel", 1f).F("_NoiseAmt", 0.35f).V("_NoiseScale", new Vector4(6f, 2f, 1.5f, 0.6f))
            .F("_BottomSoft", 0.001f).F("_TopSoft", 0.001f);
        // 光球のまわりのオーラ (輪郭だけ光る加算リング)
        var mHalo = ColumnMat(id + "_OrbHalo", Gold, 0.75f).Th(9f, 0.82f, 0.5f)
            .F("_Fresnel", 1f).F("_Rim", 1f).F("_NoiseAmt", 0.25f).V("_NoiseScale", new Vector4(5f, 2f, 1f, -0.8f))
            .F("_BottomSoft", 0.001f).F("_TopSoft", 0.001f);
        var mOrbBase = ColumnMat(id + "_OrbShadowRing", Gold, 0.3f).F("_BottomSoft", 0.3f).F("_TopSoft", 0.4f).F("_NoiseAmt", 0.3f);
        // 彗星の尾 (TrailRenderer 用: 先頭が丸く、後ろへ細く)
        var mTail = SlashMat(id + "_OrbTail", Gold, 0.35f)
            .F("_Symmetric", 1f).F("_FlipU", 1f).F("_Head", 1f).F("_TailLen", 1f)
            .F("_HeadTaper", 0.04f).F("_TailPow", 1.3f).F("_NoiseAmt", 0.3f)
            .V("_NoiseScale", new Vector4(6f, 1f, 5f, 0f));
        var mConverge = ColumnMat(id + "_Converge", Holy, 0.3f).F("_BottomSoft", 0.6f).F("_TopSoft", 0.15f).F("_NoiseAmt", 0.2f);
        // 爆発: 外へ広がる光の殻 (輪郭が光る) と白い芯
        var mBlastShell = ColumnMat(id + "_BlastShell", Holy, 0.35f).Th(0.6f, 0.35f, 0.15f)
            .F("_Rim", 1f).F("_Fresnel", 1f).F("_NoiseAmt", 0.45f).V("_NoiseScale", new Vector4(8f, 3f, 0f, 0.6f))
            .F("_BottomSoft", 0.001f).F("_TopSoft", 0.001f);
        var mBlastCore = ColumnMat(id + "_BlastCore", Holy, 0.2f).Th(0.45f, 0.25f, 0.1f).F("_Intensity", 3f)
            .F("_Fresnel", 0.9f).F("_NoiseAmt", 0.3f).F("_BottomSoft", 0.001f).F("_TopSoft", 0.001f);
        var mShock = ColumnMat(id + "_Shock", Gold, 0.2f).F("_BottomSoft", 0.4f).F("_TopSoft", 0.2f).F("_NoiseAmt", 0.3f);
        var mMote = ParticleMat(id + "_Motes", Gold, 1, 0.3f);
        var mSpark = ParticleMat(id + "_Sparks", Gold, 1, 0.3f);
        var mStar = ParticleMat(id + "_Flash", Holy, 3, 0.2f).F("_Intensity", 2.5f);
        var mHex = ParticleMat(id + "_HexEmblem", Gold, 4, 0.3f).Th(0.8f, 0.45f, 0.15f);
        var mGlitter = GlitterMat(id + "_Glitter", Holy);

        // ---------- 1. 魔法陣 (爆発のあと消える)
        float circleDur = fx.lifetime - 0.1f;
        float fadeFrom = (boom + 0.25f) / circleDur;
        var circle = P(root, "Orb_MagicCircle", Kind.Ring, mCircle, new Vector3(0f, 0.03f, 0f), Vector3.zero, 0f, circleDur);
        circle.innerRadius = 0.3f; circle.outerRadius = 2.3f; circle.segments = 64; circle.spinSpeed = 50f;
        ScaleXZ(circle, t => EaseOut(Seg(t, 0f, 0.35f / circleDur), 3f));
        circle.tracks.Add(T("_Dissolve", t => Seg(t, fadeFrom, 1f)));

        var edge = P(root, "Orb_CircleEdge", Kind.Ring, mCircleEdge, new Vector3(0f, 0.035f, 0f), Vector3.zero, 0f, circleDur);
        edge.innerRadius = 2.25f; edge.outerRadius = 2.6f; edge.segments = 64; edge.spinSpeed = -35f;
        ScaleXZ(edge, t => EaseOut(Seg(t, 0f, 0.4f / circleDur), 3f));
        edge.tracks.Add(T("_Dissolve", t => Seg(t, fadeFrom, 1f)));

        var runes = P(root, "Orb_Runes", Kind.Ring, mRunes, new Vector3(0f, 0.04f, 0f), Vector3.zero, 0.1f, circleDur - 0.1f);
        runes.innerRadius = 1.55f; runes.outerRadius = 1.95f; runes.segments = 96; runes.spinSpeed = 80f;
        ScaleXZ(runes, t => EaseOut(Seg(t, 0f, 0.3f / circleDur), 3f));
        runes.tracks.Add(T("_Dissolve", t => Seg(t, fadeFrom, 1f)));

        // ---------- 2. ミニ光球 x3 → 3. ハリケーン状に中心へ
        float miniDur = merge - miniStart;
        float spiralDur = merge - spiralStart;
        for (int i = 0; i < 3; i++)
        {
            var pivot = new GameObject("Orb_Mini" + (i + 1));
            pivot.transform.SetParent(root, false);
            var spiral = pivot.AddComponent<FESpiralMotion>();
            spiral.delay = spiralStart;
            spiral.duration = spiralDur;
            spiral.startAngle = 90f + i * 120f;
            spiral.height = orbHeight;
            // 半径は最初ゆっくり→最後に一気に (吸い込まれる感じ)
            spiral.radius = Fn(t => spiralRadius * (1f - EaseIn(t, 1.8f)));
            // 中心に近づくほど回転が速くなる (台風の目に向かう感じ)
            spiral.angle = Fn(t => EaseIn(t, 1.6f) * 420f);

            // 彗星の尾
            var tr = pivot.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mTail;
            tr.time = 0.32f;
            tr.minVertexDistance = 0.03f;
            tr.widthMultiplier = 0.42f;
            tr.widthCurve = Fn(t => 1f - 0.7f * t);
            tr.numCapVertices = 4;
            tr.alignment = LineAlignment.View;
            tr.textureMode = LineTextureMode.Stretch;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            spiral.trail = tr;

            // 出現: 一瞬大きく → 落ち着く → 中心に着く直前に小さくなって吸収される
            Func<float, float> orbS = t =>
            {
                float appear = t < 0.1f ? EaseOut(t / 0.1f, 2f) * 1.4f : Mathf.Lerp(1.4f, 1f, Seg(t, 0.1f, 0.2f));
                float pulse = 1f + 0.08f * Mathf.Sin(t * 40f);
                return appear * pulse * (1f - 0.55f * EaseIn(Seg(t, 0.82f, 1f), 2f));
            };
            var body = P(pivot.transform, "Body", Kind.Sphere, mOrb, Vector3.zero, Vector3.zero, miniStart, miniDur);
            body.radius = 0.22f; body.segments = 24;
            ScaleAll(body, orbS);

            var halo = P(pivot.transform, "Halo", Kind.Sphere, mHalo, Vector3.zero, Vector3.zero, miniStart, miniDur);
            halo.radius = 0.42f; halo.segments = 24;
            ScaleAll(halo, orbS);

            // 足元の小さな影リング (位置関係を分かりやすく)
            var baseRing = P(pivot.transform, "BaseRing", Kind.Ring, mOrbBase, new Vector3(0f, 0.05f - orbHeight, 0f), Vector3.zero, miniStart, miniDur);
            baseRing.innerRadius = 0.12f; baseRing.outerRadius = 0.4f; baseRing.segments = 32; baseRing.spinSpeed = 120f;
            ScaleXZ(baseRing, t => EaseOut(Seg(t, 0f, 0.15f), 3f) * (1f - 0.7f * EaseIn(Seg(t, 0.8f, 1f), 2f)));

            // 出現の一瞬のフラッシュ
            var pop = PS(pivot.transform, "PopFlash", mStar, Vector3.zero, Vector3.zero, miniStart, 0.1f);
            Main(pop, 0.16f, 0.16f, 0f, 0f, 1.3f, 1.3f);
            Burst(pop, 0f, 1, 1); FadeOut(pop, 0.1f);
            SizeLife(pop, t => 0.6f + 0.6f * EaseOut(t, 2f));
            GlitterBurst(pivot.transform, "PopGlitter", mGlitter, Vector3.zero, miniStart, 0.25f, 5, 8);

            // 移動中にワールド空間へキラキラを残す
            var trail = PS(pivot.transform, "TrailGlitter", mGlitter, Vector3.zero, Vector3.zero, spiralStart, spiralDur, true);
            Main(trail, 0.3f, 0.55f, 0f, 0.4f, 0.07f, 0.14f);
            ShapeSphere(trail, 0.18f);
            Rate(trail, 45f); FadeOut(trail, 0.45f); Twinkle(trail);
        }

        // 風の渦 + 台風の腕 (withStorm = false の EF07B では出さない)
        if (withStorm)
        {
            var mHurricane = ColumnMat(id + "_Hurricane", GoldWind, 0.55f).Th(0.8f, 0.5f, 0.25f)
                .F("_Rim", 1f).F("_Fresnel", 0.8f).F("_Twist", 1.6f).F("_NoiseAmt", 1f)
                .V("_NoiseScale", new Vector4(10f, 1.2f, 3f, -1.5f)).F("_BottomSoft", 0.15f).F("_TopSoft", 0.4f);
            // 上から見た台風の腕 (3本のらせん)
            var mStormArms = ColumnMat(id + "_StormArms", GoldWind, 0.6f).Th(0.8f, 0.5f, 0.28f)
                .F("_Twist", 2.5f).F("_SpokeCount", 3f).F("_SpokeAmt", 0.65f).F("_NoiseAmt", 0.6f)
                .V("_NoiseScale", new Vector4(6f, 3f, 0f, -0.5f)).F("_BottomSoft", 0.35f).F("_TopSoft", 0.3f);

            // 風の渦 (外→内へ縮む2層のねじれた筒)
            for (int i = 0; i < 2; i++)
            {
                float d = spiralStart - 0.05f + i * 0.1f;
                var h = P(root, "Orb_Hurricane" + (i + 1), Kind.Tube, mHurricane, Vector3.zero, Vector3.zero, d, merge - d);
                h.bottomRadius = i == 0 ? 2.1f : 1.5f; h.topRadius = i == 0 ? 2.7f : 2f;
                h.height = i == 0 ? 2.6f : 2.1f; h.segments = 40;
                h.spinSpeed = i == 0 ? -380f : -520f;   // FESpiralMotion の回転方向に合わせて負
                ScaleXZ(h, t => Mathf.Lerp(1f, 0.15f, EaseIn(t, 1.8f)));
                h.tracks.Add(T("_Fill", t => EaseOut(Seg(t, 0f, 0.25f), 2f) * 1.1f));
                h.tracks.Add(T("_Dissolve", t => Seg(t, 0.85f, 1f) * 0.8f));
            }

            // 光球と同じ高さの台風の腕
            var arms = P(root, "Orb_StormArms", Kind.Ring, mStormArms, center - new Vector3(0f, 0.05f, 0f), Vector3.zero, spiralStart - 0.05f, merge - spiralStart + 0.1f);
            arms.innerRadius = 0.1f; arms.outerRadius = 2.4f; arms.segments = 64; arms.spinSpeed = -300f;
            ScaleXZ(arms, t => EaseOut(Seg(t, 0f, 0.2f), 2f) * Mathf.Lerp(1f, 0.25f, EaseIn(Seg(t, 0.2f, 1f), 1.6f)));
            arms.tracks.Add(T("_Dissolve", t => Seg(t, 0.8f, 1f)));
        }

        // 外から吸い込まれる光の粒
        var inward = PS(root, "Orb_Inward", mMote, center, Vector3.zero, spiralStart, spiralDur);
        Main(inward, 0.35f, 0.55f, 0f, 0f, 0.05f, 0.1f);
        ShapeCircle(inward, 2.6f, 0.15f, 360f, new Vector3(90f, 0f, 0f), Vector3.one);
        Rate(inward, 80f); FadeOut(inward, 0.6f); Stretch(inward, 3f, 0.05f);
        Orbit(inward, 0f, -4f, -4f);

        // 合体直前、外から内へ閉じるリング
        var conv = P(root, "Orb_Converge", Kind.Ring, mConverge, center, Vector3.zero, merge - 0.4f, 0.4f);
        conv.innerRadius = 0.85f; conv.outerRadius = 1f; conv.segments = 64;
        ScaleXZ(conv, t => Mathf.Lerp(3f, 0.15f, EaseIn(t, 2f)));

        // ---------- 4. 合体 → 溜め (膨らんで一瞬縮む)
        float holdDur = boom - merge + 0.04f;
        Func<float, float> coreS = t => t < 0.45f
            ? EaseOut(t / 0.45f, 2.5f) * 1.15f
            : Mathf.Lerp(1.15f, 0.65f, EaseIn(Seg(t, 0.45f, 1f), 2f)) * (1f + 0.05f * Mathf.Sin(t * 90f));
        var core = P(root, "Merge_Core", Kind.Sphere, mOrb, center, Vector3.zero, merge - 0.04f, holdDur);
        core.radius = 0.5f; core.segments = 32;
        ScaleAll(core, coreS, 60);
        var coreHalo = P(root, "Merge_Halo", Kind.Sphere, mHalo, center, Vector3.zero, merge - 0.04f, holdDur);
        coreHalo.radius = 0.95f; coreHalo.segments = 32;
        ScaleAll(coreHalo, coreS, 60);

        var mergeFlash = PS(root, "Merge_Flash", mStar, center, Vector3.zero, merge, 0.1f);
        Main(mergeFlash, 0.14f, 0.14f, 0f, 0f, 2.2f, 2.2f);
        Burst(mergeFlash, 0f, 1, 1); FadeOut(mergeFlash, 0.1f);
        SizeLife(mergeFlash, t => 0.6f + 0.5f * EaseOut(t, 2f));

        // ---------- 5. 爆発
        var shell = P(root, "Blast_Shell", Kind.Sphere, mBlastShell, center, Vector3.zero, boom, 0.6f);
        shell.radius = 1f; shell.segments = 40;
        ScaleAll(shell, t => Mathf.Lerp(0.4f, 3.3f, EaseOut(t, 3f)));
        shell.tracks.Add(T("_Dissolve", t => Seg(t, 0.15f, 1f)));

        var bcore = P(root, "Blast_Core", Kind.Sphere, mBlastCore, center, Vector3.zero, boom, 0.34f);
        bcore.radius = 0.9f; bcore.segments = 32;
        ScaleAll(bcore, t => Mathf.Lerp(0.7f, 2f, EaseOut(t, 2f)));
        bcore.tracks.Add(T("_Dissolve", t => Seg(t, 0.3f, 1f) * 1.1f));

        // 衝撃波: 水平 / 斜め / 地面
        var r1 = P(root, "Blast_RingFlat", Kind.Ring, mShock, center, Vector3.zero, boom, 0.5f);
        r1.innerRadius = 0.75f; r1.outerRadius = 1f; r1.segments = 64;
        ScaleXZ(r1, t => Mathf.Lerp(0.5f, 5.5f, EaseOut(t, 3f)));
        r1.tracks.Add(T("_Dissolve", t => Seg(t, 0.2f, 1f)));

        var r2 = P(root, "Blast_RingTilt", Kind.Ring, mShock, center, new Vector3(65f, 30f, 0f), boom + 0.03f, 0.45f);
        r2.innerRadius = 0.78f; r2.outerRadius = 1f; r2.segments = 64;
        ScaleXZ(r2, t => Mathf.Lerp(0.5f, 4f, EaseOut(t, 3f)));
        r2.tracks.Add(T("_Dissolve", t => Seg(t, 0.2f, 1f)));

        Shock(root, "Blast_RingGround", mShock, boom + 0.05f, 0.55f, 4.8f);

        var star = PS(root, "Blast_Flash", mStar, center, Vector3.zero, boom, 0.1f);
        Main(star, 0.22f, 0.22f, 0f, 0f, 5.5f, 5.5f);
        Burst(star, 0f, 1, 1); FadeOut(star, 0.1f);
        SizeLife(star, t => 0.6f + 0.6f * EaseOut(t, 2f));
        Fudge(star, -20f);
        HexEmblem(root, "Blast_HexEmblem", mHex, center, boom + 0.02f, 2.2f, 4.2f);

        var sparks = PS(root, "Blast_Sparks", mSpark, center, Vector3.zero, boom, 0.1f);
        Main(sparks, 0.2f, 0.4f, 8f, 14f, 0.08f, 0.14f);
        ShapeSphere(sparks, 0.3f);
        Burst(sparks, 0f, 26, 34); FadeOut(sparks, 0.3f); Drag(sparks, 3f); Stretch(sparks, 3f, 0.05f);

        var glit = PS(root, "Blast_Glitter", mGlitter, center, Vector3.zero, boom + 0.02f, 0.1f, true);
        Main(glit, 0.6f, 1.1f, 1.5f, 5f, 0.08f, 0.16f);
        ShapeSphere(glit, 0.5f);
        Burst(glit, 0f, 30, 40); FadeOut(glit, 0.5f); Drag(glit, 2.5f); Twinkle(glit); Gravity(glit, -0.15f);

        var motes = PS(root, "Blast_Motes", mMote, Vector3.zero, Vector3.zero, boom, 0.4f, true);
        Main(motes, 0.4f, 0.8f, 0f, 0f, 0.06f, 0.12f);
        ShapeCircle(motes, 1.6f, 1f, 360f, new Vector3(90f, 0f, 0f), Vector3.one);
        Rate(motes, 70f); FadeOut(motes, 0.5f); Stretch(motes, 4f, 0.03f);
        VelY(motes, 3f, 7f);

        // 溜め中のほのかな明かり + 爆発の強い光
        LightFlash(root, center, new Color(1f, 0.85f, 0.5f), spiralStart, boom - spiralStart, 1.5f, 6f);
        LightFlash(root, center, new Color(1f, 0.9f, 0.6f), boom, 0.7f, 6f, 10f);
    }

    static void ScaleAll(FEMeshPart p, Func<float, float> f, int n = 40)
    {
        var c = Fn(f, n);
        p.scaleX = c; p.scaleY = c; p.scaleZ = c;
    }

    static void Shock(Transform root, string name, Material mat, float delay, float dur, float maxScale)
    {
        var sh = P(root, name, Kind.Ring, mat, new Vector3(0f, 0.05f, 0f), Vector3.zero, delay, dur);
        sh.innerRadius = 0.7f; sh.outerRadius = 1f; sh.segments = 64;
        ScaleXZ(sh, t => Mathf.Lerp(0.5f, maxScale, EaseOut(t, 3f)));
        sh.tracks.Add(T("_Dissolve", t => Seg(t, 0.2f, 1f)));
    }

    // ================================================================ EF08 闇の渦
    static void BuildDarkVortex(FEEffect fx)
    {
        var root = fx.transform;
        fx.lifetime = 2f;
        fx.shakeDelay = 0.5f; fx.shakeAmplitude = 0.14f; fx.shakeDuration = 0.3f;
        const float burst = 0.5f;

        var mSwirl = ColumnMat("EF08_Swirl", Dark).Th(0.7f, 0.35f, 0.15f)
            .F("_Twist", 2.5f).V("_NoiseScale", new Vector4(5f, 3f, 0f, -0.6f))
            .F("_SpokeCount", 4f).F("_SpokeAmt", 0.5f).F("_NoiseAmt", 0.7f)
            .F("_BottomSoft", 0.2f).F("_TopSoft", 0.35f);
        var mRunes = ColumnMat("EF08_Runes", Dark, 0.2f).Th(0.85f, 0.6f, 0.4f)
            .F("_BottomSoft", 0.3f).F("_TopSoft", 0.3f).F("_SpokeCount", 30f).F("_SpokeAmt", 0.8f)
            .F("_NoiseAmt", 0.6f).V("_NoiseScale", new Vector4(30f, 1f, 0f, 0f));
        var mGeyser = ColumnMat("EF08_Geyser", Dark).Th(0.6f, 0.3f, 0.1f)
            .V("_NoiseScale", new Vector4(5f, 1.5f, 4f, 0f)).F("_Twist", 0.4f).F("_NoiseAmt", 0.7f)
            .F("_EdgeNoise", 0.35f).F("_FillSoft", 0.08f).F("_BottomSoft", 0.03f).F("_TopSoft", 0.15f)
            .F("_Fresnel", 0.5f);
        var mCrown = ColumnMat("EF08_Crown", Dark).Th(0.6f, 0.3f, 0.1f)
            .V("_NoiseScale", new Vector4(7f, 1.5f, 3f, 0f)).F("_NoiseAmt", 0.6f)
            .F("_EdgeNoise", 0.5f).F("_FillSoft", 0.06f).F("_BottomSoft", 0.05f).F("_TopSoft", 0.4f)
            .F("_Fresnel", 0.4f);
        var mRipple = ColumnMat("EF08_Ripple", Dark).F("_BottomSoft", 0.5f).F("_TopSoft", 0.25f).F("_NoiseAmt", 0.4f);
        var mDrop = ParticleMat("EF08_Drop", Dark, 0).Th(0.55f, 0.3f, 0.12f).F("_NoiseAmt", 0.3f);
        var mStreak = ParticleMat("EF08_Streak", Dark, 1).Th(0.55f, 0.3f, 0.1f);
        var mShard = ParticleMat("EF08_Shard", DarkCrystal, 6);
        var mHex = ParticleMat("EF08_HexEmblem", DarkCrystal, 4, 0.3f).Th(0.8f, 0.45f, 0.15f);
        var mGlitter = GlitterMat("EF08_Glitter", DarkCrystal);

        // 足元の渦 (溜め)
        var swirl = P(root, "Dark_Swirl", Kind.Ring, mSwirl, new Vector3(0f, 0.02f, 0f), Vector3.zero, 0f, 1.7f);
        swirl.innerRadius = 0.05f; swirl.outerRadius = 2.2f; swirl.segments = 64; swirl.spinSpeed = -140f;
        ScaleXZ(swirl, t => EaseOut(Seg(t, 0f, 0.25f), 2.5f) * (1f + 0.15f * EaseOut(Seg(t, 0.29f, 0.4f), 2f)));
        swirl.tracks.Add(T("_Dissolve", t => Seg(t, 0.75f, 1f)));

        // Engage: ルーン状の外周リング
        var runes = P(root, "Dark_Runes", Kind.Ring, mRunes, new Vector3(0f, 0.03f, 0f), Vector3.zero, 0.05f, 1.6f);
        runes.innerRadius = 2.1f; runes.outerRadius = 2.45f; runes.segments = 96; runes.spinSpeed = 60f;
        ScaleXZ(runes, t => EaseOut(Seg(t, 0f, 0.2f), 3f));
        runes.tracks.Add(T("_Dissolve", t => Seg(t, 0.7f, 1f)));

        // 吸い込まれる黒い粒
        var inward = PS(root, "Dark_Inward", mStreak, new Vector3(0f, 0.05f, 0f), Vector3.zero, 0f, 0.45f);
        Main(inward, 0.4f, 0.6f, 0f, 0f, 0.08f, 0.15f);
        ShapeCircle(inward, 2f, 0.2f, 360f, new Vector3(90f, 0f, 0f), Vector3.one);
        Rate(inward, 40f); FadeOut(inward, 0.6f); Stretch(inward, 2f, 0.05f);
        Orbit(inward, 0f, 3f, -3f);
        SizeLife(inward, t => 1f - 0.6f * t);

        // 破裂: 水柱
        var gey = P(root, "Dark_Geyser", Kind.Tube, mGeyser, Vector3.zero, Vector3.zero, burst, 0.75f);
        gey.bottomRadius = 0.45f; gey.topRadius = 0.75f; gey.height = 3.2f; gey.segments = 32;
        gey.tracks.Add(T("_Fill", t => EaseOut(Seg(t, 0f, 0.25f), 2.5f) * 1.1f));
        gey.tracks.Add(T("_Dissolve", t => Seg(t, 0.35f, 1f)));
        ScaleXZ(gey, t => 0.6f + 0.6f * EaseOut(t, 2f));

        // 破裂: 王冠状のしぶき
        var crown = P(root, "Dark_Crown", Kind.Tube, mCrown, Vector3.zero, Vector3.zero, burst, 0.6f);
        crown.bottomRadius = 0.55f; crown.topRadius = 1.5f; crown.height = 1.1f; crown.segments = 40;
        crown.tracks.Add(T("_Fill", t => EaseOut(Seg(t, 0f, 0.3f), 2.5f) * 1.1f));
        crown.tracks.Add(T("_Dissolve", t => Seg(t, 0.3f, 1f)));
        ScaleXZ(crown, t => Mathf.Lerp(0.6f, 1.7f, EaseOut(t, 2.5f)));

        // 地面の波紋
        var rip = P(root, "Dark_Ripple", Kind.Ring, mRipple, new Vector3(0f, 0.04f, 0f), Vector3.zero, burst, 0.5f);
        rip.innerRadius = 0.75f; rip.outerRadius = 1f; rip.segments = 64;
        ScaleXZ(rip, t => Mathf.Lerp(0.6f, 3.8f, EaseOut(t, 3f)));
        rip.tracks.Add(T("_Dissolve", t => Seg(t, 0.2f, 1f)));

        // 飛び散る黒い水滴
        var drops = PS(root, "Dark_Droplets", mDrop, new Vector3(0f, 0.1f, 0f), new Vector3(-90f, 0f, 0f), burst, 0.1f, true);
        Main(drops, 0.6f, 1f, 5f, 9f, 0.12f, 0.32f, true);
        ShapeCone(drops, 30f, 0.35f);
        Burst(drops, 0f, 26, 34); FadeOut(drops, 0.55f); Gravity(drops, 2.2f);
        SizeLife(drops, t => 1f - 0.4f * t);

        var streaks = PS(root, "Dark_SplashStreaks", mStreak, new Vector3(0f, 0.1f, 0f), new Vector3(-90f, 0f, 0f), burst, 0.1f, true);
        Main(streaks, 0.4f, 0.7f, 7f, 12f, 0.06f, 0.1f);
        ShapeCone(streaks, 22f, 0.3f);
        Burst(streaks, 0f, 16, 22); FadeOut(streaks, 0.4f); Gravity(streaks, 2f); Stretch(streaks, 3f, 0.05f);

        // Engage: 紫の結晶片 + 紋章 + キラキラ
        var shards = PS(root, "Dark_Shards", mShard, new Vector3(0f, 0.2f, 0f), new Vector3(-90f, 0f, 0f), burst, 0.1f, true);
        Main(shards, 0.5f, 0.8f, 4f, 8f, 0.14f, 0.26f);
        ShapeCone(shards, 40f, 0.4f);
        Burst(shards, 0f, 8, 12); FadeOut(shards, 0.5f); Gravity(shards, 1.5f); Stretch(shards, 1.4f, 0f);
        HexEmblem(root, "Dark_HexEmblem", mHex, new Vector3(0f, 1f, 0f), burst, 1.8f, 3f);
        GlitterBurst(root, "Dark_Glitter", mGlitter, new Vector3(0f, 1.2f, 0f), burst + 0.05f, 0.9f, 12, 16);
    }

    // ================================================================ Engage 共通パーツ
    static Material GlitterMat(string name, Pal p)
    {
        return ParticleMat(name, p, 5, 0.5f).Th(0.55f, 0.3f, 0.12f).F("_Intensity", 2.2f);
    }

    // 粒ごとに明滅するキラキラ
    static void Twinkle(ParticleSystem ps)
    {
        SizeLife(ps, t => (0.3f + 0.7f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2.5f))) * (1f - 0.4f * t), 60);
        var m = ps.main;
        m.startRotation = new MMC(-0.3f, 0.3f);
    }

    // 正面向き (ビルボード) の衝撃リング1枚。grow = 最終サイズ倍率, hold = 形を保つ割合
    static void ImpactRing(Transform root, string name, Material mat, float delay, float life, float size, float grow, float hold, float fudge)
    {
        var r = PS(root, name, mat, Vector3.zero, Vector3.zero, delay, 0.1f);
        Main(r, life, life, 0f, 0f, size, size);
        Burst(r, 0f, 1, 1); FadeOut(r, hold);
        SizeLife(r, t => Mathf.Lerp(1f, grow, EaseOut(t, 3f)));
        Fudge(r, fudge);
    }

    static void GlitterBurst(Transform root, string name, Material mat, Vector3 pos, float delay, float radius, int min, int max)
    {
        var g = PS(root, name, mat, pos, Vector3.zero, delay, 0.1f, true);
        Main(g, 0.35f, 0.7f, 0.3f, 1.5f, 0.1f, 0.2f);
        ShapeSphere(g, radius);
        Burst(g, 0f, min, max); FadeOut(g, 0.5f); Drag(g, 2f); Twinkle(g);
    }

    // 六角形の紋章リング (発動・着弾の瞬間に広がる)
    static void HexEmblem(Transform root, string name, Material mat, Vector3 pos, float delay, float size, float grow)
    {
        var h = PS(root, name, mat, pos, Vector3.zero, delay, 0.1f);
        Main(h, 0.28f, 0.28f, 0f, 0f, size, size);
        var m = h.main; m.startRotation = 0f;
        Burst(h, 0f, 1, 1); FadeOut(h, 0.15f);
        SizeLife(h, t => Mathf.Lerp(1f, grow / Mathf.Max(size, 0.01f), EaseOut(t, 3f)));
        Fudge(h, -15f);
    }

    // ================================================================ materials
    static Material Mat(string shader, string name, Pal p, float coreTh, float mainTh, float outTh, float additive, float intensity)
    {
        var sh = Shader.Find(shader);
        if (sh == null)
        {
            Debug.LogError("[FE] Shader not found: " + shader + " (シェーダーのコンパイルエラーを確認してください)");
            sh = Shader.Find("Sprites/Default");
        }
        var m = new Material(sh) { name = name };
        m.SetColor("_CoreColor", p.core);
        m.SetColor("_MainColor", p.main);
        m.SetColor("_OutlineColor", p.outline);
        m.SetColor("_GlowColor", p.glow);
        // 本体帯の中のグラデーション先 = 本体色を芯の色へ寄せたもの (ツヤ)
        var main2 = Color.Lerp(p.main, p.core, 0.45f); main2.a = p.main.a;
        m.SetColor("_MainColor2", main2);
        return m.Th(coreTh, mainTh, outTh).F("_Additive", additive).F("_Intensity", intensity);
    }

    // ---------------- 残像の色
    //  Black    : 黒い残像 + 細い青の縁 (半透明の通常合成) ← 既定
    //  DarkBlue : 暗い紺の残像 + 青の縁
    //  BlueGlow : 以前の、青く光る加算の残像
    //  エディタのメニュー「1. Generate Prefabs (残像: …)」で切り替えて生成できる
    public enum AfterimageStyle { Black, DarkBlue, BlueGlow }
    public static AfterimageStyle Afterimage = AfterimageStyle.Black;

    static readonly Pal AfterBlack = new Pal(new Color(0.04f, 0.04f, 0.07f, 0.75f), new Color(0.02f, 0.02f, 0.04f, 0.75f), new Color(0.12f, 0.4f, 1f, 1f), new Color(0.15f, 0.45f, 1.2f, 0.5f));
    static readonly Pal AfterDarkBlue = new Pal(new Color(0.15f, 0.3f, 0.6f, 0.8f), new Color(0.04f, 0.1f, 0.3f, 0.8f), new Color(0.15f, 0.45f, 1f, 1f), new Color(0.15f, 0.45f, 1.2f, 0.5f));

    static void ApplyAfterimageStyle(Material m)
    {
        if (Afterimage == AfterimageStyle.BlueGlow)
        {
            SetPal(m, SwordAfter);
            m.SetFloat("_Additive", 0.55f);
            m.SetFloat("_Intensity", 1.3f);
            m.SetFloat("_CoreTh", 9f);   // 白い芯なし = サブの青だけ
            m.renderQueue = 3002;
            return;
        }
        // 黒・暗い色は加算だと見えなくなるので、通常の半透明合成で重ねる
        SetPal(m, Afterimage == AfterimageStyle.Black ? AfterBlack : AfterDarkBlue);
        m.SetFloat("_Additive", 0f);
        m.SetFloat("_Intensity", 1f);
        m.SetFloat("_CoreTh", 0.8f);
        m.SetFloat("_MainTh", 0.3f);
        m.SetFloat("_OutlineTh", 0.12f);
        m.SetFloat("_Shade", 0f);
        m.SetFloat("_BackShade", 0f);
        m.renderQueue = 2970;        // 側面・本体より奥 (本体の白に被らない)
    }

    static void SetPal(Material m, Pal p)
    {
        m.SetColor("_CoreColor", p.core);
        m.SetColor("_MainColor", p.main);
        m.SetColor("_OutlineColor", p.outline);
        m.SetColor("_GlowColor", p.glow);
        var main2 = Color.Lerp(p.main, p.core, 0.45f); main2.a = p.main.a;
        m.SetColor("_MainColor2", main2);
    }

    static Material SlashMat(string name, Pal p, float additive = 0f)
    {
        // 縁の外側に発光フチを出すため OutlineTh を少し広めに
        return Mat(SlashShader, name, p, 0.62f, 0.26f, 0.12f, additive, 1.8f)
            .F("_TailLen", 0.75f).F("_HeadTaper", 0.22f).F("_TailPow", 1.1f).F("_OuterSoft", 0.12f)
            .F("_NoiseAmt", 0.35f).V("_NoiseScale", new Vector4(5f, 2f, 3f, 0f));
    }

    static Material ColumnMat(string name, Pal p, float additive = 0f)
    {
        return Mat(ColumnShader, name, p, 0.65f, 0.35f, 0.12f, additive, 1.6f)
            .F("_Fill", 1.1f).F("_FillSoft", 0.08f).F("_EdgeNoise", 0f)
            .F("_BottomSoft", 0.05f).F("_TopSoft", 0.2f).F("_Brightness", 1f)
            .F("_NoiseAmt", 0.5f).V("_NoiseScale", new Vector4(8f, 2f, 2f, 0f))
            .F("_Twist", 0f).F("_SpokeCount", 8f).F("_SpokeAmt", 0f).F("_Fresnel", 0f).F("_Rim", 0f);
    }

    static Material WindConeMat(string name)
    {
        return ColumnMat(name, Wind, 0.5f)
            .F("_Rim", 1f).F("_Fresnel", 0.75f).F("_NoiseAmt", 0.85f)
            .V("_NoiseScale", new Vector4(6f, 1.5f, 6f, 0f)).F("_Twist", 0.6f)
            .F("_BottomSoft", 0.3f).F("_TopSoft", 0.08f).F("_FillSoft", 0.1f);
    }

    static Material ParticleMat(string name, Pal p, int shape, float additive = 0f)
    {
        return Mat(ParticleShader, name, p, 0.6f, 0.3f, 0.1f, additive, 1.6f)
            .F("_Shape", shape).F("_NoiseAmt", 0f).V("_NoiseScale", new Vector4(2f, 2f, 1f, 0f));
    }

    static Material F(this Material m, string n, float v) { m.SetFloat(n, v); return m; }
    static Material V(this Material m, string n, Vector4 v) { m.SetVector(n, v); return m; }
    static Material Th(this Material m, float core, float main, float outline)
    {
        m.SetFloat("_CoreTh", core); m.SetFloat("_MainTh", main); m.SetFloat("_OutlineTh", outline);
        return m;
    }

    // ================================================================ mesh parts
    static FEMeshPart P(Transform parent, string name, Kind kind, Material mat, Vector3 pos, Vector3 euler, float delay, float duration)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        var p = go.AddComponent<FEMeshPart>();
        p.kind = kind;
        p.delay = delay;
        p.duration = duration;
        return p;
    }

    static void Arc(FEMeshPart p, float r, float w, float s, float e, float bend)
    {
        p.radius = r; p.width = w; p.startAngle = s; p.endAngle = e; p.bend = bend; p.segments = 64;
    }

    /// 押し出しの側面: 本体と同じ形のパーツを n 枚作り、カメラ基準で offset まで段階的にずらす。
    /// 遠いものほど暗く、先に描く (renderQueue を本体より手前の番号に固定)。
    static void Extrude(Transform root, string matName, string partName, Pal side, Vector3 offset, int n,
                        Func<Material, Material> shapeProps, Func<int, FEMeshPart> makePart)
    {
        for (int k = n; k >= 1; k--)
        {
            float f = (float)k / n;
            var c = side.main * (1f - 0.4f * f); c.a = 1f;
            var pal = new Pal(c, c, new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f));
            var m = shapeProps(SlashMat(matName + k, pal)).Th(9f, 0.02f, 0.01f)
                .V("_Extrude", new Vector4(offset.x * f, offset.y * f, offset.z * f, 0f))
                .F("_Shade", 0f).F("_BackShade", 0f).F("_NoiseAmt", 0f);
            m.renderQueue = 2980 + Mathf.Min(n - k, 15);   // 本体 (3000) より必ず先に描く
            var part = makePart(k);
            part.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
    }

    static void Arc3D(FEMeshPart p, float depth, float twist, float thickness)
    {
        p.depth = depth; p.twist = twist; p.thickness = thickness;
    }

    static void ScaleXZ(FEMeshPart p, Func<float, float> f)
    {
        p.scaleX = Fn(f);
        p.scaleZ = Fn(f);
    }

    static FEMeshPart.FloatTrack T(string prop, Func<float, float> f)
    {
        return new FEMeshPart.FloatTrack { property = prop, curve = Fn(f), multiplier = 1f };
    }

    // ================================================================ particles
    static ParticleSystem PS(Transform parent, string name, Material mat, Vector3 pos, Vector3 euler, float delay, float duration, bool world = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = Mathf.Max(duration, 0.05f);
        main.loop = false;
        main.playOnAwake = false;
        main.startDelay = delay;
        main.maxParticles = 300;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.startColor = Color.white;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;

        var sh = ps.shape;
        sh.enabled = false;

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = mat;
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    static void Main(ParticleSystem ps, float l0, float l1, float sp0, float sp1, float sz0, float sz1, bool randomRot = false)
    {
        var m = ps.main;
        m.startLifetime = new MMC(l0, l1);
        m.startSpeed = new MMC(sp0, sp1);
        m.startSize = new MMC(sz0, sz1);
        if (randomRot) m.startRotation = new MMC(0f, Mathf.PI * 2f);
    }

    static void Burst(ParticleSystem ps, float time, int min, int max)
    {
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(time, (short)min, (short)max) });
    }

    static void Rate(ParticleSystem ps, float rate)
    {
        var em = ps.emission;
        em.rateOverTime = rate;
    }

    // α = 侵食。hold までは形を保ち、その後縁から削れて消える
    static void FadeOut(ParticleSystem ps, float hold)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Clamp(hold, 0.001f, 0.99f)), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void SizeLife(ParticleSystem ps, Func<float, float> f, int samples = 24)
    {
        var s = ps.sizeOverLifetime;
        s.enabled = true;
        s.size = new MMC(1f, Fn(f, samples));
    }

    // 1粒ずつランダムな向き・速さで回転させる (ラジアン/秒)
    static void Spin(ParticleSystem ps, float maxRadPerSec)
    {
        var r = ps.rotationOverLifetime;
        r.enabled = true;
        r.z = new MMC(-maxRadPerSec, maxRadPerSec);
    }

    static void Drag(ParticleSystem ps, float d)
    {
        var l = ps.limitVelocityOverLifetime;
        l.enabled = true;
        l.limit = 1000f;
        l.drag = d;
        l.multiplyDragByParticleSize = false;
        l.multiplyDragByParticleVelocity = false;
    }

    static void Gravity(ParticleSystem ps, float g)
    {
        var m = ps.main;
        m.gravityModifier = g;
    }

    static void Stretch(ParticleSystem ps, float length, float velocity)
    {
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.lengthScale = length;
        pr.velocityScale = velocity;
    }

    static void Fudge(ParticleSystem ps, float f)
    {
        ps.GetComponent<ParticleSystemRenderer>().sortingFudge = f;
    }

    static void ShapeSphere(ParticleSystem ps, float r)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = r;
    }

    static void ShapeBox(ParticleSystem ps, Vector3 size)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = size;
    }

    static void ShapeCone(ParticleSystem ps, float angle, float radius)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angle;
        sh.radius = radius;
    }

    static void ShapeCircle(ParticleSystem ps, float r, float thickness, float arc, Vector3 rot, Vector3 scale)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = r;
        sh.radiusThickness = thickness;
        sh.arc = arc;
        sh.rotation = rot;
        sh.scale = scale;
    }

    // 斬撃の弧 (FEMeshPart.Arc と同じYZ平面の角度) に沿って放出。loop=true で振りに合わせて順番に
    static void ArcShape(ParticleSystem ps, float r, float start, float end, float sweepTime, bool loop)
    {
        bool increasing = end > start;
        ShapeCircle(ps, r, 0.15f, Mathf.Abs(end - start),
            new Vector3(0f, 90f, 180f - start),
            new Vector3(1f, increasing ? -1f : 1f, 1f));
        if (!loop) return;
        var sh = ps.shape;
        sh.arcMode = ParticleSystemShapeMultiModeValue.Loop;
        sh.arcSpeed = 1f / Mathf.Max(sweepTime, 0.01f);
    }

    static void Orbit(ParticleSystem ps, float ox, float oy, float radial = 0f)
    {
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.x = new MMC(0f); v.y = new MMC(0f); v.z = new MMC(0f);
        v.orbitalX = new MMC(ox); v.orbitalY = new MMC(oy); v.orbitalZ = new MMC(0f);
        v.radial = new MMC(radial);
    }

    static void VelY(ParticleSystem ps, float a, float b)
    {
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.x = new MMC(0f, 0f); v.y = new MMC(a, b); v.z = new MMC(0f, 0f);
    }

    static void LightFlash(Transform parent, Vector3 pos, Color col, float delay, float duration, float peak, float range)
    {
        var go = new GameObject("Flash_Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.range = range;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        var f = go.AddComponent<FELightFlash>();
        f.delay = delay;
        f.duration = duration;
        f.peakIntensity = peak;
        f.curve = Fn(t => Mathf.Min(t / 0.08f, 1f) * (1f - Mathf.Pow(Seg(t, 0.1f, 1f), 0.7f)));
    }

    // ================================================================ curve helpers
    static AnimationCurve Fn(Func<float, float> f, int n = 24)
    {
        var keys = new Keyframe[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            keys[i] = new Keyframe(t, f(t));
        }
        for (int i = 0; i <= n; i++)
        {
            int a = Mathf.Max(i - 1, 0), b = Mathf.Min(i + 1, n);
            float tan = (keys[b].value - keys[a].value) / (keys[b].time - keys[a].time);
            keys[i].inTangent = tan;
            keys[i].outTangent = tan;
        }
        return new AnimationCurve(keys);
    }

    static float Seg(float t, float a, float b) { return Mathf.Clamp01((t - a) / (b - a)); }
    static float EaseOut(float x, float p) { return 1f - Mathf.Pow(1f - Mathf.Clamp01(x), p); }
    static float EaseIn(float x, float p) { return Mathf.Pow(Mathf.Clamp01(x), p); }
}
