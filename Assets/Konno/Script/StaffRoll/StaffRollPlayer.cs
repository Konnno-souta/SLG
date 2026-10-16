using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StaffRoll
{
    public enum RoleLayout
    {
        Stacked,    // 役職の下に名前
        SideBySide  // 役職（右寄せ） | 名前（左寄せ）
    }

    public enum EndMode
    {
        ScrollOut,         // 最後まで流しきる
        StopFinalAtCenter  // ラストのテキスト/画像を画面中央で止める
    }

    /// <summary>
    /// Canvas 内の RectTransform（表示範囲）にアタッチして使う。
    /// 中身は再生時に自動生成される。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class StaffRollPlayer : MonoBehaviour
    {
        [Header("データ（どちらか）")]
        public StaffRollData data;
        [Tooltip("data が空の時、このテキストから生成")]
        public TextAsset textSource;

        [Header("再生")]
        public bool playOnStart = true;
        public float startDelay = 1f;
        [Tooltip("スクロール速度 (px/秒)")]
        public float scrollSpeed = 80f;
        [Tooltip("ON にすると速度ではなく総再生時間で指定（BGMの長さに合わせる時など）")]
        public bool useDuration = false;
        public float duration = 90f;
        [Tooltip("Time.timeScale の影響を受けない")]
        public bool useUnscaledTime = true;
        public EndMode endMode = EndMode.StopFinalAtCenter;
        [Tooltip("StopFinalAtCenter 時、止まってから消えるまでの秒数")]
        public float finalHoldTime = 3f;
        public bool loop = false;

        [Header("早送り・スキップ")]
        public bool allowFastForward = true;
        public float fastForwardMultiplier = 4f;
        public bool allowSkip = true;
        [Tooltip("スキップに必要な長押し秒数（0で即スキップ）")]
        public float skipHoldTime = 1f;
        public float skipFadeTime = 0.5f;
        [Tooltip("組み込みの入力判定を使う（早送り: Space/Enter/クリック/Aボタン、スキップ: Esc/Startボタン）")]
        public bool useBuiltInInput = true;

        [Header("フェード")]
        public float fadeInTime = 1f;
        public float fadeOutTime = 1.5f;
        [Tooltip("上下端のぼかし幅 (px)。0で無効")]
        public int edgeSoftness = 80;

        [Header("レイアウト")]
        public RoleLayout roleLayout = RoleLayout.Stacked;
        public TextAlignmentOptions stackedAlignment = TextAlignmentOptions.Center;
        [Tooltip("中身の横幅。0以下なら表示範囲いっぱい")]
        public float contentWidth = 1200f;
        public float sideBySideGap = 48f;
        public float roleNameGap = 8f;
        public float entrySpacing = 40f;
        public float titleSpacing = 50f;
        public float sectionSpacing = 160f;
        public float beforeFinalSpacing = 300f;
        public float nameLineSpacing = 10f;

        [Header("フォント")]
        public TMP_FontAsset font;

        [Header("セクション見出し")]
        public float titleSize = 52f;
        public Color titleColor = new Color(1f, 0.85f, 0.5f);
        public FontStyles titleStyle = FontStyles.Bold;

        [Header("役職")]
        public float roleSize = 28f;
        public Color roleColor = new Color(0.75f, 0.75f, 0.75f);
        public FontStyles roleStyle = FontStyles.Normal;

        [Header("名前")]
        public float nameSize = 38f;
        public Color nameColor = Color.white;
        public FontStyles nameStyle = FontStyles.Normal;

        [Header("テキスト行")]
        public float textSize = 36f;
        public Color textColor = Color.white;
        public FontStyles textStyle = FontStyles.Italic;

        [Header("ラストのテキスト")]
        public float finalTextSize = 64f;
        public Color finalTextColor = Color.white;
        public FontStyles finalTextStyle = FontStyles.Bold;

        [Header("イベント")]
        public UnityEvent onStarted;
        public UnityEvent onSkipped;
        public UnityEvent onFinished;

        // ---- 公開状態 ----
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; set; }
        /// <summary>0〜1 の進行度</summary>
        public float Progress => Mathf.Approximately(endY, startY) ? 0f : Mathf.InverseLerp(startY, endY, pos);
        /// <summary>スキップ長押しゲージ 0〜1（UI表示用）</summary>
        public float SkipProgress => skipHoldTime <= 0f ? 0f : Mathf.Clamp01(skipTimer / skipHoldTime);

        // ---- 内部 ----
        RectTransform viewport;
        RectTransform content;
        CanvasGroup group;
        RectMask2D mask;
        RectTransform finalElement;
        RectTransform lastElement;
        StaffRollData activeData;
        Coroutine routine;

        float pos, startY, endY;
        float skipTimer;
        bool skipRequested;
        bool inputFastForward;
        bool externalFastForward;

        float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float SpeedMul => allowFastForward && (inputFastForward || externalFastForward) ? fastForwardMultiplier : 1f;

        void Awake()
        {
            viewport = (RectTransform)transform;
            if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            if (!TryGetComponent(out mask)) mask = gameObject.AddComponent<RectMask2D>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        void Update()
        {
            if (!IsPlaying || !useBuiltInInput) return;

            ReadInput(out bool ff, out bool skipHeld);
            inputFastForward = ff;

            if (allowSkip && skipHeld)
            {
                skipTimer += Time.unscaledDeltaTime;
                if (skipTimer >= skipHoldTime) Skip();
            }
            else
            {
                skipTimer = 0f;
            }
        }

        // ================= 公開API =================

        public void Play() => Play(null);

        public void Play(StaffRollData overrideData)
        {
            Stop();
            activeData = overrideData != null ? overrideData
                       : data != null ? data
                       : textSource != null ? StaffRollData.FromText(textSource.text)
                       : null;

            if (activeData == null)
            {
                Debug.LogWarning("[StaffRoll] データが設定されていません", this);
                return;
            }
            routine = StartCoroutine(PlayRoutine());
        }

        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            IsPlaying = false;
            if (group) group.alpha = 0f;
        }

        public void Skip()
        {
            if (allowSkip && IsPlaying) skipRequested = true;
        }

        /// <summary>独自の入力処理から早送りを制御したい時に使う</summary>
        public void SetFastForward(bool on) => externalFastForward = on;

        // ================= 再生 =================

        IEnumerator PlayRoutine()
        {
            IsPlaying = true;
            IsPaused = false;
            skipRequested = false;
            skipTimer = 0f;

            if (mask) mask.softness = new Vector2Int(0, Mathf.Max(0, edgeSoftness));

            Build();
            group.alpha = 0f;
            yield return null; // TMP のメッシュ生成を1フレーム待つ

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            float vh = viewport.rect.height;
            startY = -vh; // 中身の上端が画面下端にある状態
            float contentH = content.rect.height;

            RectTransform stopTarget = finalElement != null ? finalElement : lastElement;
            endY = endMode == EndMode.StopFinalAtCenter && stopTarget != null
                ? CenterStopY(stopTarget, vh)
                : contentH;
            if (endY < startY) endY = startY;

            float speed = useDuration && duration > 0f ? (endY - startY) / duration : scrollSpeed;

            pos = startY;
            SetY(pos);
            onStarted?.Invoke();

            // 開始待ち
            float t = 0f;
            while (t < startDelay && !skipRequested)
            {
                if (!IsPaused) t += Dt * SpeedMul;
                yield return null;
            }

            // スクロール
            float fadeT = 0f;
            while (pos < endY && !skipRequested)
            {
                if (!IsPaused)
                {
                    float dt = Dt;
                    pos = Mathf.Min(endY, pos + speed * dt * SpeedMul);
                    fadeT += dt;
                    group.alpha = fadeInTime > 0f ? Mathf.Clamp01(fadeT / fadeInTime) : 1f;
                    SetY(pos);
                }
                yield return null;
            }

            // ラストで停止
            if (!skipRequested)
            {
                group.alpha = 1f;
                if (endMode == EndMode.StopFinalAtCenter)
                {
                    t = 0f;
                    while (t < finalHoldTime && !skipRequested)
                    {
                        if (!IsPaused) t += Dt * SpeedMul;
                        yield return null;
                    }
                }
            }

            // フェードアウト
            bool skipped = skipRequested;
            float fo = skipped ? Mathf.Min(fadeOutTime, skipFadeTime) : fadeOutTime;
            float a0 = group.alpha;
            t = 0f;
            while (t < fo)
            {
                t += Dt;
                group.alpha = Mathf.Lerp(a0, 0f, t / fo);
                yield return null;
            }
            group.alpha = 0f;

            IsPlaying = false;
            routine = null;
            if (skipped) onSkipped?.Invoke();
            onFinished?.Invoke();

            if (loop && !skipped) Play(activeData);
        }

        void SetY(float y)
        {
            var p = content.anchoredPosition;
            p.y = y;
            content.anchoredPosition = p;
        }

        /// <summary>target の中心が画面中央に来る content の Y 位置</summary>
        float CenterStopY(RectTransform target, float viewportHeight)
        {
            Vector3 centerWorld = target.TransformPoint(target.rect.center);
            Vector3 local = content.InverseTransformPoint(centerWorld);
            float centerFromTop = -local.y; // content のピボットは上端
            return centerFromTop - viewportHeight * 0.5f;
        }

        // ================= 構築 =================

        void Build()
        {
            if (content == null)
            {
                var go = new GameObject("Content", typeof(RectTransform));
                content = (RectTransform)go.transform;
                content.SetParent(viewport, false);

                var vlg = go.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 0f;

                var fitter = go.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // 横幅
            if (contentWidth > 0f)
            {
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(contentWidth, content.sizeDelta.y);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
            }
            content.pivot = new Vector2(0.5f, 1f);

            // 既存の中身を削除
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            finalElement = null;
            lastElement = null;

            var sections = activeData.sections;
            for (int s = 0; s < sections.Count; s++)
            {
                var sec = sections[s];

                if (!string.IsNullOrEmpty(sec.title))
                {
                    CreateText(content, sec.title, titleSize, titleColor, titleStyle, TextAlignmentOptions.Center);
                    CreateSpace(content, titleSpacing);
                }

                for (int e = 0; e < sec.entries.Count; e++)
                {
                    BuildEntry(sec.entries[e]);
                    bool isLast = e == sec.entries.Count - 1;
                    if (!isLast && sec.entries[e].type != EntryType.Space) CreateSpace(content, entrySpacing);
                }

                bool lastSection = s == sections.Count - 1;
                if (!lastSection) CreateSpace(content, sec.spacingAfter >= 0f ? sec.spacingAfter : sectionSpacing);
            }

            bool hasFinal = activeData.finalImage != null || !string.IsNullOrEmpty(activeData.finalText);
            if (hasFinal)
            {
                CreateSpace(content, beforeFinalSpacing);

                // 画像とテキストをまとめて1ブロックにし、そのブロックを中央で止める
                var block = CreateVerticalGroup(content, "Final", 24f);
                if (activeData.finalImage != null)
                    CreateImage(block, activeData.finalImage, activeData.finalImageSize);
                if (!string.IsNullOrEmpty(activeData.finalText))
                    CreateText(block, activeData.finalText, finalTextSize, finalTextColor, finalTextStyle, TextAlignmentOptions.Center);
                finalElement = block;
            }
        }

        void BuildEntry(StaffRollEntry entry)
        {
            float nSize = entry.fontSizeOverride > 0f ? entry.fontSizeOverride : nameSize;
            float tSize = entry.fontSizeOverride > 0f ? entry.fontSizeOverride : textSize;

            switch (entry.type)
            {
                case EntryType.Space:
                    CreateSpace(content, entry.spaceHeight);
                    break;

                case EntryType.Text:
                    CreateText(content, entry.names, tSize, textColor, textStyle, stackedAlignment);
                    break;

                case EntryType.Image:
                    if (entry.image != null) CreateImage(content, entry.image, entry.imageSize);
                    break;

                case EntryType.RoleAndNames:
                    if (roleLayout == RoleLayout.SideBySide)
                    {
                        var row = new GameObject("Row", typeof(RectTransform));
                        row.transform.SetParent(content, false);
                        var h = row.AddComponent<HorizontalLayoutGroup>();
                        h.spacing = sideBySideGap;
                        h.childControlWidth = true;
                        h.childControlHeight = true;
                        h.childForceExpandWidth = true;
                        h.childForceExpandHeight = false;
                        h.childAlignment = TextAnchor.UpperCenter;

                        var r = CreateText(row.transform, entry.role ?? "", roleSize, roleColor, roleStyle, TextAlignmentOptions.TopRight);
                        var n = CreateText(row.transform, entry.names ?? "", nSize, nameColor, nameStyle, TextAlignmentOptions.TopLeft);
                        r.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                        n.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                        lastElement = (RectTransform)row.transform;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(entry.role))
                        {
                            CreateText(content, entry.role, roleSize, roleColor, roleStyle, stackedAlignment);
                            CreateSpace(content, roleNameGap);
                        }
                        CreateText(content, entry.names ?? "", nSize, nameColor, nameStyle, stackedAlignment);
                    }
                    break;
            }
        }

        // ================= 部品生成 =================

        TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.lineSpacing = nameLineSpacing;
            t.raycastTarget = false;
            lastElement = (RectTransform)go.transform;
            return t;
        }

        void CreateImage(Transform parent, Sprite sprite, Vector2 size)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            le.minHeight = size.y;
            lastElement = (RectTransform)go.transform;
        }

        void CreateSpace(Transform parent, float height)
        {
            if (height <= 0f) return;
            var go = new GameObject("Space", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        RectTransform CreateVerticalGroup(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        // ================= 入力 =================

        void ReadInput(out bool fastForward, out bool skipHeld)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            var mouse = Mouse.current;
            fastForward =
                (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed)) ||
                (mouse != null && mouse.leftButton.isPressed) ||
                (gp != null && gp.buttonSouth.isPressed);
            skipHeld =
                (kb != null && kb.escapeKey.isPressed) ||
                (gp != null && gp.startButton.isPressed);
#else
            fastForward = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetMouseButton(0);
            skipHeld = Input.GetKey(KeyCode.Escape);
#endif
        }
    }
}
