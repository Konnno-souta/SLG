using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace Conversation
{
    [Serializable] public class ChoiceEvent : UnityEvent<string> { }
    [Serializable] public class StringEvent : UnityEvent<string> { }

    /// <summary>
    /// 会話パート。空の GameObject にアタッチするだけで動く。
    /// Canvas と UI は実行時に自動生成される。
    /// </summary>
    public class ConversationPlayer : MonoBehaviour
    {
        // =====================================================================
        // 設定
        // =====================================================================

        [Header("データ")]
        public ConversationDatabase database;
        public TextAsset scriptAsset;
        public bool playOnStart = true;
        [Tooltip("終了時に会話UIを消す")]
        public bool hideOnFinish = true;

        [Header("CSV")]
        [Tooltip("Text: Script Asset を再生 / Csv: Csv Asset の Story Id を再生")]
        public ScriptSource source = ScriptSource.Text;
        public TextAsset csvAsset;
        [Tooltip("再生するストーリーID")]
        public string storyId;
        [Tooltip("CSVの見出し名（1行目）。表に合わせて変更")]
        public CsvColumns csvColumns = new CsvColumns();
        [Tooltip("話者が画面にいなければ自動で登場させる")]
        public bool autoShowSpeakers = true;

        [Header("画面")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        public int sortingOrder = 100;
        public TMP_FontAsset font;

        [Header("テキストウィンドウ")]
        [Tooltip("ウィンドウ画像（空ならグラデーションを自動生成）。9スライス推奨")]
        public Sprite windowSprite;
        public Color windowColor = Color.white;
        public float boxHeight = 400f;
        [Tooltip("windowSprite が空の時のグラデーション。左が下端、右が上端")]
        public Gradient boxGradient = DefaultBoxGradient();
        public float sidePadding = 180f;
        [Tooltip("ウィンドウ上端から名前までの距離")]
        public float nameAreaTop = 100f;
        public float nameSize = 46f;
        public Color nameColor = Color.white;
        public float affiliationSize = 30f;
        public Color affiliationColor = new Color(0.45f, 0.78f, 1f);
        public Color separatorColor = new Color(1f, 1f, 1f, 0.45f);
        public float bodySize = 38f;
        public Color bodyColor = Color.white;
        public float bodyLineSpacing = 12f;

        [Header("カーソルUI")]
        [Tooltip("カーソル画像（空なら▼を自動生成）")]
        public Sprite cursorSprite;
        public Color cursorColor = new Color(0.45f, 0.78f, 1f);
        public Vector2 cursorSize = new Vector2(30f, 22f);
        [Tooltip("ウィンドウ右下からの位置 (x: 右端から, y: 下端から)")]
        public Vector2 cursorOffset = new Vector2(150f, 100f);
        public float cursorBobHeight = 6f;
        [Tooltip("1秒あたりの上下回数")]
        public float cursorBobSpeed = 1.5f;

        [Header("キャラクター")]
        public float characterHeight = 1050f;
        public float leftX = 0.22f;
        public float centerX = 0.5f;
        public float rightX = 0.78f;
        [Tooltip("テキスト表示時に話者が小さく跳ねる")]
        public bool speakerHop = true;
        public float hopHeight = 18f;
        public float hopDuration = 0.22f;
        [Tooltip("話者以外に黒の半透明レイヤーを乗せる")]
        public bool dimNonSpeakers = true;
        [Range(0f, 1f)] public float dimAlpha = 0.5f;
        [Tooltip("ナレーション中は全員を暗くする")]
        public bool dimAllOnNarration = false;
        public float characterFadeTime = 0.25f;
        public float characterMoveTime = 0.3f;
        public float appearRise = 30f;
        public float jumpHeight = 40f;
        public float shakeStrength = 18f;
        public float emoteSize = 90f;
        public Color emoteColor = new Color(1f, 0.82f, 0.2f);

        [Header("演出")]
        public float backgroundFadeTime = 0.5f;
        public float defaultScreenFadeTime = 0.6f;
        public Color screenFadeColor = Color.black;

        [Header("UI（ログ・スキップ）")]
        public bool showUiBar = true;
        public Color uiBarColor = new Color(0f, 0f, 0f, 0.55f);
        public float uiBarHeight = 56f;
        public float uiItemWidth = 190f;
        [Tooltip("画面右下からの余白")]
        public Vector2 uiBarMargin = new Vector2(24f, 20f);
        public float uiTextSize = 24f;
        public Color uiTextColor = Color.white;
        public Color uiBadgeColor = new Color(1f, 1f, 1f, 0.9f);
        public Color uiBadgeTextColor = new Color(0.1f, 0.15f, 0.25f);
        public string logLabel = "ログ";
        public string skipLabel = "スキップ";
        [Tooltip("ボタンアイコンの代わりに出す文字")]
        public string logKeyLabel = "L2";
        public string skipKeyLabel = "OPT";
        [Tooltip("ボタンアイコン画像（設定すると文字の代わりに表示）")]
        public Sprite logButtonIcon;
        public Sprite skipButtonIcon;

        [Header("ストーリースキップ")]
        public bool allowStorySkip = true;
        [Tooltip("長押しに必要な秒数（サークルがこの時間で1周）")]
        public float skipHoldSeconds = 2f;
        public Color skipRingColor = new Color(0.45f, 0.78f, 1f);
        public Color skipRingBackColor = new Color(1f, 1f, 1f, 0.2f);
        public float skipFadeTime = 0.5f;

        [Header("ログ")]
        [Range(0f, 1f)] public float logDimAlpha = 0.65f;
        public int maxLogEntries = 200;
        public float logEntryWidth = 1400f;
        public float logEntrySpacing = 36f;
        public float logIconSize = 170f;
        public float logTextSize = 34f;
        public float logNameSize = 28f;
        [Tooltip("ログのウィンドウ画像（空なら角丸を自動生成）")]
        public Sprite logWindowSprite;
        public Color logWindowColor = new Color(0.06f, 0.1f, 0.18f, 0.92f);
        [Tooltip("一番新しい（現在の）ログのウィンドウ色")]
        public Color logCurrentWindowColor = new Color(0.1f, 0.2f, 0.36f, 0.96f);
        public Color logNameTabColor = new Color(0.22f, 0.5f, 0.88f, 1f);
        public Color logIconBackColor = new Color(1f, 1f, 1f, 0.08f);
        [Tooltip("スティック/矢印キーでのスクロール速度 (px/秒)")]
        public float logScrollSpeed = 1400f;
        [Tooltip("マウスホイール1目盛りのスクロール量 (px)")]
        public float logWheelStep = 160f;

        [Header("選択肢")]
        public float choiceWidth = 900f;
        public float choiceHeight = 96f;
        public float choiceTextSize = 36f;
        public Color choiceColor = new Color(1f, 1f, 1f, 0.95f);
        public Color choiceTextColor = new Color(0.15f, 0.2f, 0.3f);

#if ENABLE_INPUT_SYSTEM
        [Header("入力（キーバインドシートに合わせて変更してください）")]
        public InputAction advanceAction = new InputAction("Advance", InputActionType.Button);
        public InputAction storySkipAction = new InputAction("StorySkip", InputActionType.Button);
        public InputAction logAction = new InputAction("Log", InputActionType.Button);
        public InputAction logCloseAction = new InputAction("LogClose", InputActionType.Button);
        public InputAction logScrollAction = new InputAction("LogScroll", InputActionType.Value, expectedControlType: "Axis");
#else
        [Header("入力（キーバインドシートに合わせて変更してください）")]
        public KeyCode advanceKey = KeyCode.Space;
        public KeyCode advanceKey2 = KeyCode.Return;
        public KeyCode storySkipKey = KeyCode.Escape;
        public KeyCode logKey = KeyCode.L;
        public KeyCode logCloseKey = KeyCode.Backspace;
#endif

        [Header("サウンド")]
        [Tooltip("空なら同じ GameObject の ConversationAudio を使う（無ければ自動で追加）")]
        public ConversationAudio audioPlayer;
        [Tooltip("会話が終わった時にBGMを止める")]
        public bool stopBgmOnFinish = true;
        public float finishBgmFadeOut = 1.5f;
        [Tooltip("会話が終わった時に鳴っているSEを止める")]
        public bool stopSeOnFinish = true;
        [Tooltip("ストーリースキップ時のBGM・SEのフェードアウト秒数")]
        public float skipSoundFadeOut = 0.5f;

        [Header("UI効果音（空なら鳴らない）")]
        [Tooltip("テキスト送り")]
        public AudioClip advanceSe;
        public AudioClip logOpenSe;
        public AudioClip logCloseSe;
        [Tooltip("ストーリースキップ実行")]
        public AudioClip skipSe;
        [Tooltip("選択肢の決定")]
        public AudioClip choiceSelectSe;
        [Range(0f, 1f)] public float uiSeVolume = 1f;

        [Header("イベント")]
        public UnityEvent onStarted;
        public UnityEvent onFinished;
        [Tooltip("ストーリースキップされた時（onFinished より先に呼ばれる）")]
        public UnityEvent onSkipped;
        [Tooltip("選んだ選択肢の文字列が渡される")]
        public ChoiceEvent onChoiceSelected;
        [Tooltip("テキストが表示された時。テキストIDが渡される")]
        public StringEvent onTextShown;

        // =====================================================================
        // 内部
        // =====================================================================

        public bool IsPlaying { get; private set; }
        public bool IsLogOpen => logOpen;
        /// <summary>再生中のストーリーID（CSV再生時）</summary>
        public string CurrentStoryId { get; private set; }
        /// <summary>表示中のテキストID（CSV再生時）</summary>
        public string CurrentTextId { get; private set; }
        /// <summary>最後に読み込んだCSV（ストーリーID一覧などの取得用）</summary>
        public ConversationCsv LoadedCsv { get; private set; }

        class StageCharacter
        {
            public string id;
            public CharacterProfile profile;
            public string expression;
            public RectTransform holder;   // 位置（左右移動）
            public RectTransform motion;   // 跳ねる・揺れる
            public Image image;            // 立ち絵
            public Image dimOverlay;       // 黒の半透明レイヤー
            public CanvasGroup group;
            public Coroutine moveCo;
        }

        class LogEntry
        {
            public string name;
            public string affiliation;
            public string text;
            public Sprite icon;
            public bool dedicatedIcon;
            public Vector2 faceCenter;
            public float zoom;
        }

        RectTransform root, bgLayer, charLayer, boxRoot, choicePanel, cursor;
        RectTransform logRoot, logContent;
        CanvasGroup rootGroup, boxGroup, logGroup;
        Image currentBg, fadeOverlay, separator, skipRing, skipRingBack;
        TextMeshProUGUI nameText, bodyText;
        HoldButton skipHoldButton;
        Sprite roundSprite, gradientSprite, triangleSprite, circleSprite, ringSprite;

        ConversationScript script;
        int index;
        bool advanceRequested, choosing, boxVisible, logOpen, finishing;
        string currentSpeakerId;
        float skipTimer, logScroll, logMaxScroll, logOpenOffset;
        Coroutine runRoutine;

        readonly Dictionary<string, StageCharacter> stage =
            new Dictionary<string, StageCharacter>(StringComparer.OrdinalIgnoreCase);
        readonly List<LogEntry> logEntries = new List<LogEntry>();

        static float Dt => Time.unscaledDeltaTime;

        // =====================================================================
        // ライフサイクル
        // =====================================================================

        void Reset() => SetDefaultBindings(true);
        void Awake()
        {
            SetDefaultBindings(false);
            if (!audioPlayer && !TryGetComponent(out audioPlayer))
                audioPlayer = gameObject.AddComponent<ConversationAudio>();
        }

        void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            advanceAction.Enable(); storySkipAction.Enable(); logAction.Enable();
            logCloseAction.Enable(); logScrollAction.Enable();
#endif
        }

        void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            advanceAction.Disable(); storySkipAction.Disable(); logAction.Disable();
            logCloseAction.Disable(); logScrollAction.Disable();
#endif
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        void Update()
        {
            if (!IsPlaying || finishing) return;

            if (logOpen)
            {
                UpdateLog();
            }
            else
            {
                if (!choosing && AdvancePressed()) advanceRequested = true;
                if (LogPressed()) OpenLog();
                UpdateStorySkip();
            }

            UpdateCharacterDim();
            UpdateCursor();
        }

        // =====================================================================
        // 公開API
        // =====================================================================

        /// <summary>インスペクターの設定（source）に従って再生</summary>
        public void Play()
        {
            if (source == ScriptSource.Csv) PlayStory(csvAsset, storyId);
            else Play(scriptAsset);
        }

        public void Play(TextAsset asset)
        {
            if (asset == null) { Debug.LogWarning("[Conversation] スクリプトが設定されていません", this); return; }
            CurrentStoryId = null;
            Play(asset.text);
        }

        public void Play(string scriptText) => Play(ConversationScript.Parse(scriptText));

        /// <summary>Csv Asset から指定ストーリーを再生</summary>
        public void PlayStory(string id) => PlayStory(csvAsset, id);

        public void PlayStory(TextAsset csv, string id)
        {
            if (csv == null) { Debug.LogWarning("[Conversation] CSVが設定されていません", this); return; }
            LoadedCsv = ConversationCsv.Parse(csv.text, csvColumns);
            var s = LoadedCsv.BuildScript(id);
            if (s == null) return;
            CurrentStoryId = id;
            Play(s);
        }

        public void Play(ConversationScript s)
        {
            if (s == null) return;
            if (audioPlayer && !audioPlayer.database) audioPlayer.database = database;
            EnsureUI();
            Stop();
            ResetStage();
            script = s;
            root.gameObject.SetActive(true);
            rootGroup.alpha = 1f;
            runRoutine = StartCoroutine(Run());
        }

        public void Stop()
        {
            if (runRoutine != null) StopCoroutine(runRoutine);
            runRoutine = null;
            IsPlaying = false;
            finishing = false;
        }

        /// <summary>ストーリーを最後まで飛ばす</summary>
        public void StorySkip()
        {
            if (!IsPlaying || finishing) return;
            if (runRoutine != null) StopCoroutine(runRoutine);
            CloseLog();
            PlayUiSe(skipSe);
            runRoutine = StartCoroutine(FinishRoutine(true));
        }

        public void OpenLog()
        {
            if (logOpen || logEntries.Count == 0 || finishing) return;
            logOpen = true;
            skipTimer = 0f;
            UpdateSkipRing();
            BuildLogEntries();
            logRoot.gameObject.SetActive(true);
            logScroll = 0f;
            PlayUiSe(logOpenSe);
            StartCoroutine(LogOpenRoutine());
        }

        public void CloseLog()
        {
            if (!logOpen) return;
            logOpen = false;
            logRoot.gameObject.SetActive(false);
            Deselect();
            if (IsPlaying && !finishing) PlayUiSe(logCloseSe);
        }

        // =====================================================================
        // 実行
        // =====================================================================

        IEnumerator Run()
        {
            IsPlaying = true;
            onStarted?.Invoke();
            index = 0;

            while (index < script.commands.Count)
            {
                var c = script.commands[index++];
                if (c.type == CommandType.End) break;
                yield return Execute(c);
            }

            yield return FinishRoutine(false);
        }

        IEnumerator FinishRoutine(bool skipped)
        {
            finishing = true;
            skipTimer = 0f;
            UpdateSkipRing();
            choosing = false;
            choicePanel.gameObject.SetActive(false);
            cursor.gameObject.SetActive(false);

            if (audioPlayer)
            {
                if (skipped) audioPlayer.StopAll(skipSoundFadeOut);
                else
                {
                    if (stopBgmOnFinish) audioPlayer.StopBgm(finishBgmFadeOut);
                    if (stopSeOnFinish) audioPlayer.StopAllSe();
                }
            }

            if (skipped)
            {
                float from = fadeOverlay.color.a, t = 0f;
                while (t < skipFadeTime)
                {
                    t += Dt;
                    SetFadeAlpha(Mathf.Lerp(from, 1f, t / skipFadeTime));
                    yield return null;
                }
                SetFadeAlpha(1f);
            }
            else
            {
                yield return HideBox();
            }

            IsPlaying = false;
            if (hideOnFinish)
            {
                yield return FadeGroup(rootGroup, 0f, 0.4f);
                root.gameObject.SetActive(false);
            }

            finishing = false;
            runRoutine = null;
            if (skipped) onSkipped?.Invoke();
            onFinished?.Invoke();
        }

        IEnumerator Execute(Command c)
        {
            switch (c.type)
            {
                case CommandType.Line:
                    yield return DoLine(c);
                    break;
                case CommandType.Background:
                    yield return DoBackground(c.Arg(0, "none"), ParseF(c.Arg(1), backgroundFadeTime));
                    break;
                case CommandType.Show:
                    DoShow(c);
                    break;
                case CommandType.Hide:
                    DoHide(c.Arg(0));
                    break;
                case CommandType.Clear:
                    foreach (var id in new List<string>(stage.Keys)) DoHide(id);
                    break;
                case CommandType.Face:
                    SetFace(c.Arg(0), c.Arg(1));
                    break;
                case CommandType.Jump:
                    if (stage.TryGetValue(c.Arg(0, ""), out var j))
                        StartCoroutine(HopRoutine(j, jumpHeight, 0.28f, Mathf.Max(1, (int)ParseF(c.Arg(1), 1))));
                    break;
                case CommandType.Shake:
                    if (stage.TryGetValue(c.Arg(0, ""), out var s)) StartCoroutine(ShakeRoutine(s));
                    break;
                case CommandType.Emote:
                    DoEmote(c.Arg(0), c.Arg(1, "!"));
                    break;
                case CommandType.Wait:
                    yield return WaitSeconds(ParseF(c.Arg(0), 0.5f));
                    break;
                case CommandType.Fade:
                    yield return DoFade(c.Arg(0, "out"), ParseF(c.Arg(1), defaultScreenFadeTime));
                    break;
                case CommandType.Choice:
                    yield return DoChoice(c);
                    break;
                case CommandType.Goto:
                    GotoLabel(c.Arg(0), c.lineNumber);
                    break;
                case CommandType.Bgm:
                    yield return DoBgm(c);
                    break;
                case CommandType.Se:
                    yield return DoSe(c);
                    break;
            }
        }

        IEnumerator DoLine(Command c)
        {
            CharacterProfile prof = null;
            string id = null;
            if (!string.IsNullOrEmpty(c.speaker))
            {
                id = c.speaker;
                prof = database ? database.FindCharacter(id) : null;
            }
            if (autoShowSpeakers && prof != null && !stage.ContainsKey(id)) AutoShow(id, c.expression);
            else if (!string.IsNullOrEmpty(c.expression)) SetFace(id, c.expression);

            currentSpeakerId = id;
            CurrentTextId = c.textId;
            string displayName = !string.IsNullOrEmpty(c.displayName) ? c.displayName
                               : prof != null ? prof.displayName : c.speaker;
            string affiliation = prof?.affiliation;
            SetName(displayName, affiliation);
            yield return ShowBox();

            // 全文を一度に表示
            bodyText.text = c.text;

            // 話者が小さく跳ねる
            if (speakerHop && id != null && stage.TryGetValue(id, out var sc))
                StartCoroutine(HopRoutine(sc, hopHeight, hopDuration, 1));

            AddLog(prof, id, displayName, affiliation, c);
            onTextShown?.Invoke(c.textId ?? "");

            // 入力待ち
            cursor.gameObject.SetActive(true);
            advanceRequested = false;
            while (!advanceRequested) yield return null;
            advanceRequested = false;
            cursor.gameObject.SetActive(false);
            PlayUiSe(advanceSe);
        }

        void SetName(string displayName, string affiliation)
        {
            bool has = !string.IsNullOrEmpty(displayName);
            nameText.gameObject.SetActive(has);
            separator.gameObject.SetActive(has);
            if (has) nameText.text = FormatName(displayName, affiliation, affiliationSize);
        }

        string FormatName(string displayName, string affiliation, float affSize)
        {
            if (string.IsNullOrEmpty(affiliation)) return $"<b>{displayName}</b>";
            string hex = ColorUtility.ToHtmlStringRGBA(affiliationColor);
            return $"<b>{displayName}</b><space=0.6em><size={affSize}><color=#{hex}>{affiliation}</color></size>";
        }

        IEnumerator DoBackground(string bgName, float time)
        {
            bool black = bgName == "none" || bgName == "black";
            Sprite sp = black || database == null ? null : database.FindBackground(bgName);
            if (!black && sp == null) Debug.LogWarning($"[Conversation] 背景 '{bgName}' が見つかりません");

            var img = CreateImage(bgLayer, "BG_" + bgName, sp);
            Stretch(img.rectTransform);
            img.color = sp ? Color.white : Color.black;
            if (sp)
            {
                var fitter = img.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sp.rect.width / sp.rect.height;
            }

            var cg = img.gameObject.AddComponent<CanvasGroup>();
            yield return FadeGroup(cg, 1f, time, 0f);

            if (currentBg) Destroy(currentBg.gameObject);
            currentBg = img;
        }

        IEnumerator DoFade(string dir, float time)
        {
            bool toDark = dir != "in";
            if (toDark) yield return HideBox();

            float from = fadeOverlay.color.a, to = toDark ? 1f : 0f, t = 0f;
            while (t < time)
            {
                t += Dt;
                SetFadeAlpha(Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            SetFadeAlpha(to);
        }

        void SetFadeAlpha(float a)
        {
            var col = screenFadeColor;
            col.a = a;
            fadeOverlay.color = col;
        }

        IEnumerator DoChoice(Command c)
        {
            choosing = true;
            for (int i = choicePanel.childCount - 1; i >= 0; i--) Destroy(choicePanel.GetChild(i).gameObject);

            ChoiceOption selected = null;
            foreach (var opt in c.options)
            {
                var o = opt;
                var btn = CreateButton(choicePanel, o.text, choiceTextSize, choiceColor, choiceTextColor,
                    () => { selected = o; Deselect(); PlayUiSe(choiceSelectSe); });
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = le.minHeight = choiceHeight;
            }

            choicePanel.gameObject.SetActive(true);
            StartCoroutine(FadeGroup(choicePanel.GetComponent<CanvasGroup>(), 1f, 0.2f, 0f));

            while (selected == null) yield return null;

            choicePanel.gameObject.SetActive(false);
            choosing = false;
            onChoiceSelected?.Invoke(selected.text);
            if (!string.IsNullOrEmpty(selected.label)) GotoLabel(selected.label, c.lineNumber);
        }

        void GotoLabel(string label, int line)
        {
            if (label != null && script.labels.TryGetValue(label, out int to)) index = to;
            else Debug.LogWarning($"[Conversation] {line}行目: ラベル '{label}' が見つかりません");
        }

        IEnumerator WaitSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                if (!logOpen) t += Dt;
                yield return null;
            }
        }

        // =====================================================================
        // BGM・SE
        // =====================================================================

        IEnumerator DoBgm(Command c)
        {
            if (!audioPlayer) yield break;
            var opt = SoundOptions.Parse(c.args);
            string first = opt.Positional(0);
            if (string.IsNullOrEmpty(first)) yield break;

            switch (first.ToLowerInvariant())
            {
                case "stop":
                    {
                        float fade = opt.Float("fade", ParseF(opt.Positional(1), audioPlayer.defaultBgmFadeOut));
                        audioPlayer.StopBgm(fade);
                        if (opt.Has("wait")) yield return WaitSeconds(fade);
                        break;
                    }
                case "volume":
                    {
                        float v = ParseF(opt.Positional(1), opt.Float("vol", 1f));
                        float time = opt.Float("time", opt.Float("fade", 0f));
                        audioPlayer.SetBgmVolume(v, time);
                        if (opt.Has("wait")) yield return WaitSeconds(time);
                        break;
                    }
                case "pause":
                    audioPlayer.PauseBgm();
                    break;
                case "resume":
                    audioPlayer.ResumeBgm();
                    break;
                default:
                    {
                        float fade = opt.Float("fade", ParseF(opt.Positional(1), -1f));
                        audioPlayer.PlayBgm(first, fade, opt.Float("vol", 1f), opt.Float("start", -1f), opt.Float("pitch", 1f));
                        if (opt.Has("wait")) yield return WaitSeconds(fade >= 0f ? fade : audioPlayer.defaultBgmFadeIn);
                        break;
                    }
            }
        }

        IEnumerator DoSe(Command c)
        {
            if (!audioPlayer) yield break;
            var opt = SoundOptions.Parse(c.args);
            string first = opt.Positional(0);
            if (string.IsNullOrEmpty(first)) yield break;

            if (string.Equals(first, "stop", StringComparison.OrdinalIgnoreCase))
            {
                audioPlayer.StopSe(opt.Positional(1), opt.Float("fade", -1f));
                yield break;
            }

            bool? loop = opt.Has("loop") ? opt.Bool("loop", true) : (bool?)null;
            audioPlayer.PlaySe(first, opt.Float("vol", 1f), opt.Float("pitch", 1f),
                opt.Float("delay", 0f), loop, opt.Float("fade", 0f));

            // wait: 鳴り終わるまで次に進まない（ループSEでは無視）
            if (opt.Has("wait") && loop != true)
            {
                yield return null;
                while (audioPlayer.IsSePlaying(first)) yield return null;
            }
        }

        void PlayUiSe(AudioClip clip)
        {
            if (audioPlayer && clip) audioPlayer.PlayUi(clip, uiSeVolume);
        }

        /// <summary>「名前 fade=1 vol=0.5 loop wait」のような引数を読む</summary>
        class SoundOptions
        {
            readonly List<string> positional = new List<string>();
            readonly Dictionary<string, string> named =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public static SoundOptions Parse(string[] args)
            {
                var o = new SoundOptions();
                foreach (var a in args)
                {
                    int eq = a.IndexOf('=');
                    if (eq > 0)
                    {
                        string key = a.Substring(0, eq).Trim().ToLowerInvariant();
                        if (key == "volume") key = "vol";
                        o.named[key] = a.Substring(eq + 1).Trim();
                    }
                    else if (string.Equals(a, "loop", StringComparison.OrdinalIgnoreCase)) o.named["loop"] = "true";
                    else if (string.Equals(a, "noloop", StringComparison.OrdinalIgnoreCase)) o.named["loop"] = "false";
                    else if (string.Equals(a, "wait", StringComparison.OrdinalIgnoreCase)) o.named["wait"] = "true";
                    else o.positional.Add(a);
                }
                return o;
            }

            public string Positional(int i) => i < positional.Count ? positional[i] : null;
            public bool Has(string key) => named.ContainsKey(key);

            public float Float(string key, float def) =>
                named.TryGetValue(key, out var v) ? ParseF(v, def) : def;

            public bool Bool(string key, bool def)
            {
                if (!named.TryGetValue(key, out var v)) return def;
                v = v.ToLowerInvariant();
                return v == "true" || v == "1" || v == "on" || v == "yes";
            }
        }

        // =====================================================================
        // ストーリースキップ
        // =====================================================================

        void UpdateStorySkip()
        {
            bool held = allowStorySkip &&
                        (SkipHeld() || (skipHoldButton != null && skipHoldButton.IsHeld));

            if (held)
            {
                skipTimer += Dt;
                if (skipTimer >= skipHoldSeconds)
                {
                    StorySkip();
                    return;
                }
            }
            else
            {
                skipTimer = 0f;
            }
            UpdateSkipRing();
        }

        void UpdateSkipRing()
        {
            if (!skipRing) return;
            bool show = skipTimer > 0f;
            skipRing.enabled = show;
            skipRingBack.enabled = show;
            skipRing.fillAmount = skipHoldSeconds > 0f ? Mathf.Clamp01(skipTimer / skipHoldSeconds) : 1f;
        }

        // =====================================================================
        // ログ
        // =====================================================================

        void AddLog(CharacterProfile prof, string id, string displayName, string affiliation, Command c)
        {
            var entry = new LogEntry { name = displayName, affiliation = affiliation, text = c.text };

            if (prof != null)
            {
                string expr = c.expression;
                if (string.IsNullOrEmpty(expr) && id != null && stage.TryGetValue(id, out var sc)) expr = sc.expression;
                var ce = prof.FindExpression(expr);
                if (ce != null)
                {
                    entry.dedicatedIcon = ce.logIcon != null;
                    entry.icon = ce.logIcon != null ? ce.logIcon : ce.sprite;
                }
                entry.faceCenter = prof.faceCenter;
                entry.zoom = prof.logIconZoom;
            }

            logEntries.Add(entry);
            if (logEntries.Count > maxLogEntries) logEntries.RemoveAt(0);
        }

        void BuildLogEntries()
        {
            for (int i = logContent.childCount - 1; i >= 0; i--) Destroy(logContent.GetChild(i).gameObject);

            // 一番新しいログを中央 (y=0) に置き、古いものほど上に積む
            float y = 0f, prevH = 0f;
            for (int i = logEntries.Count - 1; i >= 0; i--)
            {
                bool isCurrent = i == logEntries.Count - 1;
                var rt = CreateLogEntry(logEntries[i], isCurrent, out float h);
                if (!isCurrent) y += prevH * 0.5f + logEntrySpacing + h * 0.5f;
                rt.anchoredPosition = new Vector2(0f, y);
                prevH = h;
            }
            logMaxScroll = y;
        }

        RectTransform CreateLogEntry(LogEntry e, bool isCurrent, out float height)
        {
            var entry = NewRect("LogEntry", logContent);
            entry.anchorMin = entry.anchorMax = new Vector2(0.5f, 0.5f);
            entry.pivot = new Vector2(0.5f, 0.5f);

            float winLeft = logIconSize + 24f;
            float textW = logEntryWidth - winLeft - 80f;
            bool hasName = !string.IsNullOrEmpty(e.name);

            // テキスト（高さを先に測る）
            var body = CreateText(entry, "Text", logTextSize, bodyColor, TextAlignmentOptions.TopLeft);
            body.lineSpacing = bodyLineSpacing;
            body.text = e.text;
            float textH = body.GetPreferredValues(e.text, textW, 0f).y;

            float winH = Mathf.Max(logIconSize, textH + 92f);
            height = winH + (hasName ? 24f : 0f);
            entry.sizeDelta = new Vector2(logEntryWidth, height);

            // ウィンドウ
            var win = CreateImage(entry, "Window", logWindowSprite ? logWindowSprite : roundSprite);
            win.type = Image.Type.Sliced;
            win.color = isCurrent ? logCurrentWindowColor : logWindowColor;
            var wrt = win.rectTransform;
            wrt.anchorMin = new Vector2(0f, 0f);
            wrt.anchorMax = new Vector2(1f, 0f);
            wrt.pivot = new Vector2(0.5f, 0f);
            wrt.offsetMin = new Vector2(winLeft, 0f);
            wrt.offsetMax = new Vector2(0f, winH);
            win.transform.SetAsFirstSibling();

            var brt = body.rectTransform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = new Vector2(winLeft + 40f, 32f);
            brt.offsetMax = new Vector2(-40f, winH - 52f);

            // 名前タブ
            if (hasName)
            {
                var tab = CreateImage(entry, "NameTab", roundSprite);
                tab.type = Image.Type.Sliced;
                tab.color = logNameTabColor;

                var nameT = CreateText(tab.transform, "Name", logNameSize, nameColor, TextAlignmentOptions.MidlineLeft);
                nameT.textWrappingMode = TextWrappingModes.NoWrap;
                nameT.text = FormatName(e.name, e.affiliation, logNameSize * 0.75f);
                // 折り返さない状態での幅を測る
                float nameW = nameT.GetPreferredValues(nameT.text, float.PositiveInfinity, float.PositiveInfinity).x;

                const float tabPad = 24f;
                var trt = tab.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0f, 0f);
                trt.pivot = new Vector2(0f, 0.5f);
                trt.anchoredPosition = new Vector2(winLeft + 30f, winH);
                trt.sizeDelta = new Vector2(nameW + tabPad * 2f, 48f);

                var nrt = nameT.rectTransform;
                Stretch(nrt);
                nrt.offsetMin = new Vector2(tabPad, 0f);
                nrt.offsetMax = new Vector2(-tabPad, 0f);
            }

            // キャラの顔
            if (e.icon != null)
            {
                var box = NewRect("Icon", entry);
                box.anchorMin = box.anchorMax = new Vector2(0f, 0f);
                box.pivot = new Vector2(0f, 0f);
                box.anchoredPosition = new Vector2(0f, (winH - logIconSize) * 0.5f);
                box.sizeDelta = new Vector2(logIconSize, logIconSize);
                box.gameObject.AddComponent<RectMask2D>();

                var back = CreateImage(box, "Back", roundSprite);
                back.type = Image.Type.Sliced;
                back.color = logIconBackColor;
                Stretch(back.rectTransform);

                var face = CreateImage(box, "Face", e.icon);
                var frt = face.rectTransform;
                if (e.dedicatedIcon)
                {
                    Stretch(frt);
                    face.preserveAspect = true;
                }
                else
                {
                    // 立ち絵の顔部分を中央に合わせて拡大
                    float hh = logIconSize * e.zoom;
                    float ww = hh * e.icon.rect.width / e.icon.rect.height;
                    frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
                    frt.pivot = e.faceCenter;
                    frt.anchoredPosition = Vector2.zero;
                    frt.sizeDelta = new Vector2(ww, hh);
                }
            }

            return entry;
        }

        void UpdateLog()
        {
            if (LogPressed() || LogClosePressed())
            {
                CloseLog();
                return;
            }

            logScroll += ReadLogScrollAxis() * logScrollSpeed * Dt;
            logScroll += ReadWheelNotches() * logWheelStep;
            logScroll = Mathf.Clamp(logScroll, 0f, logMaxScroll);
            logContent.anchoredPosition = new Vector2(0f, -logScroll + logOpenOffset);
        }

        IEnumerator LogOpenRoutine()
        {
            const float d = 0.25f;
            float t = 0f;
            while (t < d && logOpen)
            {
                t += Dt;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / d), 3f);
                logGroup.alpha = k;
                logOpenOffset = -160f * (1f - k); // 下から上へ流れて出てくる
                logContent.anchoredPosition = new Vector2(0f, -logScroll + logOpenOffset);
                yield return null;
            }
            logGroup.alpha = 1f;
            logOpenOffset = 0f;
        }

        // =====================================================================
        // キャラクター
        // =====================================================================

        void DoShow(Command c)
        {
            string id = c.Arg(0);
            if (string.IsNullOrEmpty(id)) return;

            string expr = null;
            float? x = null;
            for (int i = 1; i < c.args.Length; i++)
            {
                if (TryParsePosition(c.args[i], out float px)) x = px;
                else expr = c.args[i];
            }

            if (stage.TryGetValue(id, out var existing))
            {
                if (expr != null) SetFace(id, expr);
                if (x.HasValue) MoveTo(existing, x.Value);
                return;
            }

            var prof = database ? database.FindCharacter(id) : null;
            if (prof == null) { Debug.LogWarning($"[Conversation] {c.lineNumber}行目: キャラ '{id}' がDatabaseにいません"); return; }

            var sc = CreateStageCharacter(prof, x ?? centerX);
            stage[id] = sc;
            SetExpression(sc, expr);
            var col = sc.dimOverlay.color;
            col.a = TargetDim(sc);
            sc.dimOverlay.color = col;
            StartCoroutine(AppearRoutine(sc));
        }

        /// <summary>話者を空いている位置に自動で登場させる</summary>
        void AutoShow(string id, string expr)
        {
            var others = new List<StageCharacter>(stage.Values);
            float x;
            if (others.Count == 0)
            {
                x = centerX;
            }
            else if (others.Count == 1)
            {
                var o = others[0];
                float ox = o.holder.anchorMin.x;
                if (Mathf.Abs(ox - centerX) < 0.05f) { MoveTo(o, leftX); x = rightX; }
                else x = ox < centerX ? rightX : leftX;
            }
            else
            {
                // 既存キャラから一番離れた位置
                x = centerX;
                float best = -1f;
                foreach (var cand in new[] { leftX, centerX, rightX })
                {
                    float min = float.MaxValue;
                    foreach (var o in others) min = Mathf.Min(min, Mathf.Abs(o.holder.anchorMin.x - cand));
                    if (min > best) { best = min; x = cand; }
                }
            }

            string pos = x.ToString(CultureInfo.InvariantCulture);
            var args = string.IsNullOrEmpty(expr) ? new[] { id, pos } : new[] { id, expr, pos };
            DoShow(new Command { type = CommandType.Show, args = args });
        }

        void DoHide(string id)
        {
            if (string.IsNullOrEmpty(id) || !stage.TryGetValue(id, out var sc)) return;
            stage.Remove(id);
            StartCoroutine(DisappearRoutine(sc));
        }

        void SetFace(string id, string expr)
        {
            if (string.IsNullOrEmpty(id) || !stage.TryGetValue(id, out var sc)) return;
            SetExpression(sc, expr);
        }

        void SetExpression(StageCharacter sc, string expr)
        {
            var ce = sc.profile.FindExpression(expr);
            sc.expression = ce?.name;
            var sp = ce?.sprite;

            sc.image.sprite = sp;
            sc.dimOverlay.sprite = sp;
            sc.image.enabled = sc.dimOverlay.enabled = sp != null;
            if (sp == null) return;

            float h = characterHeight * sc.profile.scale;
            sc.image.rectTransform.sizeDelta = new Vector2(h * sp.rect.width / sp.rect.height, h);
        }

        StageCharacter CreateStageCharacter(CharacterProfile prof, float x)
        {
            var holder = NewRect("Char_" + prof.id, charLayer);
            holder.anchorMin = holder.anchorMax = new Vector2(x, 0f);
            holder.pivot = new Vector2(0.5f, 0f);
            holder.sizeDelta = Vector2.zero;
            holder.anchoredPosition = prof.offset;

            var group = holder.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var motion = NewRect("Motion", holder);
            motion.anchorMin = motion.anchorMax = new Vector2(0.5f, 0f);
            motion.pivot = new Vector2(0.5f, 0f);
            motion.sizeDelta = Vector2.zero;

            var img = CreateImage(motion, "Sprite", null);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);

            // 立ち絵と同じ形の黒レイヤー（話者以外を暗くする）
            var overlay = CreateImage(img.transform, "DimLayer", null);
            Stretch(overlay.rectTransform);
            overlay.color = new Color(0f, 0f, 0f, 0f);

            return new StageCharacter
            {
                id = prof.id,
                profile = prof,
                holder = holder,
                motion = motion,
                image = img,
                dimOverlay = overlay,
                group = group
            };
        }

        float TargetDim(StageCharacter sc)
        {
            if (!dimNonSpeakers) return 0f;
            if (currentSpeakerId == null) return dimAllOnNarration ? dimAlpha : 0f;
            return string.Equals(sc.id, currentSpeakerId, StringComparison.OrdinalIgnoreCase) ? 0f : dimAlpha;
        }

        void UpdateCharacterDim()
        {
            float k = 1f - Mathf.Exp(-14f * Dt);
            foreach (var sc in stage.Values)
            {
                if (!sc.dimOverlay) continue;
                var col = sc.dimOverlay.color;
                col.a = Mathf.Lerp(col.a, TargetDim(sc), k);
                sc.dimOverlay.color = col;
            }
        }

        void MoveTo(StageCharacter sc, float x)
        {
            if (sc.moveCo != null) StopCoroutine(sc.moveCo);
            sc.moveCo = StartCoroutine(MoveRoutine(sc, x));
        }

        IEnumerator MoveRoutine(StageCharacter sc, float x)
        {
            float from = sc.holder.anchorMin.x, t = 0f;
            while (t < characterMoveTime)
            {
                if (!sc.holder) yield break;
                t += Dt;
                float v = Mathf.Lerp(from, x, Mathf.SmoothStep(0f, 1f, t / characterMoveTime));
                sc.holder.anchorMin = sc.holder.anchorMax = new Vector2(v, 0f);
                yield return null;
            }
            if (sc.holder) sc.holder.anchorMin = sc.holder.anchorMax = new Vector2(x, 0f);
        }

        IEnumerator AppearRoutine(StageCharacter sc)
        {
            float t = 0f;
            var rt = sc.image.rectTransform;
            while (t < characterFadeTime)
            {
                if (!sc.holder) yield break;
                t += Dt;
                float k = Mathf.Clamp01(t / characterFadeTime);
                sc.group.alpha = k;
                rt.anchoredPosition = new Vector2(0f, -appearRise * (1f - k) * (1f - k));
                yield return null;
            }
            if (!sc.holder) yield break;
            sc.group.alpha = 1f;
            rt.anchoredPosition = Vector2.zero;
        }

        IEnumerator DisappearRoutine(StageCharacter sc)
        {
            float from = sc.group ? sc.group.alpha : 0f, t = 0f;
            while (t < characterFadeTime)
            {
                if (!sc.holder) yield break;
                t += Dt;
                sc.group.alpha = Mathf.Lerp(from, 0f, t / characterFadeTime);
                yield return null;
            }
            if (sc.holder) Destroy(sc.holder.gameObject);
        }

        IEnumerator HopRoutine(StageCharacter sc, float height, float duration, int count)
        {
            for (int n = 0; n < count; n++)
            {
                float t = 0f;
                while (t < duration)
                {
                    if (!sc.motion) yield break;
                    t += Dt;
                    sc.motion.anchoredPosition = new Vector2(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration)) * height);
                    yield return null;
                }
            }
            if (sc.motion) sc.motion.anchoredPosition = Vector2.zero;
        }

        IEnumerator ShakeRoutine(StageCharacter sc)
        {
            const float d = 0.4f;
            float t = 0f;
            while (t < d)
            {
                if (!sc.motion) yield break;
                t += Dt;
                sc.motion.anchoredPosition = new Vector2(Mathf.Sin(t * 60f) * shakeStrength * (1f - t / d), 0f);
                yield return null;
            }
            if (sc.motion) sc.motion.anchoredPosition = Vector2.zero;
        }

        void DoEmote(string id, string symbol)
        {
            if (string.IsNullOrEmpty(id) || !stage.TryGetValue(id, out var sc)) return;

            var size = sc.image.rectTransform.sizeDelta;
            var txt = CreateText(sc.motion, "Emote", emoteSize, emoteColor, TextAlignmentOptions.Center);
            txt.text = $"<b>{symbol}</b>";
            txt.outlineWidth = 0.2f;
            txt.outlineColor = Color.white;

            var rt = txt.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(240f, 160f);
            rt.anchoredPosition = new Vector2(size.x * (sc.profile.emoteAnchor.x - 0.5f), size.y * sc.profile.emoteAnchor.y);
            StartCoroutine(EmoteRoutine(rt, txt));
        }

        IEnumerator EmoteRoutine(RectTransform rt, TextMeshProUGUI txt)
        {
            float t = 0f;
            while (t < 0.2f)
            {
                if (!rt) yield break;
                t += Dt;
                float k = t / 0.2f;
                rt.localScale = Vector3.one * (k < 0.6f ? Mathf.Lerp(0f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f));
                yield return null;
            }
            t = 0f;
            while (t < 0.9f) { t += Dt; yield return null; }
            t = 0f;
            while (t < 0.2f)
            {
                if (!rt) yield break;
                t += Dt;
                txt.alpha = 1f - t / 0.2f;
                yield return null;
            }
            if (rt) Destroy(rt.gameObject);
        }

        // =====================================================================
        // テキストウィンドウ・カーソル
        // =====================================================================

        IEnumerator ShowBox()
        {
            if (boxVisible) yield break;
            boxVisible = true;
            yield return FadeGroup(boxGroup, 1f, 0.2f);
        }

        IEnumerator HideBox()
        {
            if (!boxVisible) yield break;
            boxVisible = false;
            cursor.gameObject.SetActive(false);
            yield return FadeGroup(boxGroup, 0f, 0.2f);
        }

        void UpdateCursor()
        {
            if (!cursor || !cursor.gameObject.activeSelf) return;
            float y = cursorOffset.y + Mathf.Sin(Time.unscaledTime * cursorBobSpeed * Mathf.PI * 2f) * cursorBobHeight;
            cursor.anchoredPosition = new Vector2(-cursorOffset.x, y);
        }

        static IEnumerator FadeGroup(CanvasGroup g, float to, float time, float? from = null)
        {
            float start = from ?? g.alpha, t = 0f;
            g.alpha = start;
            while (t < time)
            {
                if (!g) yield break;
                t += Dt;
                g.alpha = Mathf.Lerp(start, to, t / time);
                yield return null;
            }
            if (g) g.alpha = to;
        }

        void ResetStage()
        {
            foreach (var sc in stage.Values) if (sc.holder) Destroy(sc.holder.gameObject);
            stage.Clear();
            logEntries.Clear();
            CloseLog();
            if (currentBg) Destroy(currentBg.gameObject);
            currentBg = null;
            currentSpeakerId = null;
            CurrentTextId = null;
            boxVisible = false;
            boxGroup.alpha = 0f;
            choosing = false;
            choicePanel.gameObject.SetActive(false);
            cursor.gameObject.SetActive(false);
            skipTimer = 0f;
            UpdateSkipRing();
            SetFadeAlpha(0f);
        }

        // =====================================================================
        // UI構築
        // =====================================================================

        void EnsureUI()
        {
            if (root) return;
            EnsureEventSystem();
            roundSprite = MakeRoundedSprite(64, 14);
            gradientSprite = MakeGradientSprite(boxGradient);
            triangleSprite = MakeTriangleSprite(32, 24);
            circleSprite = MakeRingSprite(128, 64f);
            ringSprite = MakeRingSprite(128, 10f);
            BuildUI();
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("ConversationCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            rootGroup = canvasGo.AddComponent<CanvasGroup>();
            root = (RectTransform)canvasGo.transform;

            // ---- 背景 ----
            bgLayer = NewRect("Background", root);
            Stretch(bgLayer);
            var baseBg = CreateImage(bgLayer, "Base", null);
            Stretch(baseBg.rectTransform);
            baseBg.color = Color.black;

            // ---- キャラ ----
            charLayer = NewRect("Characters", root);
            Stretch(charLayer);

            // ---- テキストウィンドウ ----
            boxRoot = NewRect("TextWindow", root);
            boxRoot.anchorMin = new Vector2(0f, 0f);
            boxRoot.anchorMax = new Vector2(1f, 0f);
            boxRoot.pivot = new Vector2(0.5f, 0f);
            boxRoot.sizeDelta = new Vector2(0f, boxHeight);
            boxRoot.anchoredPosition = Vector2.zero;
            boxGroup = boxRoot.gameObject.AddComponent<CanvasGroup>();
            boxGroup.blocksRaycasts = false;
            boxGroup.alpha = 0f;

            var win = CreateImage(boxRoot, "Window", windowSprite ? windowSprite : gradientSprite);
            if (windowSprite) { win.type = Image.Type.Sliced; win.color = windowColor; }
            Stretch(win.rectTransform);

            float nameH = nameSize * 1.6f;
            nameText = CreateText(boxRoot, "Name", nameSize, nameColor, TextAlignmentOptions.BottomLeft);
            SetTopStretch(nameText.rectTransform, nameAreaTop, nameH, sidePadding, sidePadding);

            float sepY = nameAreaTop + nameH + 8f;
            separator = CreateImage(boxRoot, "Separator", null);
            separator.color = separatorColor;
            SetTopStretch(separator.rectTransform, sepY, 2f, sidePadding - 10f, sidePadding - 10f);

            bodyText = CreateText(boxRoot, "Text", bodySize, bodyColor, TextAlignmentOptions.TopLeft);
            bodyText.lineSpacing = bodyLineSpacing;
            var brt = bodyText.rectTransform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(sidePadding, 90f);
            brt.offsetMax = new Vector2(-sidePadding, -(sepY + 22f));

            // ---- カーソルUI ----
            var cur = CreateImage(boxRoot, "Cursor", cursorSprite ? cursorSprite : triangleSprite);
            cur.color = cursorColor;
            cur.preserveAspect = true;
            cursor = cur.rectTransform;
            cursor.anchorMin = cursor.anchorMax = new Vector2(1f, 0f);
            cursor.pivot = new Vector2(0.5f, 0f);
            cursor.sizeDelta = cursorSize;
            cursor.anchoredPosition = new Vector2(-cursorOffset.x, cursorOffset.y);
            cursor.gameObject.SetActive(false);

            // ---- 画面クリックで文字送り ----
            var catcher = CreateImage(root, "ClickCatcher", null);
            Stretch(catcher.rectTransform);
            catcher.color = Color.clear;
            catcher.raycastTarget = true;
            var cb = catcher.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None;
            cb.navigation = new Navigation { mode = Navigation.Mode.None };
            cb.onClick.AddListener(() =>
            {
                Deselect();
                if (!choosing && !logOpen && !finishing) advanceRequested = true;
            });

            // ---- UIバー（ログ・スキップ） ----
            BuildUiBar();

            // ---- 選択肢 ----
            choicePanel = NewRect("Choices", root);
            choicePanel.anchorMin = choicePanel.anchorMax = new Vector2(0.5f, 0.5f);
            choicePanel.sizeDelta = new Vector2(choiceWidth, 700f);
            choicePanel.anchoredPosition = new Vector2(0f, 80f);
            var v = choicePanel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 24f;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            choicePanel.gameObject.AddComponent<CanvasGroup>();
            choicePanel.gameObject.SetActive(false);

            // ---- ログ ----
            BuildLogUI();

            // ---- 暗転 ----
            fadeOverlay = CreateImage(root, "Fade", null);
            Stretch(fadeOverlay.rectTransform);
            SetFadeAlpha(0f);
        }

        void BuildUiBar()
        {
            var bar = CreateImage(root, "UIBar", roundSprite);
            bar.type = Image.Type.Sliced;
            bar.color = uiBarColor;
            var rt = bar.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-uiBarMargin.x, uiBarMargin.y);
            rt.sizeDelta = new Vector2(uiItemWidth * 2f + 16f, uiBarHeight);

            // ログ
            var logItem = CreateBarItem(rt, 0, logKeyLabel, logButtonIcon, logLabel, out _);
            var logBtn = logItem.gameObject.AddComponent<Button>();
            logBtn.transition = Selectable.Transition.None;
            logBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            logBtn.onClick.AddListener(() => { Deselect(); if (logOpen) CloseLog(); else OpenLog(); });

            // スキップ（長押し）
            var skipItem = CreateBarItem(rt, 1, skipKeyLabel, skipButtonIcon, skipLabel, out var badge);
            skipHoldButton = skipItem.gameObject.AddComponent<HoldButton>();

            // 長押しサークル（UIからはみ出ないサイズ）
            float ringSize = uiBarHeight - 10f;
            skipRingBack = CreateImage(skipItem, "SkipRingBack", ringSprite);
            skipRingBack.color = skipRingBackColor;
            skipRing = CreateImage(skipItem, "SkipRing", ringSprite);
            skipRing.color = skipRingColor;
            skipRing.type = Image.Type.Filled;
            skipRing.fillMethod = Image.FillMethod.Radial360;
            skipRing.fillOrigin = (int)Image.Origin360.Top;
            skipRing.fillClockwise = true;
            foreach (var r in new[] { skipRingBack.rectTransform, skipRing.rectTransform })
            {
                r.anchorMin = r.anchorMax = badge.anchorMin;
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = badge.anchoredPosition;
                r.sizeDelta = new Vector2(ringSize, ringSize);
            }
            UpdateSkipRing();

            bar.gameObject.SetActive(showUiBar);
        }

        RectTransform CreateBarItem(RectTransform bar, int i, string keyLabel, Sprite icon, string label, out RectTransform badge)
        {
            var hit = CreateImage(bar, "Item_" + label, null);
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var rt = hit.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(8f + i * uiItemWidth, 0f);
            rt.sizeDelta = new Vector2(uiItemWidth, uiBarHeight - 8f);

            float bs = uiBarHeight - 22f;
            var b = CreateImage(rt, "Badge", icon ? icon : circleSprite);
            b.color = icon ? Color.white : uiBadgeColor;
            b.preserveAspect = true;
            badge = b.rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = new Vector2(uiBarHeight * 0.5f - 4f, 0f);
            badge.sizeDelta = new Vector2(bs, bs);

            if (!icon)
            {
                var k = CreateText(badge, "Key", bs * 0.4f, uiBadgeTextColor, TextAlignmentOptions.Center);
                k.text = $"<b>{keyLabel}</b>";
                k.enableAutoSizing = true;
                k.fontSizeMin = 8f;
                k.fontSizeMax = bs * 0.42f;
                Stretch(k.rectTransform);
            }

            var l = CreateText(rt, "Label", uiTextSize, uiTextColor, TextAlignmentOptions.MidlineLeft);
            l.text = label;
            var lrt = l.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(uiBarHeight + 2f, 0f);
            lrt.offsetMax = new Vector2(-6f, 0f);
            return rt;
        }

        void BuildLogUI()
        {
            logRoot = NewRect("Log", root);
            Stretch(logRoot);
            logGroup = logRoot.gameObject.AddComponent<CanvasGroup>();

            // ログ以外のすべてに乗せる黒の半透明レイヤー（クリックで閉じる）
            var dim = CreateImage(logRoot, "DimLayer", null);
            Stretch(dim.rectTransform);
            dim.color = new Color(0f, 0f, 0f, logDimAlpha);
            dim.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            dimBtn.onClick.AddListener(CloseLog);

            var viewport = NewRect("Viewport", logRoot);
            Stretch(viewport);
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, 80);

            logContent = NewRect("Content", viewport);
            logContent.anchorMin = logContent.anchorMax = new Vector2(0.5f, 0.5f);
            logContent.pivot = new Vector2(0.5f, 0.5f);
            logContent.sizeDelta = Vector2.zero;

            logRoot.gameObject.SetActive(false);
        }

        Button CreateButton(Transform parent, string label, float size, Color bg, Color fg, UnityAction onClick)
        {
            var img = CreateImage(parent, "Button_" + label, roundSprite);
            img.type = Image.Type.Sliced;
            img.color = bg;
            img.raycastTarget = true;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.88f, 0.94f, 1f);
            colors.pressedColor = new Color(0.7f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var text = CreateText(img.transform, "Label", size, fg, TextAlignmentOptions.Center);
            text.text = $"<b>{label}</b>";
            Stretch(text.rectTransform);
            return btn;
        }

        TextMeshProUGUI CreateText(Transform parent, string objName, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(objName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        static Image CreateImage(Transform parent, string objName, Sprite sprite)
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

        static void SetTopStretch(RectTransform rt, float top, float height, float left, float right)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -(top + height));
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void Deselect()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        // =====================================================================
        // 入力
        // =====================================================================

        void SetDefaultBindings(bool force)
        {
#if ENABLE_INPUT_SYSTEM
            if (force || advanceAction == null || advanceAction.bindings.Count == 0)
            {
                advanceAction = new InputAction("Advance", InputActionType.Button);
                advanceAction.AddBinding("<Gamepad>/buttonSouth");
                advanceAction.AddBinding("<Keyboard>/space");
                advanceAction.AddBinding("<Keyboard>/enter");
            }
            if (force || storySkipAction == null || storySkipAction.bindings.Count == 0)
            {
                storySkipAction = new InputAction("StorySkip", InputActionType.Button);
                storySkipAction.AddBinding("<Gamepad>/start");      // PS: OPTIONS / Xbox: Menu
                storySkipAction.AddBinding("<Keyboard>/escape");
            }
            if (force || logAction == null || logAction.bindings.Count == 0)
            {
                logAction = new InputAction("Log", InputActionType.Button);
                logAction.AddBinding("<Gamepad>/leftTrigger");      // PS: L2 / Xbox: LT
                logAction.AddBinding("<Keyboard>/l");
            }
            if (force || logCloseAction == null || logCloseAction.bindings.Count == 0)
            {
                logCloseAction = new InputAction("LogClose", InputActionType.Button);
                logCloseAction.AddBinding("<Gamepad>/buttonEast");
                logCloseAction.AddBinding("<Keyboard>/backspace");
                logCloseAction.AddBinding("<Mouse>/rightButton");
            }
            if (force || logScrollAction == null || logScrollAction.bindings.Count == 0)
            {
                logScrollAction = new InputAction("LogScroll", InputActionType.Value, expectedControlType: "Axis");
                logScrollAction.AddBinding("<Gamepad>/leftStick/y");
                logScrollAction.AddBinding("<Gamepad>/dpad/y");
                logScrollAction.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/downArrow")
                    .With("Positive", "<Keyboard>/upArrow");
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        bool AdvancePressed() => advanceAction.WasPressedThisFrame();
        bool SkipHeld() => storySkipAction.IsPressed();
        bool LogPressed() => logAction.WasPressedThisFrame();
        bool LogClosePressed() => logCloseAction.WasPressedThisFrame();
        float ReadLogScrollAxis() => logScrollAction.ReadValue<float>();
        static float ReadWheelNotches() => Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        bool AdvancePressed() => Input.GetKeyDown(advanceKey) || Input.GetKeyDown(advanceKey2);
        bool SkipHeld() => Input.GetKey(storySkipKey);
        bool LogPressed() => Input.GetKeyDown(logKey);
        bool LogClosePressed() => Input.GetKeyDown(logCloseKey) || Input.GetMouseButtonDown(1);
        static float ReadLogScrollAxis() =>
            (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        static float ReadWheelNotches() => Input.mouseScrollDelta.y;
#endif

        // =====================================================================
        // ユーティリティ
        // =====================================================================

        bool TryParsePosition(string s, out float x)
        {
            switch (s.ToLowerInvariant())
            {
                case "left": case "l": x = leftX; return true;
                case "center": case "c": case "middle": x = centerX; return true;
                case "right": case "r": x = rightX; return true;
            }
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out x) && x >= 0f && x <= 1f;
        }

        static float ParseF(string s, float def)
        {
            return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        static Gradient DefaultBoxGradient()
        {
            var g = new Gradient();
            var c = new Color(0.04f, 0.08f, 0.16f);
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0.88f, 0f), new GradientAlphaKey(0.75f, 0.55f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Sprite MakeGradientSprite(Gradient g)
        {
            const int h = 128;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                var col = g.Evaluate(y / (float)(h - 1));
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, col);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        /// <summary>リング（thickness が半径以上なら塗りつぶしの円）</summary>
        static Sprite MakeRingSprite(int size, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float outer = r - 1f - d;
                    float inner = d - (r - 1f - thickness);
                    float a = Mathf.Clamp01(Mathf.Min(outer, inner) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeTriangleSprite(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float cx = w / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float half = (y + 0.5f) / h * cx; // 下が尖った▼
                    float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - cx) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
    }
}