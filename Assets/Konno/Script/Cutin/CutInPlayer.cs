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
        Sprite lineSprite;
        Texture2D gradientTexture;
        Sprite gradientSprite;
        readonly List<SpeedLine> lines = new List<SpeedLine>();
        Coroutine routine;
        float bandWidth;
        float flashTimer;
        Vector2 characterBase, labelBase;

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

            // 1. 帯が左から入ってくる
            float t = 0f;
            while (t < bandInTime)
            {
                t += Dt;
                float k = EaseOutCubic(t / bandInTime);
                bandRoot.anchoredPosition = new Vector2(Mathf.Lerp(-bandWidth, 0f, k), 0f);
                SetDim(dimAlpha * k);
                Tick(d);
                yield return null;
            }
            bandRoot.anchoredPosition = Vector2.zero;
            SetDim(dimAlpha);

            // 2. キャラが左から勢いよく入ってくる
            PlaySound(d.voice, d.volume);
            flashTimer = flash ? flashTime : 0f;
            t = 0f;
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

            // 3. 少しの間ゆっくり動きながら見せる
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

            // 4. 消える
            t = 0f;
            while (t < exitTime)
            {
                t += Dt;
                float k = Mathf.Clamp01(t / exitTime);
                switch (exitMode)
                {
                    case CutInExit.Collapse:
                        bandRoot.localScale = new Vector3(1f, 1f - EaseInCubic(k), 1f);
                        break;
                    case CutInExit.SlideOut:
                        bandRoot.anchoredPosition = new Vector2(Mathf.Lerp(0f, bandWidth, EaseInCubic(k)), 0f);
                        break;
                    case CutInExit.Fade:
                        rootGroup.alpha = 1f - k;
                        break;
                }
                SetDim(dimAlpha * (1f - k));
                drift += d.drift * Dt;
                character.anchoredPosition = characterBase + drift;
                Tick(d);
                yield return null;
            }

            routine = null;
            Finish(invokeEvent: true);
        }

        void Finish(bool invokeEvent)
        {
            if (root) root.gameObject.SetActive(false);
            if (pauseGame) Time.timeScale = 1f;
            IsPlaying = false;
            if (invokeEvent) onFinished?.Invoke();
        }

        /// <summary>毎フレームの更新（流れる線・フラッシュ）</summary>
        void Tick(CutInData d)
        {
            float dt = Dt;

            if (d.speedLines)
            {
                float half = bandWidth * 0.5f;
                foreach (var l in lines)
                {
                    var p = l.rt.anchoredPosition;
                    p.x += speedLineSpeed * l.factor * dt;
                    if (p.x - l.rt.sizeDelta.x > half) p.x = -half - Random.Range(0f, half * 0.5f);
                    l.rt.anchoredPosition = p;
                }
            }

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

        // =====================================================================
        // 見た目の設定
        // =====================================================================

        void Setup(CutInData d)
        {
            // 帯
            bandWidth = Mathf.Max(referenceResolution.x, referenceResolution.y) * 1.8f;  // 傾けても画面を覆う長さ
            bandRoot.localRotation = Quaternion.Euler(0f, 0f, d.bandAngle);
            bandRoot.localScale = Vector3.one;
            bandRoot.anchoredPosition = new Vector2(-bandWidth, 0f);
            band.sizeDelta = new Vector2(bandWidth, d.bandHeight);

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

            // 上下のふち
            bool edge = d.bandEdgeColor.a > 0f && d.bandEdgeThickness > 0f;
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
            float half = bandWidth * 0.5f;
            foreach (var l in lines)
            {
                float len = Random.Range(180f, 700f);
                float thick = Random.Range(2f, 6f);
                l.rt.sizeDelta = new Vector2(len, thick);
                l.rt.anchoredPosition = new Vector2(Random.Range(-half, half), Random.Range(-d.bandHeight * 0.48f, d.bandHeight * 0.48f));
                l.factor = Random.Range(0.6f, 1.4f);
                var c = d.speedLineColor;
                c.a *= Random.Range(0.4f, 1f);
                l.image.color = c;
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
            character.localRotation = Quaternion.Euler(0f, 0f, d.characterRotation);
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
            var bandImage = NewImage(bandRoot, "Band", null);
            band = bandImage.rectTransform;
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            band.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            bandBackground = NewImage(band, "Background", null);
            Stretch(bandBackground.rectTransform);

            linesRoot = NewRect("SpeedLines", band);
            linesRoot.anchorMin = linesRoot.anchorMax = new Vector2(0.5f, 0.5f);
            linesRoot.sizeDelta = Vector2.zero;

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
