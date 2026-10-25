using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CutIn
{
    public enum CutInExit
    {
        Collapse,  // 帯が上下からつぶれて閉じる
        SlideOut,  // 右へ流れて出ていく
        Fade       // フェードで消える
    }

    /// <summary>
    /// 左から流れてくるカットインを表示する。
    /// 空の GameObject に付けるだけで動く（Canvas などは自動で作る）。
    /// </summary>
    public class CutInPlayer : MonoBehaviour
    {
        [Header("画面")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        [Tooltip("他の UI より手前に出す順番")]
        public int sortingOrder = 200;
        [Tooltip("文字のフォント（日本語を出すなら日本語の TMP フォント）")]
        public TMP_FontAsset font;

        [Header("演出")]
        [Tooltip("カットイン中に画面を暗くする強さ")]
        [Range(0f, 1f)] public float dimAlpha = 0.45f;
        [Tooltip("キャラが入ってきた瞬間に画面を光らせる")]
        public bool flash = true;
        [Range(0f, 1f)] public float flashAlpha = 0.6f;
        public float flashTime = 0.25f;
        [Tooltip("消え方")]
        public CutInExit exitMode = CutInExit.Collapse;
        [Tooltip("キャラが入ってくる時の行き過ぎ（大きいほど勢いよく止まる）")]
        [Range(0f, 3f)] public float overshoot = 1.2f;

        [Header("時間（秒）")]
        [Tooltip("帯が左から入ってくる時間")]
        public float bandInTime = 0.18f;
        [Tooltip("キャラが左から入ってくる時間")]
        public float characterInTime = 0.3f;
        [Tooltip("キャラが入ってくる距離 (px)")]
        public float characterInDistance = 800f;
        [Tooltip("消える時間")]
        public float exitTime = 0.22f;

        [Header("雷")]
        [Tooltip("雷が左から右へ伸びきるまでの時間")]
        public float lightningRevealTime = 0.1f;
        [Tooltip("雷の形を時々作り直す間隔（秒）。0 で同じ形のまま")]
        public float lightningReshapeInterval = 0.3f;

        [Header("流れる線")]
        public int speedLineCount = 26;
        [Tooltip("線が流れる速さ (px/秒)")]
        public float speedLineSpeed = 2800f;

        [Header("その他")]
        [Tooltip("カットイン中はゲームを止める（Time.timeScale = 0）")]
        public bool pauseGame = false;
        [Tooltip("空なら自動で作る")]
        public AudioSource audioSource;

        [Header("イベント")]
        public UnityEvent onStarted;
        public UnityEvent onFinished;

        /// <summary>カットインを表示中か</summary>
        public bool IsPlaying { get; private set; }

        // ---- 内部 ----
        class SpeedLine
        {
            public RectTransform rt;
            public Image image;
            public float factor;
        }

        RectTransform root, bandRoot, band, character, label, edgeTop, edgeBottom, linesRoot;
        CanvasGroup rootGroup;
        Image dim, flashImage, bandBackground, characterImage, edgeTopImage, edgeBottomImage;
        TextMeshProUGUI labelText;
        CutInLightning lightning;
        CutInBandShape bandShape, bandOutline;
        Image halftoneImage, strikeTint, strikeFlashImage, openLineImage;
        RectTransform openLine;
        Sprite glowLineSprite;
        float bandInDuration;   // 帯が出きるまでの時間（出し方によって変わる）
        bool slideIn;           // 帯が左から流れてくるか
        int strikeIndex = -1;
        Sprite halftoneSprite;
        readonly List<CutInBandShape> layerShapes = new List<CutInBandShape>();
        readonly List<float> layerDelays = new List<float>();
        readonly List<float> layerBaseX = new List<float>();
        float elapsed;
        Vector2 bandSize;
        Sprite lineSprite;
        Texture2D gradientTexture;
        Sprite gradientSprite;
        readonly List<SpeedLine> lines = new List<SpeedLine>();
        Coroutine routine;
        float bandWidth;
        float flashTimer;
        Vector2 characterBase, labelBase;
        Vector2 bandDir, bandPos;
        float characterBaseAngle;   // キャラの元の傾き   // 帯の向き（傾きに沿った方向）と、止まる位置
        bool lightningActive;
        float lightningTime, lightningFlickerTimer, lightningReshapeTimer, lightningLength;

        static float Dt => Time.unscaledDeltaTime;

        // =====================================================================
        // 公開API
        // =====================================================================

        /// <summary>カットインを再生する（再生中なら止めてから再生し直す）</summary>
        public void Play(CutInData data)
        {
            if (data == null) { Debug.LogWarning("[CutIn] CutInData が設定されていません", this); return; }
            if (!data.character) Debug.LogWarning($"[CutIn] {data.name} にキャラクター画像が入っていません", data);
            Stop();
            routine = StartCoroutine(Run(data));
        }

        /// <summary>カットインを再生し、終わるまで待つ（コルーチンから yield return で使う）</summary>
        public IEnumerator PlayAndWait(CutInData data)
        {
            Play(data);
            while (IsPlaying) yield return null;
        }

        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (IsPlaying) Finish(invokeEvent: false);
        }

        // =====================================================================
        // 再生
        // =====================================================================

        IEnumerator Run(CutInData d)
        {
            EnsureUI();
            Setup(d);
            IsPlaying = true;
            root.gameObject.SetActive(true);
            rootGroup.alpha = 1f;
            if (pauseGame) Time.timeScale = 0f;
            onStarted?.Invoke();
            PlaySound(d.se, d.volume);

            float t = 0f;
            elapsed = 0f;
            if (slideIn)
            {
                // 1. 帯が左から入ってくる
                while (t < bandInTime)
                {
                    t += Dt;
                    float k = EaseOutCubic(t / bandInTime);
                    bandRoot.anchoredPosition = bandPos + bandDir * Mathf.Lerp(-bandWidth, 0f, k);   // 傾きに沿って左から
                    SetDim(dimAlpha * k);
                    Tick(d);
                    yield return null;
                }
            }
            else
            {
                // 1. 細い光の線が出る → 上下に一気に開く（FE 風）
                bandRoot.anchoredPosition = bandPos;
                float lineTime = Mathf.Max(0f, d.lineTime);
                float openTime = Mathf.Max(0.01f, d.openTime);
                while (t < lineTime + openTime)
                {
                    t += Dt;
                    float lineIn = lineTime > 0f ? Mathf.Clamp01(t / Mathf.Min(0.04f, lineTime)) : 1f;   // 線がパッと出る
                    float k = EaseOutCubic((t - lineTime) / openTime);                                       // 開く
                    SetBandOpen(k, d, lineIn * (1f - k));
                    SetDim(dimAlpha * Mathf.Clamp01(t / (lineTime + openTime)));
                    Tick(d);
                    yield return null;
                }
                SetBandOpen(1f, d, 0f);
            }
            bandRoot.anchoredPosition = bandPos;
            SetDim(dimAlpha);

            // 2. キャラが左から勢いよく入ってくる（同時に光って、雷が走る）
            PlaySound(d.voice, d.volume);
            // Strike の白い光を使う時は、画面全体のフラッシュは出さない（FE の動画と同じ）
            bool bandFlash = d.lightning && d.lightningStyle == LightningStyle.Strike && d.strikeFlash;
            flashTimer = flash && !bandFlash ? flashTime : 0f;
            lightningActive = d.lightning;
            lightningTime = 0f;

            if (d.motion == CharacterMotion.PullBackAndDash)
            {
                // FE 風：行き過ぎ → 左へ戻る → ゆっくり右へ → 一気に右へ抜ける（抜ける途中で帯が閉じる）
                yield return PullBackAndDashMotion(d);
            }
            else
            {
                yield return StayMotion(d);
            }

            routine = null;
            Finish(invokeEvent: true);
        }

        // =====================================================================
        // キャラの動き
        // =====================================================================

        /// <summary>
        /// 入ってきて止まり、そのまま帯と一緒に消える（Drift があればゆっくり動く）
        /// </summary>
        IEnumerator StayMotion(CutInData d)
        {
            float t = 0f;
            while (t < characterInTime)
            {
                t += Dt;
                float k = EaseOutBack(t / characterInTime, overshoot);
                float dx = -(1f - k) * characterInDistance;
                character.anchoredPosition = characterBase + new Vector2(dx, 0f);
                label.anchoredPosition = labelBase + new Vector2(dx * 0.6f, 0f);
                labelText.alpha = Mathf.Clamp01(t / characterInTime);
                Tick(d);
                yield return null;
            }
            character.anchoredPosition = characterBase;
            label.anchoredPosition = labelBase;
            labelText.alpha = 1f;

            t = 0f;
            Vector2 drift = Vector2.zero;
            while (t < d.holdTime)
            {
                t += Dt;
                drift += d.drift * Dt;
                character.anchoredPosition = characterBase + drift;
                Tick(d);
                yield return null;
            }

            t = 0f;
            while (t < exitTime)
            {
                t += Dt;
                ApplyExit(Mathf.Clamp01(t / exitTime));
                drift += d.drift * Dt;
                character.anchoredPosition = characterBase + drift;
                Tick(d);
                yield return null;
            }
        }

        /// <summary>
        /// FE 風の動き（動画から計測した流れ）
        ///   a. 左から一気に入ってきて、真ん中を少し行き過ぎる
        ///   b. 少しずつ左へ戻る（このとき時計回りに少し傾く）
        ///   c. ゆっくり右へ動き出す（だんだん速く）
        ///   d. 一気に右へ走り抜けて画面の外へ。抜けている途中で帯が閉じる
        /// </summary>
        IEnumerator PullBackAndDashMotion(CutInData d)
        {
            float over = Mathf.Max(0f, d.pullBackDistance);   // 行き過ぎる距離（＝左へ戻る距離）
            float x = 0f;
            float rot = 0f;

            // a. 入ってくる（行き過ぎるところまで）
            float t = 0f;
            float enter = Mathf.Max(0.01f, d.enterTime);
            while (t < enter)
            {
                t += Dt;
                float k = EaseOutCubic(t / enter);
                x = Mathf.Lerp(-characterInDistance, over, k);
                SetCharacter(x, 0f);
                label.anchoredPosition = labelBase + new Vector2(Mathf.Min(0f, x - over) * 0.6f, 0f);
                labelText.alpha = k;
                Tick(d);
                yield return null;
            }
            x = over;
            SetCharacter(x, 0f);
            label.anchoredPosition = labelBase;
            labelText.alpha = 1f;

            // （少しだけ止める。0 なら止まらない）
            t = 0f;
            while (t < d.holdBeforePullBack)
            {
                t += Dt;
                Tick(d);
                yield return null;
            }

            // b. 左へ戻る（だんだんゆっくり）＋ 時計回りに少し傾く
            t = 0f;
            while (t < d.pullBackTime)
            {
                t += Dt;
                float k = EaseOutCubic(t / d.pullBackTime);
                x = Mathf.Lerp(over, 0f, k);
                rot = d.pullBackRotation * k;
                SetCharacter(x, rot);
                Tick(d);
                yield return null;
            }
            x = 0f;

            // c. ゆっくり右へ動き出す（だんだん速く）
            t = 0f;
            while (t < d.creepTime)
            {
                t += Dt;
                float k = Mathf.Clamp01(t / d.creepTime);
                x = d.creepDistance * k * k;
                SetCharacter(x, rot);
                Tick(d);
                yield return null;
            }
            x = d.creepDistance;

            // d. 一気に右へ走り抜ける。帯は、抜けきるのと同時に閉じ終わるように途中から閉じ始める
            float dash = Mathf.Max(0.01f, d.dashTime);
            float closeTime = Mathf.Max(0.01f, d.dashCloseTime);
            float exitStart = d.closeDuringDash ? Mathf.Max(0f, dash - closeTime) : float.MaxValue;
            if (!d.closeDuringDash) closeTime = exitTime;
            float exitT = 0f;
            float startRot = rot;
            t = 0f;
            while (t < dash)
            {
                t += Dt;
                // 最初から少しずつ右へ流れて、どんどん速くなる（帯が細くなる間もキャラが見える）
                float k = EaseInQuad(t / dash);
                x = d.creepDistance + d.dashDistance * k;
                if (d.straightenOnDash)
                    rot = Mathf.Lerp(startRot, 0f, EaseOutCubic(Mathf.Clamp01(t / (dash * 0.5f))));
                SetCharacter(x, rot);

                if (t >= exitStart)
                {
                    exitT += Dt;
                    ApplyExit(Mathf.Clamp01(exitT / closeTime));
                }
                Tick(d);
                yield return null;
            }
            characterImage.enabled = false;   // 画面の外に出たので消す

            // 帯がまだ閉じきっていなければ、残りを閉じる
            while (exitT < closeTime)
            {
                exitT += Dt;
                ApplyExit(Mathf.Clamp01(exitT / closeTime));
                Tick(d);
                yield return null;
            }
        }

        /// <summary>帯の開き具合（0: 閉じている → 1: 開ききった）と、細い光の線の濃さ</summary>
        void SetBandOpen(float k, CutInData d, float lineAlpha)
        {
            float sy = Mathf.Max(0.001f, k);
            bandRoot.localScale = new Vector3(1f, sy, 1f);

            bool showLine = lineAlpha > 0.001f;
            openLine.gameObject.SetActive(showLine);
            if (showLine)
            {
                // 帯と一緒に縦につぶれないように、つぶれた分だけ太くしておく
                float h = Mathf.Max(1f, d.lineThickness) * 5f;   // 周りの光の分も含めた太さ
                openLine.sizeDelta = new Vector2(bandWidth, h / sy);
                var c = d.lineColor;
                c.a *= lineAlpha;
                openLineImage.color = c;
            }
        }

        /// <summary>キャラの位置（帯に沿った x）と、時計回りの傾き（度）を設定</summary>
        void SetCharacter(float x, float clockwise)
        {
            character.anchoredPosition = characterBase + new Vector2(x, 0f);
            character.localRotation = Quaternion.Euler(0f, 0f, characterBaseAngle - clockwise);   // Unity はマイナスが時計回り
        }

        /// <summary>帯の消え方（k: 0 → 1）</summary>
        void ApplyExit(float k)
        {
            switch (exitMode)
            {
                case CutInExit.Collapse:
                    bandRoot.localScale = new Vector3(1f, Mathf.Max(0.001f, 1f - EaseInQuad(k)), 1f);
                    break;
                case CutInExit.SlideOut:
                    bandRoot.anchoredPosition = bandPos + bandDir * Mathf.Lerp(0f, bandWidth, EaseInCubic(k));
                    break;
                case CutInExit.Fade:
                    rootGroup.alpha = 1f - k;
                    break;
            }
            SetDim(dimAlpha * (1f - k));
        }

        void Finish(bool invokeEvent)
        {
            if (root) root.gameObject.SetActive(false);
            if (pauseGame) Time.timeScale = 1f;
            lightningActive = false;
            IsPlaying = false;
            if (invokeEvent) onFinished?.Invoke();
        }

        /// <summary>毎フレームの更新（流れる線・雷・フラッシュ）</summary>
        void Tick(CutInData d)
        {
            float dt = Dt;
            elapsed += dt;
            UpdateLayers();

            if (d.speedLines)
            {
                float half = bandSize.x * 0.5f;
                foreach (var l in lines)
                {
                    var p = l.rt.anchoredPosition;
                    p.x += speedLineSpeed * l.factor * dt;
                    if (p.x - l.rt.sizeDelta.x > half) p.x = -half - Random.Range(0f, half * 0.5f);
                    l.rt.anchoredPosition = p;
                }
            }

            if (d.lightning && d.lightningStyle == LightningStyle.Strike) TickStrike(d, dt);
            else if (lightningActive) TickLightning(d, dt);

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                var c = Color.white;
                c.a = flashAlpha * Mathf.Clamp01(flashTimer / flashTime);
                flashImage.color = c;
            }
            else if (flashImage.color.a > 0f)
            {
                flashImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        /// <summary>
        /// FE 風の雷（動画から見た流れ）
        ///   帯が入ってくる間 : まっすぐな細い光の線。帯の中は Strike Tint の色
        ///   Strike Times の各タイミング : 大きな波の雷がドンと走る → まっすぐになりながら消える。帯の色もすぐ元に戻る
        /// </summary>
        void TickStrike(CutInData d, float dt)
        {
            lightningTime += dt;
            float tint = 0f;
            var L = lightning;

            if (lightningTime < 0f)
            {
                // 帯が入ってくる間：まっすぐな細い線
                if (d.strikePreLine)
                {
                    L.SetShapeScale(0f);
                    L.reveal = 1f;
                    L.intensity = 0.9f;
                    L.coreWidth = d.lightningCoreWidth * 0.4f;
                    L.glowWidth = d.lightningGlowWidth * 0.35f;
                }
                else
                {
                    L.intensity = 0f;
                    L.Refresh();
                }
                tint = 1f;
            }
            else
            {
                // 今走っている雷を探す
                int active = -1;
                float local = 0f;
                float duration = Mathf.Max(0.01f, d.strikeDuration);
                if (d.strikeTimes != null)
                {
                    for (int i = 0; i < d.strikeTimes.Count; i++)
                    {
                        float l = lightningTime - d.strikeTimes[i];
                        if (l >= 0f && l < duration) { active = i; local = l; }
                    }
                }

                if (active >= 0)
                {
                    // 新しい雷が走るたびに形を作り直す
                    if (active != strikeIndex)
                    {
                        strikeIndex = active;
                        L.GenerateSmooth(-lightningLength * 0.5f, lightningLength, d.strikeAmplitude, d.strikeWaves, d.strikeRoughness);
                    }

                    float p = local / duration;
                    // 最初の3割は大きな波のまま → そのあと、だんだんまっすぐになる
                    float shape = p < 0.3f ? 1f : 1f - EaseOutCubic((p - 0.3f) / 0.7f);
                    L.SetShapeScale(shape);
                    L.reveal = lightningRevealTime > 0f ? Mathf.Clamp01(local / lightningRevealTime) : 1f;
                    L.intensity = p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f;
                    L.coreWidth = d.lightningCoreWidth * Mathf.Lerp(1f, 0.5f, p);
                    L.glowWidth = d.lightningGlowWidth * Mathf.Lerp(1f, 0.6f, p);
                    tint = p < 0.25f ? 1f : 1f - EaseOutCubic((p - 0.25f) / 0.75f);
                }
                else if (L.intensity > 0f)
                {
                    L.intensity = 0f;
                    L.Refresh();
                }
            }

            var c = d.strikeTintColor;
            c.a *= tint;
            strikeTint.color = c;

            // 雷が走った後、帯の中が白く光る（いちばん新しい雷を基準にする）
            if (d.strikeFlash)
            {
                float flashA = 0f;
                if (lightningTime >= 0f && d.strikeTimes != null)
                {
                    float latest = float.MaxValue;
                    foreach (var st in d.strikeTimes)
                    {
                        float l = lightningTime - st;
                        if (l >= 0f && l < latest) latest = l;
                    }
                    if (latest < float.MaxValue)
                    {
                        float s = latest - Mathf.Max(0f, d.strikeFlashDelay);
                        if (s >= 0f)
                        {
                            float hold = Mathf.Max(0f, d.strikeFlashHold);
                            flashA = s < hold ? 1f : 1f - EaseOutCubic((s - hold) / Mathf.Max(0.01f, d.strikeFlashFade));
                        }
                    }
                }
                var fc = d.strikeFlashColor;
                fc.a *= flashA;
                strikeFlashImage.color = fc;
            }
        }

        void TickLightning(CutInData d, float dt)
        {
            lightningTime += dt;

            // 左から一気に伸びる
            lightning.reveal = lightningRevealTime > 0f ? Mathf.Clamp01(lightningTime / lightningRevealTime) : 1f;

            // Bolt（塗りつぶしの稲妻）は形を保ったまま
            if (d.lightningStyle == LightningStyle.Bolt)
            {
                lightning.Refresh();
                return;
            }

            // 時々形を作り直す
            if (lightningReshapeInterval > 0f)
            {
                lightningReshapeTimer -= dt;
                if (lightningReshapeTimer <= 0f)
                {
                    lightningReshapeTimer = lightningReshapeInterval;
                    if (lightningTime > lightningRevealTime)
                        lightning.Generate(-lightningLength * 0.5f, lightningLength, d.lightningAmplitude, d.lightningSegments);
                }
            }

            // チラつき（形の小さな揺れと明るさ）
            if (d.lightningFlicker > 0f)
            {
                lightningFlickerTimer -= dt;
                if (lightningFlickerTimer <= 0f)
                {
                    lightningFlickerTimer = d.lightningFlicker;
                    lightning.intensity = Random.Range(0.65f, 1f);
                    lightning.Jitter(d.lightningJitter);
                    return;
                }
            }
            lightning.Refresh();
        }

        // =====================================================================
        // 見た目の設定
        // =====================================================================

        void Setup(CutInData d)
        {
            // 帯
            bandWidth = Mathf.Max(referenceResolution.x, referenceResolution.y) * 1.8f;  // 傾けても画面を覆う長さ
            bandRoot.localRotation = Quaternion.Euler(0f, 0f, d.bandAngle);
            bandRoot.localScale = Vector3.one;
            float rad = d.bandAngle * Mathf.Deg2Rad;
            bandDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            bandPos = d.bandOffset;
            slideIn = d.bandOpen == BandOpen.SlideFromLeft;
            bandInDuration = slideIn ? bandInTime : Mathf.Max(0f, d.lineTime) + Mathf.Max(0.01f, d.openTime);
            if (slideIn)
            {
                bandRoot.anchoredPosition = bandPos - bandDir * bandWidth;
            }
            else
            {
                bandRoot.anchoredPosition = bandPos;
                bandRoot.localScale = new Vector3(1f, 0.001f, 1f);   // 最初は閉じている
            }
            openLine.gameObject.SetActive(false);

            // 形と大きさ（Wedge は画面の左端から伸びる三角形）
            bool wedge = d.bandShape == BandShape.Wedge;
            float screenW = referenceResolution.x;
            if (wedge)
            {
                float len = screenW * d.wedgeLength;
                bandSize = new Vector2(len, d.wedgeHeight);
                band.anchoredPosition = new Vector2(-screenW * 0.5f + len * 0.5f, 0f);
            }
            else
            {
                bandSize = new Vector2(bandWidth, d.bandHeight);
                band.anchoredPosition = Vector2.zero;
            }
            band.sizeDelta = bandSize;
            int seed = Random.Range(1, int.MaxValue);
            ApplyShape(bandShape, d, seed, bandSize);

            // 後ろに重ねる帯
            SetupLayers(d);

            // 網点
            halftoneImage.gameObject.SetActive(d.halftone);
            if (d.halftone)
            {
                halftoneImage.color = d.halftoneColor;
                // 点の画像は 16px 間隔で作ってあるので、倍率で間隔を変える
                halftoneImage.pixelsPerUnitMultiplier = 16f / Mathf.Max(2f, d.halftoneSpacing);
            }

            if (d.bandBackground)
            {
                bandBackground.sprite = d.bandBackground;
                bandBackground.color = Color.white;
            }
            else
            {
                bandBackground.sprite = MakeGradient(d.bandColorLeft, d.bandColorRight);
                bandBackground.color = Color.white;
            }

            // ふちの線（Slash はギザギザに沿って、Wedge は斜めの2辺、Rectangle は上下）
            bool edge = d.bandEdgeColor.a > 0f && d.bandEdgeThickness > 0f;
            bool shapedEdge = d.bandShape != BandShape.Rectangle || d.bandFlare < 0.999f;
            bandOutline.gameObject.SetActive(edge && shapedEdge);
            if (edge && shapedEdge)
            {
                bandOutline.color = d.bandEdgeColor;
                bandOutline.outlineWidth = d.bandEdgeThickness;
                bandOutline.rectTransform.anchoredPosition = band.anchoredPosition;
                ApplyShape(bandOutline, d, seed, bandSize);   // 塗りと同じ形
            }
            edge &= !shapedEdge;
            edgeTop.gameObject.SetActive(edge);
            edgeBottom.gameObject.SetActive(edge);
            if (edge)
            {
                edgeTopImage.color = edgeBottomImage.color = d.bandEdgeColor;
                edgeTop.sizeDelta = edgeBottom.sizeDelta = new Vector2(bandWidth, d.bandEdgeThickness);
                edgeTop.anchoredPosition = new Vector2(0f, d.bandHeight * 0.5f);
                edgeBottom.anchoredPosition = new Vector2(0f, -d.bandHeight * 0.5f);
            }

            // 流れる線
            linesRoot.gameObject.SetActive(d.speedLines);
            EnsureLines();
            float half = bandSize.x * 0.5f;
            foreach (var l in lines)
            {
                float len = Random.Range(180f, 700f);
                float thick = Random.Range(2f, 6f);
                l.rt.sizeDelta = new Vector2(len, thick);
                l.rt.anchoredPosition = new Vector2(Random.Range(-half, half), Random.Range(-bandSize.y * 0.45f, bandSize.y * 0.45f));
                l.factor = Random.Range(0.6f, 1.4f);
                var c = d.speedLineColor;
                c.a *= Random.Range(0.4f, 1f);
                l.image.color = c;
            }

            // 雷が走る瞬間に帯の中を染める色（Strike の時だけ）
            bool strike = d.lightning && d.lightningStyle == LightningStyle.Strike;
            strikeTint.gameObject.SetActive(strike && d.strikeTintColor.a > 0f);
            strikeTint.color = new Color(d.strikeTintColor.r, d.strikeTintColor.g, d.strikeTintColor.b, 0f);
            strikeFlashImage.gameObject.SetActive(strike && d.strikeFlash);
            strikeFlashImage.color = new Color(d.strikeFlashColor.r, d.strikeFlashColor.g, d.strikeFlashColor.b, 0f);

            // 雷（キャラが入ってくる時に走り出す）
            lightningActive = false;
            lightningFlickerTimer = 0f;
            lightningReshapeTimer = lightningReshapeInterval;
            lightning.gameObject.SetActive(d.lightning);
            if (d.lightning)
            {
                lightning.style = d.lightningStyle;
                lightning.coreColor = d.lightningCoreColor;
                lightning.glowColor = d.lightningGlowColor;
                lightning.coreWidth = d.lightningCoreWidth;
                lightning.glowWidth = d.lightningGlowWidth;
                lightning.boltColorStart = d.boltColorStart;
                lightning.boltColorEnd = d.boltColorEnd;
                lightning.boltThickness = d.boltThickness;
                lightning.boltTaper = d.boltTaper;
                lightning.boltOutlineColor = d.boltOutlineColor;
                lightning.boltOutlineWidth = d.boltOutlineWidth;
                lightning.rectTransform.anchoredPosition = new Vector2(0f, d.lightningY);
                lightning.reveal = 0f;
                lightning.intensity = 1f;

                if (d.lightningStyle == LightningStyle.Bolt)
                {
                    // 帯（または画面）の左端から、先端の手前まで
                    float boltSpan = wedge ? bandSize.x : screenW;
                    float boltLen = boltSpan * d.boltLength;
                    lightning.Generate(-boltSpan * 0.5f, boltLen, d.boltAmplitude, d.boltSegments);
                }
                else if (d.lightningStyle == LightningStyle.Strike)
                {
                    // FE 風：帯が入ってくる間は「まっすぐな細い線」。キャラが入ってきたら波の雷が走る
                    lightningLength = wedge ? bandSize.x : Mathf.Max(referenceResolution.x, referenceResolution.y) * 1.3f;
                    lightning.GenerateSmooth(-lightningLength * 0.5f, lightningLength, d.strikeAmplitude, d.strikeWaves, d.strikeRoughness);
                    lightning.SetShapeScale(0f);
                    lightning.intensity = 0f;
                    lightningTime = -bandInDuration;   // キャラが入ってくる瞬間が 0
                    strikeIndex = -1;
                }
                else
                {
                    lightningLength = wedge ? bandSize.x : Mathf.Max(referenceResolution.x, referenceResolution.y) * 1.3f;
                    lightning.Generate(-lightningLength * 0.5f, lightningLength, d.lightningAmplitude, d.lightningSegments);
                }

                // キャラの後ろ／手前
                // 後に並んだものほど手前に描かれる
                if (d.lightningBehindCharacter)
                {
                    lightning.transform.SetAsLastSibling();
                    character.SetAsLastSibling();          // 雷 → キャラ の順（キャラが手前）
                }
                else
                {
                    character.SetAsLastSibling();
                    lightning.transform.SetAsLastSibling();  // キャラ → 雷 の順（雷が手前）
                }
            }

            // キャラクター
            characterImage.enabled = d.character;
            if (d.character)
            {
                characterImage.sprite = d.character;
                characterImage.SetNativeSize();
            }
            characterImage.color = d.characterColor;
            float s = Mathf.Max(0.01f, d.characterScale);
            character.localScale = new Vector3(d.flipX ? -s : s, s, 1f);
            // 帯と一緒に傾く（Character Upright がオンなら、傾きを打ち消してまっすぐにする）
            float charAngle = d.characterRotation - (d.characterUpright ? d.bandAngle : 0f);
            characterBaseAngle = charAngle;
            character.localRotation = Quaternion.Euler(0f, 0f, charAngle);
            characterBase = d.characterOffset;
            character.anchoredPosition = characterBase - new Vector2(characterInDistance, 0f);

            // 文字
            bool hasLabel = !string.IsNullOrEmpty(d.label);
            label.gameObject.SetActive(hasLabel);
            if (hasLabel)
            {
                if (font) labelText.font = font;
                labelText.text = d.label;
                labelText.color = d.labelColor;
                labelText.fontSize = d.labelSize;
                labelText.alpha = 0f;
            }
            labelBase = d.labelOffset;
            label.anchoredPosition = labelBase;

            SetDim(0f);
            flashTimer = 0f;
            flashImage.color = new Color(1f, 1f, 1f, 0f);
        }

        void SetDim(float a) => dim.color = new Color(0f, 0f, 0f, a);

        void ApplyShape(CutInBandShape g, CutInData d, int seed, Vector2 size)
        {
            g.shape = d.bandShape;
            g.jag = d.slashJag;
            g.jagDensity = d.slashJagDensity;
            g.seed = seed;
            // 画面の左端 → 右端 にかけて広がる（帯の中心から見た x）
            g.flareLeft = d.bandShape == BandShape.Wedge ? 1f : d.bandFlare;
            g.flareFromX = -referenceResolution.x * 0.5f;
            g.flareToX = referenceResolution.x * 0.5f;
            g.rectTransform.sizeDelta = size;
            g.Refresh();
        }

        void SetupLayers(CutInData d)
        {
            int count = d.backLayers != null ? d.backLayers.Count : 0;
            while (layerShapes.Count < count)
            {
                var go = new GameObject("BackLayer", typeof(RectTransform));
                go.transform.SetParent(bandRoot, false);
                var g = go.AddComponent<CutInBandShape>();
                g.raycastTarget = false;
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                layerShapes.Add(g);
            }
            layerDelays.Clear();
            layerBaseX.Clear();

            for (int i = 0; i < layerShapes.Count; i++)
            {
                var g = layerShapes[i];
                bool use = i < count && d.backLayers[i] != null;
                g.gameObject.SetActive(use);
                layerDelays.Add(use ? d.backLayers[i].delay : 0f);
                if (!use) { layerBaseX.Add(0f); continue; }

                var layer = d.backLayers[i];
                g.color = layer.color;
                g.rectTransform.localScale = Vector3.one;
                var size = new Vector2(bandSize.x, bandSize.y + layer.extraHeight);
                ApplyShape(g, d, Random.Range(1, int.MaxValue), size);
                Vector2 pos = band.anchoredPosition + layer.offset;
                g.rectTransform.anchoredPosition = pos;
                layerBaseX.Add(pos.x);

                // リストの後ろのものほど奥に（メインの帯より奥に並べる）
                g.transform.SetSiblingIndex(0);
            }
        }

        /// <summary>後ろの帯を、メインの帯より少し遅れて入ってこさせる</summary>
        void UpdateLayers()
        {
            if (!slideIn) return;   // 線から開く時は、メインの帯と一緒に開く
            float main = Mathf.Lerp(-bandWidth, 0f, EaseOutCubic(elapsed / bandInTime));
            for (int i = 0; i < layerShapes.Count && i < layerDelays.Count; i++)
            {
                var g = layerShapes[i];
                if (!g.gameObject.activeSelf) continue;
                float lagged = Mathf.Lerp(-bandWidth, 0f, EaseOutCubic((elapsed - layerDelays[i]) / bandInTime));
                var p = g.rectTransform.anchoredPosition;
                p.x = layerBaseX[i] + (lagged - main);   // 帯の向き（ローカルの x）に沿って遅れる
                g.rectTransform.anchoredPosition = p;
            }
        }

        void PlaySound(AudioClip clip, float volume)
        {
            if (!clip) return;
            if (!audioSource)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(clip, volume);
        }

        // =====================================================================
        // UI の構築
        // =====================================================================

        void EnsureUI()
        {
            if (root) return;

            var canvasGo = new GameObject("CutInCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            rootGroup = canvasGo.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
            root = (RectTransform)canvasGo.transform;

            dim = NewImage(root, "Dim", null);
            Stretch(dim.rectTransform);

            // 帯（傾き・位置・閉じる動きを担当）
            bandRoot = NewRect("BandRoot", root);
            bandRoot.anchorMin = bandRoot.anchorMax = new Vector2(0.5f, 0.5f);
            bandRoot.sizeDelta = Vector2.zero;

            // 帯の中身（はみ出た部分は Mask で切る。傾けても正しく切れるように RectMask2D ではなく Mask を使う）
            var bandGo = new GameObject("Band", typeof(RectTransform));
            bandGo.transform.SetParent(bandRoot, false);
            bandShape = bandGo.AddComponent<CutInBandShape>();
            bandShape.raycastTarget = false;
            band = bandShape.rectTransform;
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            bandGo.AddComponent<Mask>().showMaskGraphic = false;

            bandBackground = NewImage(band, "Background", null);
            Stretch(bandBackground.rectTransform);

            halftoneSprite = MakeHalftoneSprite();
            halftoneImage = NewImage(band, "Halftone", halftoneSprite);
            halftoneImage.type = Image.Type.Tiled;
            Stretch(halftoneImage.rectTransform);

            strikeTint = NewImage(band, "StrikeTint", null);
            Stretch(strikeTint.rectTransform);

            strikeFlashImage = NewImage(band, "StrikeFlash", null);
            Stretch(strikeFlashImage.rectTransform);
            strikeFlashImage.color = new Color(1f, 1f, 1f, 0f);

            linesRoot = NewRect("SpeedLines", band);
            linesRoot.anchorMin = linesRoot.anchorMax = new Vector2(0.5f, 0.5f);
            linesRoot.sizeDelta = Vector2.zero;

            var lightningGo = new GameObject("Lightning", typeof(RectTransform));
            lightningGo.transform.SetParent(band, false);
            lightning = lightningGo.AddComponent<CutInLightning>();
            lightning.raycastTarget = false;
            var lrt = lightning.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = Vector2.zero;

            characterImage = NewImage(band, "Character", null);
            character = characterImage.rectTransform;
            character.anchorMin = character.anchorMax = new Vector2(0.5f, 0.5f);
            character.pivot = new Vector2(0.5f, 0.5f);

            edgeTopImage = NewImage(bandRoot, "EdgeTop", null);
            edgeTop = edgeTopImage.rectTransform;
            edgeBottomImage = NewImage(bandRoot, "EdgeBottom", null);
            edgeBottom = edgeBottomImage.rectTransform;
            foreach (var e in new[] { edgeTop, edgeBottom })
                e.anchorMin = e.anchorMax = new Vector2(0.5f, 0.5f);

            var outlineGo = new GameObject("BandOutline", typeof(RectTransform));
            outlineGo.transform.SetParent(bandRoot, false);
            bandOutline = outlineGo.AddComponent<CutInBandShape>();
            bandOutline.outlineOnly = true;
            bandOutline.raycastTarget = false;
            bandOutline.rectTransform.anchorMin = bandOutline.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            // 帯が開く前の、細い光の線
            glowLineSprite = MakeGlowLineSprite();
            openLineImage = NewImage(bandRoot, "OpenLine", glowLineSprite);
            openLine = openLineImage.rectTransform;
            openLine.anchorMin = openLine.anchorMax = new Vector2(0.5f, 0.5f);
            openLine.gameObject.SetActive(false);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(bandRoot, false);
            labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.raycastTarget = false;
            labelText.outlineWidth = 0.15f;
            labelText.outlineColor = new Color(0f, 0f, 0f, 0.8f);
            label = labelText.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
            label.sizeDelta = new Vector2(1200f, 160f);

            flashImage = NewImage(root, "Flash", null);
            Stretch(flashImage.rectTransform);
            flashImage.color = new Color(1f, 1f, 1f, 0f);

            lineSprite = MakeLineSprite();
            root.gameObject.SetActive(false);
        }

        void EnsureLines()
        {
            while (lines.Count < speedLineCount)
            {
                var img = NewImage(linesRoot, "Line", lineSprite);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.pivot = new Vector2(1f, 0.5f);   // 先頭（右端）を基準に流す
                lines.Add(new SpeedLine { rt = img.rectTransform, image = img });
            }
            for (int i = 0; i < lines.Count; i++)
                lines[i].rt.gameObject.SetActive(i < speedLineCount);
        }

        static Image NewImage(Transform parent, string objName, Sprite sprite)
        {
            var go = new GameObject(objName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform NewRect(string objName, Transform parent)
        {
            var go = new GameObject(objName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // =====================================================================
        // 画像の自動生成
        // =====================================================================

        /// <summary>左の色 → 右の色 の横グラデーション</summary>
        Sprite MakeGradient(Color left, Color right)
        {
            if (gradientTexture) Destroy(gradientTexture);
            if (gradientSprite) Destroy(gradientSprite);
            const int w = 128;
            gradientTexture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < w; x++) gradientTexture.SetPixel(x, 0, Color.Lerp(left, right, x / (float)(w - 1)));
            gradientTexture.Apply();
            gradientSprite = Sprite.Create(gradientTexture, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f));
            return gradientSprite;
        }

        /// <summary>網点（16px 間隔の丸い点）。並べて敷き詰めて使う</summary>
        static Sprite MakeHalftoneSprite()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            float r = size * 0.28f;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - dist + 0.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>真ん中が白く光り、上下に向かってぼんやり消える線（縦のグラデーション）</summary>
        static Sprite MakeGlowLineSprite()
        {
            const int w = 4, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;    // 中心 0 → ふち 1
                float core = v < 0.2f ? 1f : 0f;                     // 芯（太さの 2 割）
                float glow = Mathf.Pow(1f - v, 2f) * 0.7f;           // 周りの光
                float a = Mathf.Clamp01(Mathf.Max(core, glow));
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        /// <summary>右端が濃く、左に向かって薄くなる線</summary>
        static Sprite MakeLineSprite()
        {
            const int w = 64, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = x / (float)(w - 1);
                    float edge = 1f - Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * Mathf.Clamp01(edge * 1.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        // =====================================================================
        // イージング
        // =====================================================================

        static float EaseOutCubic(float k) { k = Mathf.Clamp01(k); float u = 1f - k; return 1f - u * u * u; }
        static float EaseInCubic(float k) { k = Mathf.Clamp01(k); return k * k * k; }
        static float EaseInQuad(float k) { k = Mathf.Clamp01(k); return k * k; }

        static float EaseOutBack(float k, float s)
        {
            k = Mathf.Clamp01(k) - 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        void OnDisable()
        {
            if (IsPlaying && pauseGame) Time.timeScale = 1f;
        }
    }
}