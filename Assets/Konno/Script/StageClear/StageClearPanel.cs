using System;
using System.Collections;
using System.Collections.Generic;
using Conversation;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ステージクリア画面（パネル）。
///   ・画面を暗くする
///   ・上に横長の帯 ＋ 金色の線 ＋ 後ろで回る魔法陣 ＋「STAGE CLEAR」の文字 ＋ キラキラ
///   ・左下にキャラクター（MVP など）
///   ・右下に項目（MVP / TURN など。好きなだけ増やせる）
///   ・出きったら右下に「閉じる」ボタン（会話パートの ログ／スキップ と同じ見た目）。押すとパネルを閉じる
///   ・ボタン（または決定ボタン）でフェードして閉じて、次のシーンへ
/// 画像や部品は自動で作るので、空の GameObject に付けるだけで動く。
/// 魔法陣・キラキラなどは、自分の画像に差し替えることもできる。
/// </summary>
public class StageClearPanel : MonoBehaviour
{
    public enum CloseMode
    {
        WaitForInput,   // 決定ボタン（Enter / Space / Z / パッドの A / クリック）で閉じる
        AutoClose,      // 決まった秒数で自動で閉じる
        Manual          // Close() を呼んだ時だけ閉じる
    }

    public enum ConversationTiming
    {
        AfterClose,   // パネルが消えきってから会話
        Immediately   // 押した瞬間に会話（パネルは後ろでフェードして消える）
    }

    [Serializable]
    public class StatRow
    {
        [Tooltip("項目名（MVP、TURN など）")]
        public string label = "MVP";
        [Tooltip("値（コードから SetValue で変えられる）")]
        public string value = "";
    }

    // =========================================================================
    // 設定
    // =========================================================================

    [Header("再生")]
    [Tooltip("ゲーム開始時に自動で再生する")]
    public bool playOnStart = false;
    [Tooltip("閉じ方。WaitForInput: 決定ボタンで閉じる / AutoClose: 自動 / Manual: Close() を呼ぶ")]
    public CloseMode closeMode = CloseMode.WaitForInput;
    [Tooltip("AutoClose の時、出きってから閉じるまでの時間（秒）")]
    public float autoCloseTime = 3f;
    [Tooltip("出てくる途中で決定ボタンを押したら、最後まで飛ばす")]
    public bool skipIntroWithInput = true;
    [Tooltip("再生中はゲームを止める（Time.timeScale = 0）")]
    public bool pauseGame = false;

    [Header("表示する場所")]
    [Tooltip("この中に作る（自分で用意したパネル）。空なら専用の Canvas を作って一番手前に出す")]
    public RectTransform buildInto;
    [Tooltip("閉じ終わったら Build Into のオブジェクトを非表示にする")]
    public bool hidePanelOnClose = true;
    [Tooltip("専用の Canvas を作る時の、手前に出す順番")]
    public int sortingOrder = 220;

    [Header("フォント")]
    [Tooltip("「STAGE CLEAR」のフォント（セリフ体がおすすめ）。空なら TMP の標準")]
    public TMP_FontAsset titleFont;
    [Tooltip("項目のフォント（日本語の TMP フォントを入れる）。空なら TMP の標準")]
    public TMP_FontAsset textFont;

    [Header("背景")]
    [Tooltip("画面を暗くする色（アルファが濃さ）")]
    public Color dimColor = new Color(0.12f, 0.08f, 0.03f, 0.55f);
    [Tooltip("キャラの後ろの、もやっとした煙")]
    public bool smoke = true;
    public Color smokeColor = new Color(0.25f, 0.2f, 0.08f, 0.7f);

    [Header("タイトル（STAGE CLEAR）")]
    public string title = "STAGE CLEAR";
    [Tooltip("タイトルの位置（画面の中心から）")]
    public Vector2 titlePosition = new Vector2(0f, 250f);
    public float titleSize = 120f;
    public Color titleColor = new Color(1f, 0.94f, 0.80f, 1f);
    public bool titleItalic = true;
    public bool titleBold = true;
    [Tooltip("文字の周りを光らせる")]
    public bool titleGlow = true;
    public Color titleGlowColor = new Color(1f, 0.75f, 0.25f, 0.8f);
    [Tooltip("文字が出た瞬間に、光る文字が広がって消える")]
    public bool titleFlash = true;

    [Header("帯")]
    public float bandHeight = 190f;
    public Color bandColor = new Color(0.08f, 0.06f, 0.03f, 0.55f);
    [Tooltip("帯の上下の金色の線")]
    public Color lineColor = new Color(1f, 0.85f, 0.55f, 0.95f);
    public float lineThickness = 4f;
    [Tooltip("線の長さ (px)")]
    public float lineLength = 1500f;
    [Tooltip("線の上の飾り（ひし形）")]
    public bool lineOrnaments = true;

    [Header("魔法陣（タイトルの後ろ）")]
    public bool emblem = true;
    [Tooltip("自分の画像を使う時に入れる（空なら自動で作る）")]
    public Sprite emblemSprite;
    public float emblemSize = 440f;
    public Color emblemColor = new Color(1f, 0.85f, 0.5f, 0.75f);
    [Tooltip("回る速さ（度/秒）。マイナスで逆回り")]
    public float emblemRotateSpeed = 6f;

    [Header("キラキラ")]
    public bool sparkles = true;
    [Tooltip("自分の画像を使う時に入れる（空なら自動で作る）")]
    public Sprite sparkleSprite;
    [Tooltip("同時に出ている数")]
    public int sparkleCount = 10;
    public Color sparkleColor = new Color(1f, 0.85f, 0.35f, 1f);
    [Tooltip("キラキラが出る範囲（タイトルの位置からの大きさ）")]
    public Vector2 sparkleArea = new Vector2(1300f, 520f);
    public Vector2 sparkleSizeRange = new Vector2(24f, 64f);

    [Header("キャラクター（左下）")]
    public Sprite character;
    public Vector2 characterPosition = new Vector2(-470f, -210f);
    [Tooltip("キャラの高さ (px)。横幅は画像に合わせる")]
    public float characterHeight = 480f;
    public bool characterFlipX = false;

    [Header("項目（右下）")]
    public List<StatRow> rows = new List<StatRow>
    {
        new StatRow { label = "MVP", value = "イシュマール" },
        new StatRow { label = "TURN", value = "6" },
    };
    [Tooltip("1行目の左端の位置（画面の中心から）")]
    public Vector2 rowsPosition = new Vector2(-150f, -158f);
    [Tooltip("行の横幅")]
    public float rowWidth = 720f;
    [Tooltip("行の間隔")]
    public float rowSpacing = 122f;
    public float rowTextSize = 40f;
    [Tooltip("値の位置（行の左端から）")]
    public float valueX = 510f;
    public Color labelColor = new Color(1f, 0.96f, 0.88f, 1f);
    public Color valueColor = Color.white;
    [Tooltip("項目名の左の印の色")]
    public Color bulletColor = new Color(1f, 0.92f, 0.75f, 1f);
    [Tooltip("行の下の線の色")]
    public Color rowLineColor = new Color(1f, 0.92f, 0.75f, 0.6f);

    [Header("出てくるタイミング（秒）")]
    public float dimTime = 0.3f;
    [Tooltip("帯と線が出始める時間")]
    public float bandStart = 0.1f;
    public float bandTime = 0.35f;
    public float emblemStart = 0.2f;
    public float emblemTime = 0.6f;
    public float titleStart = 0.35f;
    public float titleTime = 0.4f;
    [Tooltip("タイトルが左からずれて入ってくる距離 (px)")]
    public float titleSlide = 80f;
    public float sparkleStart = 0.6f;
    public float characterStart = 0.7f;
    public float characterTime = 0.45f;
    [Tooltip("キャラが左から入ってくる距離 (px)")]
    public float characterSlide = 160f;
    public float rowsStart = 0.9f;
    [Tooltip("次の行が出始めるまでのずれ")]
    public float rowStagger = 0.15f;
    public float rowTime = 0.35f;
    [Tooltip("行が右からずれて入ってくる距離 (px)")]
    public float rowSlide = 50f;

    [Header("閉じる")]
    [Tooltip("フェードで消える時間（秒）")]
    public float closeTime = 0.3f;

    [Header("確認用")]
    [Tooltip("画面の左上に確認用のボタンを出す")]
    public bool showTestButton = true;
    public float testButtonScale = 1.5f;

    [Header("閉じるボタン（右下・会話パートの UI と同じ見た目）")]
    [Tooltip("出きったら右下に「閉じる」ボタンを出す（押すとパネルを閉じる）")]
    public bool showNextButton = true;
    [Tooltip("閉じた後に移るシーン名（Build Profiles のシーン一覧に入れておく）。空ならパネルを閉じるだけ")]
    public string nextSceneName = "";
    [Tooltip("ボタンの文字")]
    public string nextLabel = "閉じる";
    [Tooltip("丸の中に出す文字（ボタンアイコンの代わり）")]
    public string nextKeyLabel = "A";
    [Tooltip("ボタンアイコン画像（設定すると丸と文字の代わりに表示）")]
    public Sprite nextButtonIcon;
    [Tooltip("全部出きってから、ボタンが出るまでの時間（秒）")]
    public float nextButtonDelay = 0.2f;
    [Tooltip("マウスはボタンの上をクリックした時だけ反応する（オフなら画面のどこでも）")]
    public bool clickOnlyOnButton = true;
    public Color uiBarColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("マウスが乗っている時の色")]
    public Color uiBarHoverColor = new Color(0.2f, 0.35f, 0.5f, 0.75f);
    public float uiBarHeight = 56f;
    public float uiItemWidth = 190f;
    [Tooltip("画面右下からの余白")]
    public Vector2 uiBarMargin = new Vector2(24f, 20f);
    public float uiTextSize = 24f;
    public Color uiTextColor = Color.white;
    public Color uiBadgeColor = new Color(1f, 1f, 1f, 0.9f);
    public Color uiBadgeTextColor = new Color(0.1f, 0.15f, 0.25f);

    [Header("閉じた後の会話（閉じるボタンから会話パートへ直行）")]
    [Tooltip("閉じるボタンで閉じたら、そのまま会話パートを始める")]
    public bool playConversationOnClose = true;
    [Tooltip("会話を流す ConversationPlayer（空なら自動で探す）")]
    public ConversationPlayer conversationPlayer;
    [Tooltip("流す会話のストーリーID（CSV の story_id）。空なら会話なし")]
    public string conversationStoryId = "story_clear_001";
    [Tooltip("会話の CSV（空なら ConversationPlayer の Csv Asset を使う）")]
    public TextAsset conversationCsv;
    [Tooltip("AfterClose: パネルが消えきってから会話 / Immediately: 押した瞬間に会話を始める（パネルは後ろで消える）")]
    public ConversationTiming conversationTiming = ConversationTiming.AfterClose;

    [Header("イベント")]
    [Tooltip("全部出きった時")]
    public UnityEvent onShown;
    [Tooltip("閉じ終わった時（このあと会話を始めるなど）")]
    public UnityEvent onClosed;
    [Tooltip("閉じるボタンが押された時（閉じ始める時に呼ばれる）")]
    public UnityEvent onNext;
    [Tooltip("閉じた後の会話が終わった時")]
    public UnityEvent onConversationFinished;

    public bool IsPlaying { get; private set; }

    // =========================================================================
    // 内部
    // =========================================================================

    class Row
    {
        public RectTransform rt;
        public CanvasGroup group;
        public TextMeshProUGUI label, value;
        public Image bullet, line;
    }

    class Sparkle
    {
        public RectTransform rt;
        public Image image;
        public float age, life, size, spin;
    }

    Canvas canvas;
    RectTransform root;
    CanvasGroup rootGroup;
    Image dim, smokeImage, bandImage, lineTop, lineBottom, emblemOuter, emblemInner, characterImage;
    readonly List<Image> ornaments = new List<Image>();
    TextMeshProUGUI titleText, flashText;
    readonly List<Row> rowUis = new List<Row>();
    readonly List<Sparkle> sparkleUis = new List<Sparkle>();
    Sprite lineSprite, bandSprite, smokeSprite, diamondSprite, bulletSprite, autoSparkleSprite, autoEmblemOuter, autoEmblemInner;

    float t, introEnd, closeTimer = -1f, shownTimer;
    bool shownInvoked;
    bool goNext;   // 閉じ終わったら次のシーンへ
    Coroutine conversationRoutine;
    RectTransform nextBar;
    CanvasGroup nextGroup;
    Image nextBarImage;
    TextMeshProUGUI nextKeyText, nextLabelText;
    Image nextBadge;
    Sprite circleSprite, roundSprite;
    float emblemAngle;

    static float Dt => Time.unscaledDeltaTime;

    // =========================================================================
    // 公開API
    // =========================================================================

    /// <summary>最初から再生する</summary>
    [ContextMenu("再生（Play）")]
    public void Play()
    {
        if (buildInto && !buildInto.gameObject.activeSelf) buildInto.gameObject.SetActive(true);
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        Build();
        Apply();
        root.gameObject.SetActive(true);
        if (canvas) canvas.gameObject.SetActive(true);
        rootGroup.alpha = 1f;
        t = 0f;
        closeTimer = -1f;
        shownTimer = 0f;
        shownInvoked = false;
        emblemAngle = 0f;
        foreach (var s in sparkleUis) { s.age = 0f; s.life = 0f; s.image.enabled = false; }
        goNext = false;
        IsPlaying = true;
        if (pauseGame) Time.timeScale = 0f;
        Pose();
    }

    /// <summary>フェードして閉じる（出てくる途中なら最後まで出してから閉じる）</summary>
    [ContextMenu("閉じる（Close）")]
    public void Close()
    {
        if (!IsPlaying || closeTimer >= 0f) return;
        t = Mathf.Max(t, introEnd);
        closeTimer = 0f;
    }

    /// <summary>閉じるボタンと同じ（閉じる。Next Scene Name があれば、そのシーンへ）</summary>
    public void GoNext()
    {
        if (!IsPlaying || closeTimer >= 0f) return;
        goNext = true;
        onNext?.Invoke();
        Close();
        if (WillPlayConversation() && conversationTiming == ConversationTiming.Immediately)
            StartConversation();
    }

    /// <summary>閉じた後に会話を流す設定になっているか</summary>
    bool WillPlayConversation()
    {
        if (!playConversationOnClose || string.IsNullOrEmpty(conversationStoryId)) return false;
        if (!conversationPlayer) conversationPlayer = FindAnyObjectByType<ConversationPlayer>(FindObjectsInactive.Include);
        if (!conversationPlayer)
        {
            Debug.LogWarning("[StageClearPanel] ConversationPlayer が見つからないので、会話なしで閉じます", this);
            return false;
        }
        return true;
    }

    void StartConversation()
    {
        if (conversationRoutine != null) return;
        conversationRoutine = StartCoroutine(ConversationRoutine());
    }

    // 会話を流して、終わったら（設定があれば）次のシーンへ
    IEnumerator ConversationRoutine()
    {
        if (pauseGame) Time.timeScale = 1f;   // 会話中はゲームの時間を戻す
        if (!conversationPlayer.gameObject.activeInHierarchy) conversationPlayer.gameObject.SetActive(true);

        if (conversationCsv) conversationPlayer.PlayStory(conversationCsv, conversationStoryId);
        else conversationPlayer.PlayStory(conversationStoryId);

        // 会話が始まるのを少し待つ（ストーリーIDが見つからない時は始まらない）
        float wait = 0f;
        while (!conversationPlayer.IsPlaying && wait < 0.5f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!conversationPlayer.IsPlaying)
            Debug.LogWarning($"[StageClearPanel] 会話「{conversationStoryId}」が始まりませんでした（CSV にそのストーリーIDがあるか確認してください）", this);

        // 会話が終わるまで待つ（ストーリースキップでも終わる）
        while (conversationPlayer.IsPlaying) yield return null;

        // パネルがまだ消えている途中なら待つ
        while (IsPlaying) yield return null;

        conversationRoutine = null;
        onConversationFinished?.Invoke();
        if (buildInto && hidePanelOnClose) buildInto.gameObject.SetActive(false);
        LoadNextScene();
    }

    void LoadNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName)) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>すぐに消す</summary>
    public void HideImmediately()
    {
        IsPlaying = false;
        closeTimer = -1f;
        if (root) root.gameObject.SetActive(false);
        if (pauseGame) Time.timeScale = 1f;
    }

    /// <summary>項目の値を変える（index: 上から 0, 1, ...）</summary>
    public void SetValue(int index, string value)
    {
        if (index < 0 || index >= rows.Count) return;
        rows[index].value = value;
        if (index < rowUis.Count) rowUis[index].value.text = value;
    }

    /// <summary>項目名で値を変える（例：SetValue("TURN", "6")）</summary>
    public void SetValue(string label, string value)
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].label == label) SetValue(i, value);
    }

    /// <summary>キャラクターの画像を変える</summary>
    public void SetCharacter(Sprite sprite)
    {
        character = sprite;
        if (characterImage) ApplyCharacter();
    }

    // =========================================================================
    // Unity
    // =========================================================================

    void Start()
    {
        Build();
        if (playOnStart) Play();
        else
        {
            HideImmediately();
            if (buildInto && hidePanelOnClose) buildInto.gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        if (IsPlaying && pauseGame) Time.timeScale = 1f;
    }

    void Update()
    {
        if (!IsPlaying) return;
        float dt = Dt;
        t += dt;
        emblemAngle += emblemRotateSpeed * dt;

        bool keyPressed = DecideKeyPressed();
        bool clicked = MouseClicked(out Vector2 mousePos);
        bool overButton = NextButtonVisible() && IsOverNextButton(mousePos);

        // 出てくる途中（キーでもクリックでも最後まで飛ばす）
        bool pressed;
        if (t < introEnd)
        {
            if ((keyPressed || clicked) && skipIntroWithInput) t = introEnd;
            pressed = false;
        }
        else if (showNextButton)
        {
            // ボタンが出てから受け付ける。マウスはボタンの上だけ（設定による）
            pressed = NextButtonVisible() && (keyPressed || (clicked && (overButton || !clickOnlyOnButton)));
        }
        else pressed = keyPressed || clicked;

        if (nextBarImage) nextBarImage.color = overButton && closeTimer < 0f ? uiBarHoverColor : uiBarColor;

        if (t >= introEnd && !shownInvoked)
        {
            shownInvoked = true;
            onShown?.Invoke();
        }

        // 閉じる判定
        if (closeTimer < 0f && t >= introEnd)
        {
            shownTimer += dt;
            if (pressed && showNextButton) GoNext();
            else if (closeMode == CloseMode.WaitForInput && pressed) Close();
            else if (closeMode == CloseMode.AutoClose && shownTimer >= autoCloseTime) Close();
        }

        Pose();
        UpdateSparkles(dt);

        // フェードで消える
        if (closeTimer >= 0f)
        {
            closeTimer += dt;
            float k = Mathf.Clamp01(closeTimer / Mathf.Max(0.01f, closeTime));
            rootGroup.alpha = 1f - k;
            root.localScale = Vector3.one * Mathf.Lerp(1f, 1.02f, k);
            if (k >= 1f) Finish();
        }
    }

    void Finish()
    {
        IsPlaying = false;
        closeTimer = -1f;
        root.gameObject.SetActive(false);
        root.localScale = Vector3.one;
        if (pauseGame) Time.timeScale = 1f;
        onClosed?.Invoke();

        bool next = goNext;
        goNext = false;

        // 閉じるボタンで閉じた時は、会話パートへ（会話が終わってから次のシーンへ）
        if (next && (conversationRoutine != null || WillPlayConversation()))
        {
            StartConversation();   // Immediately の時はもう始まっている
            return;
        }

        if (buildInto && hidePanelOnClose) buildInto.gameObject.SetActive(false);
        if (next) LoadNextScene();
    }

    void OnGUI()
    {
        if (!showTestButton) return;
        var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(testButtonScale, testButtonScale, 1f));
        if (GUI.Button(new Rect(10, 60, 180, 40), "STAGE CLEAR 確認")) Play();
        GUI.matrix = old;
    }

    // =========================================================================
    // 入力
    // =========================================================================

    /// <summary>キーボード・パッドの決定ボタン</summary>
    bool DecideKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                           kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)) return true;
        foreach (var pad in Gamepad.all)
            if (pad.buttonSouth.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.JoystickButton0);
#endif
    }

    /// <summary>マウスの左クリック（確認用ボタンの上は除く）。pos には今のマウスの位置が入る</summary>
    bool MouseClicked(out Vector2 pos)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) { pos = new Vector2(-9999f, -9999f); return false; }
        pos = mouse.position.ReadValue();
        return mouse.leftButton.wasPressedThisFrame && !OverTestButton(pos);
#else
        pos = Input.mousePosition;
        return Input.GetMouseButtonDown(0) && !OverTestButton(pos);
#endif
    }

    bool NextButtonVisible() => showNextButton && nextGroup && nextGroup.alpha > 0.5f;

    bool IsOverNextButton(Vector2 screenPos)
    {
        if (!nextBar) return false;
        var c = nextBar.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (c)
        {
            c = c.rootCanvas;
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) cam = c.worldCamera;
        }
        return RectTransformUtility.RectangleContainsScreenPoint(nextBar, screenPos, cam);
    }

    /// <summary>確認用ボタンを押したクリックで閉じないようにする</summary>
    bool OverTestButton(Vector2 screenPos)
    {
        if (!showTestButton) return false;
        float s = testButtonScale;
        float x = screenPos.x, y = Screen.height - screenPos.y;   // GUI は上が 0
        return x >= 10f * s && x <= 190f * s && y >= 60f * s && y <= 100f * s;
    }

    // =========================================================================
    // 動き（時間 t から全部の見た目を決める）
    // =========================================================================

    static float P(float time, float start, float dur) => Mathf.Clamp01((time - start) / Mathf.Max(0.01f, dur));
    static float EaseOutCubic(float k) { k = Mathf.Clamp01(k); float u = 1f - k; return 1f - u * u * u; }

    void Pose()
    {
        // 背景
        float kd = EaseOutCubic(P(t, 0f, dimTime));
        dim.color = new Color(dimColor.r, dimColor.g, dimColor.b, dimColor.a * kd);
        if (smokeImage) smokeImage.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, smokeColor.a * EaseOutCubic(P(t, characterStart - 0.1f, characterTime)));

        // 帯と線
        float kb = EaseOutCubic(P(t, bandStart, bandTime));
        bandImage.rectTransform.localScale = new Vector3(1f, kb, 1f);
        bandImage.color = new Color(bandColor.r, bandColor.g, bandColor.b, bandColor.a * kb);
        float kl = EaseOutCubic(P(t, bandStart, bandTime * 1.4f));
        foreach (var l in new[] { lineTop, lineBottom })
        {
            l.rectTransform.sizeDelta = new Vector2(lineLength * kl, lineThickness);
            l.color = new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * Mathf.Clamp01(kl * 2f));
        }
        foreach (var o in ornaments)
            o.color = new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * P(t, bandStart + bandTime, 0.2f));

        // 魔法陣
        if (emblemOuter)
        {
            float ke = P(t, emblemStart, emblemTime);
            float s = Mathf.Lerp(1.4f, 1f, EaseOutCubic(ke));
            var c = new Color(emblemColor.r, emblemColor.g, emblemColor.b, emblemColor.a * ke);
            emblemOuter.color = c;
            emblemOuter.rectTransform.localScale = Vector3.one * s;
            emblemOuter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, emblemAngle);
            if (emblemInner)
            {
                emblemInner.color = c;
                emblemInner.rectTransform.localScale = Vector3.one * s;
                emblemInner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -emblemAngle * 1.6f);
            }
        }

        // タイトル
        float kt = P(t, titleStart, titleTime);
        float et = EaseOutCubic(kt);
        titleText.rectTransform.anchoredPosition = titlePosition + new Vector2(Mathf.Lerp(-titleSlide, 0f, et), 0f);
        titleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, et);
        titleText.alpha = kt;

        if (flashText)
        {
            float kf = P(t, titleStart + titleTime * 0.7f, 0.5f);
            bool on = titleFlash && kf > 0f && kf < 1f;
            flashText.gameObject.SetActive(on);
            if (on)
            {
                flashText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.3f, EaseOutCubic(kf));
                flashText.alpha = 0.8f * (1f - kf);
            }
        }

        // キャラクター
        if (characterImage.enabled)
        {
            float kc = P(t, characterStart, characterTime);
            var rt = characterImage.rectTransform;
            rt.anchoredPosition = characterPosition + new Vector2(Mathf.Lerp(-characterSlide, 0f, EaseOutCubic(kc)), 0f);
            var cc = characterImage.color;
            cc.a = kc;
            characterImage.color = cc;
        }

        // 項目
        for (int i = 0; i < rowUis.Count; i++)
        {
            var r = rowUis[i];
            float kr = P(t, rowsStart + i * rowStagger, rowTime);
            float er = EaseOutCubic(kr);
            r.group.alpha = kr;
            r.rt.anchoredPosition = RowPosition(i) + new Vector2(Mathf.Lerp(rowSlide, 0f, er), 0f);
            r.line.fillAmount = EaseOutCubic(P(t, rowsStart + i * rowStagger, rowTime * 1.5f));
        }

        PoseNextButton();
    }

    void PoseNextButton()
    {
        if (!nextBar) return;
        float k = showNextButton ? EaseOutCubic(P(t, introEnd + nextButtonDelay, 0.25f)) : 0f;
        nextGroup.alpha = k;
        nextBar.anchoredPosition = new Vector2(-uiBarMargin.x, uiBarMargin.y + Mathf.Lerp(-12f, 0f, k));
    }

    Vector2 RowPosition(int i) => rowsPosition + new Vector2(0f, -rowSpacing * i);

    void UpdateSparkles(float dt)
    {
        bool active = sparkles && t >= sparkleStart && closeTimer < 0f;
        foreach (var s in sparkleUis)
        {
            if (s.life <= 0f || s.age >= s.life)
            {
                if (!active) { s.image.enabled = false; continue; }
                // 新しく出す（少しずつずらして出るように、たまにお休み）
                if (UnityEngine.Random.value > 0.08f) { s.image.enabled = false; s.life = 0f; continue; }
                s.age = 0f;
                s.life = UnityEngine.Random.Range(0.5f, 1.2f);
                s.size = UnityEngine.Random.Range(sparkleSizeRange.x, sparkleSizeRange.y);
                s.spin = UnityEngine.Random.Range(-40f, 40f);
                s.rt.anchoredPosition = titlePosition + new Vector2(
                    UnityEngine.Random.Range(-0.5f, 0.5f) * sparkleArea.x,
                    UnityEngine.Random.Range(-0.5f, 0.5f) * sparkleArea.y);
                s.rt.localRotation = Quaternion.identity;
                s.image.enabled = true;
            }
            s.age += dt;
            float k = Mathf.Clamp01(s.age / s.life);
            float a = Mathf.Sin(k * Mathf.PI);   // ふわっと出て、ふわっと消える
            s.rt.sizeDelta = Vector2.one * s.size * Mathf.Lerp(0.4f, 1f, a);
            s.rt.localRotation = Quaternion.Euler(0f, 0f, s.spin * s.age);
            s.image.color = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, sparkleColor.a * a);
        }
    }

    // =========================================================================
    // 見た目を設定に合わせる
    // =========================================================================

    void Apply()
    {
        // 終わる時間
        introEnd = Mathf.Max(dimTime, bandStart + bandTime * 1.4f, emblemStart + emblemTime, titleStart + titleTime + 0.5f,
                             characterStart + characterTime,
                             rowsStart + Mathf.Max(0, rows.Count - 1) * rowStagger + rowTime * 1.5f);

        if (smokeImage)
        {
            smokeImage.gameObject.SetActive(smoke);
            smokeImage.rectTransform.anchoredPosition = characterPosition + new Vector2(60f, -40f);
        }

        // 帯
        bandImage.rectTransform.anchoredPosition = titlePosition;
        bandImage.rectTransform.sizeDelta = new Vector2(2400f, bandHeight);
        lineTop.rectTransform.anchoredPosition = titlePosition + new Vector2(0f, bandHeight * 0.5f);
        lineBottom.rectTransform.anchoredPosition = titlePosition - new Vector2(0f, bandHeight * 0.5f);
        float ox = lineLength * 0.36f;
        var orn = new[] { new Vector2(-ox, 1f), new Vector2(ox, 1f), new Vector2(-ox, -1f), new Vector2(ox, -1f) };
        for (int i = 0; i < ornaments.Count; i++)
        {
            ornaments[i].gameObject.SetActive(lineOrnaments);
            ornaments[i].rectTransform.anchoredPosition = titlePosition + new Vector2(orn[i].x, orn[i].y * bandHeight * 0.5f);
        }

        // 魔法陣
        emblemOuter.gameObject.SetActive(emblem);
        emblemInner.gameObject.SetActive(emblem && !emblemSprite);
        emblemOuter.sprite = emblemSprite ? emblemSprite : autoEmblemOuter;
        emblemOuter.rectTransform.anchoredPosition = emblemInner.rectTransform.anchoredPosition = titlePosition;
        emblemOuter.rectTransform.sizeDelta = Vector2.one * emblemSize;
        emblemInner.rectTransform.sizeDelta = Vector2.one * emblemSize * 0.62f;

        // タイトル
        foreach (var tt in new[] { titleText, flashText })
        {
            if (titleFont) tt.font = titleFont;
            tt.text = title;
            tt.fontSize = titleSize;
            tt.fontStyle = (titleBold ? FontStyles.Bold : FontStyles.Normal) | (titleItalic ? FontStyles.Italic : FontStyles.Normal);
            tt.rectTransform.anchoredPosition = titlePosition;
        }
        titleText.color = titleColor;
        flashText.color = Color.white;
        ApplyTitleGlow();

        // キラキラ
        while (sparkleUis.Count < sparkleCount)
        {
            var img = NewImage("Sparkle", root, Vector2.zero, Vector2.one * 40f, null, Color.clear);
            img.rectTransform.SetSiblingIndex(titleText.rectTransform.GetSiblingIndex() + 1);
            sparkleUis.Add(new Sparkle { rt = img.rectTransform, image = img });
        }
        for (int i = 0; i < sparkleUis.Count; i++)
        {
            sparkleUis[i].image.sprite = sparkleSprite ? sparkleSprite : autoSparkleSprite;
            sparkleUis[i].rt.gameObject.SetActive(i < sparkleCount);
        }

        ApplyCharacter();
        ApplyRows();
        ApplyNextButton();
    }

    void ApplyNextButton()
    {
        nextBar.gameObject.SetActive(showNextButton);
        nextBar.sizeDelta = new Vector2(uiItemWidth + 16f, uiBarHeight);
        nextBarImage.color = uiBarColor;

        float bs = uiBarHeight - 22f;
        var b = nextBadge.rectTransform;
        b.anchoredPosition = new Vector2(8f + uiBarHeight * 0.5f - 4f, 0f);
        b.sizeDelta = new Vector2(bs, bs);
        nextBadge.sprite = nextButtonIcon ? nextButtonIcon : circleSprite;
        nextBadge.color = nextButtonIcon ? Color.white : uiBadgeColor;

        nextKeyText.gameObject.SetActive(!nextButtonIcon);
        nextKeyText.text = "<b>" + nextKeyLabel + "</b>";
        nextKeyText.fontSize = bs * 0.42f;
        nextKeyText.color = uiBadgeTextColor;

        nextLabelText.text = nextLabel;
        nextLabelText.fontSize = uiTextSize;
        nextLabelText.color = uiTextColor;
        nextLabelText.rectTransform.offsetMin = new Vector2(8f + uiBarHeight + 2f, 0f);
        nextLabelText.rectTransform.offsetMax = new Vector2(-6f, 0f);
        foreach (var tx in new[] { nextKeyText, nextLabelText })
            if (textFont) tx.font = textFont;
    }

    void ApplyTitleGlow()
    {
        // TMP のマテリアルの Underlay を使って、文字の周りをぼんやり光らせる
        var m = titleText.fontMaterial;   // この文字専用のマテリアル（元のフォントは変わらない）
        if (titleGlow)
        {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", titleGlowColor);
            m.SetFloat("_UnderlayOffsetX", 0f);
            m.SetFloat("_UnderlayOffsetY", 0f);
            m.SetFloat("_UnderlayDilate", 0.5f);
            m.SetFloat("_UnderlaySoftness", 0.8f);
        }
        else m.DisableKeyword("UNDERLAY_ON");
    }

    void ApplyCharacter()
    {
        characterImage.enabled = character;
        if (!character) return;
        characterImage.sprite = character;
        float aspect = character.rect.height > 0f ? character.rect.width / character.rect.height : 1f;
        characterImage.rectTransform.sizeDelta = new Vector2(characterHeight * aspect, characterHeight);
        characterImage.rectTransform.localScale = new Vector3(characterFlipX ? -1f : 1f, 1f, 1f);
        characterImage.color = Color.white;
    }

    void ApplyRows()
    {
        while (rowUis.Count < rows.Count) rowUis.Add(NewRow());
        for (int i = 0; i < rowUis.Count; i++)
        {
            var r = rowUis[i];
            bool use = i < rows.Count;
            r.rt.gameObject.SetActive(use);
            if (!use) continue;

            r.rt.sizeDelta = new Vector2(rowWidth, rowTextSize * 1.6f);
            r.label.text = rows[i].label;
            r.value.text = rows[i].value;
            foreach (var tx in new[] { r.label, r.value })
            {
                if (textFont) tx.font = textFont;
                tx.fontSize = rowTextSize;
            }
            r.label.color = labelColor;
            r.value.color = valueColor;
            r.bullet.color = bulletColor;
            r.line.color = rowLineColor;

            float h = rowTextSize * 1.6f;
            r.bullet.rectTransform.anchoredPosition = new Vector2(rowTextSize * 0.4f, 0f);
            r.bullet.rectTransform.sizeDelta = Vector2.one * rowTextSize * 0.8f;
            r.label.rectTransform.anchoredPosition = new Vector2(rowTextSize * 0.95f, 0f);
            r.label.rectTransform.sizeDelta = new Vector2(valueX - rowTextSize, h);
            r.value.rectTransform.anchoredPosition = new Vector2(valueX, 0f);
            r.value.rectTransform.sizeDelta = new Vector2(Mathf.Max(100f, (rowWidth - valueX) * 2f), h);
            r.line.rectTransform.anchoredPosition = new Vector2(0f, -h * 0.5f - 6f);
            r.line.rectTransform.sizeDelta = new Vector2(rowWidth, 3f);
        }
    }

    // =========================================================================
    // 部品を作る
    // =========================================================================

    void Build()
    {
        if (root) return;

        lineSprite = MakeLineSprite();
        bandSprite = MakeBandSprite();
        smokeSprite = MakeSmokeSprite();
        diamondSprite = MakeDiamondSprite();
        bulletSprite = MakeStarSprite(8, 64, 0.18f);
        autoSparkleSprite = MakeStarSprite(4, 64, 0.08f);
        autoEmblemOuter = MakeEmblemSprite(512, false);
        autoEmblemInner = MakeEmblemSprite(256, true);

        Transform parent;
        if (buildInto) parent = buildInto;
        else
        {
            var cgo = new GameObject("StageClearCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            parent = cgo.transform;
        }

        var go = new GameObject("StageClear", typeof(RectTransform));
        root = (RectTransform)go.transform;
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        rootGroup = go.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        // 描く順番（後ほど手前）
        dim = NewImage("Dim", root, Vector2.zero, Vector2.zero, null, Color.clear);
        dim.rectTransform.anchorMin = Vector2.zero;
        dim.rectTransform.anchorMax = Vector2.one;
        dim.rectTransform.sizeDelta = Vector2.zero;

        smokeImage = NewImage("Smoke", root, characterPosition + new Vector2(60f, -40f), new Vector2(1100f, 600f), smokeSprite, Color.clear);

        bandImage = NewImage("Band", root, Vector2.zero, Vector2.zero, bandSprite, Color.clear);
        emblemOuter = NewImage("Emblem", root, Vector2.zero, Vector2.zero, null, Color.clear);
        emblemInner = NewImage("EmblemInner", root, Vector2.zero, Vector2.zero, autoEmblemInner, Color.clear);
        lineTop = NewImage("LineTop", root, Vector2.zero, Vector2.zero, lineSprite, Color.clear);
        lineBottom = NewImage("LineBottom", root, Vector2.zero, Vector2.zero, lineSprite, Color.clear);
        for (int i = 0; i < 4; i++)
            ornaments.Add(NewImage("Ornament", root, Vector2.zero, new Vector2(22f, 22f), diamondSprite, Color.clear));

        flashText = NewText("TitleFlash", root, new Vector2(1600f, 260f), TextAlignmentOptions.Center);
        titleText = NewText("Title", root, new Vector2(1600f, 260f), TextAlignmentOptions.Center);

        characterImage = NewImage("Character", root, Vector2.zero, Vector2.zero, null, Color.white);
        characterImage.preserveAspect = true;

        // 右下の「閉じる」ボタン（会話パートの ログ／スキップ と同じ形：角の丸い半透明の黒）
        circleSprite = MakeCircleSprite();
        roundSprite = MakeRoundRectSprite();
        nextBarImage = NewImage("CloseButton", root, Vector2.zero, Vector2.zero, roundSprite, Color.clear);
        nextBarImage.type = Image.Type.Sliced;
        nextBar = nextBarImage.rectTransform;
        nextBar.anchorMin = nextBar.anchorMax = new Vector2(1f, 0f);
        nextBar.pivot = new Vector2(1f, 0f);
        nextGroup = nextBar.gameObject.AddComponent<CanvasGroup>();
        nextGroup.alpha = 0f;

        nextBadge = NewImage("Badge", nextBar, Vector2.zero, Vector2.zero, null, Color.white);
        nextBadge.preserveAspect = true;
        var br = nextBadge.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0f, 0.5f);
        br.pivot = new Vector2(0.5f, 0.5f);

        nextKeyText = NewText("Key", br, Vector2.zero, TextAlignmentOptions.Center);
        var kr = nextKeyText.rectTransform;
        kr.anchorMin = Vector2.zero;
        kr.anchorMax = Vector2.one;
        kr.offsetMin = kr.offsetMax = Vector2.zero;

        nextLabelText = NewText("Label", nextBar, Vector2.zero, TextAlignmentOptions.MidlineLeft);
        var lr = nextLabelText.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
    }

    Row NewRow()
    {
        var rt = NewRect("Row", root, Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(0f, 0.5f);
        var r = new Row { rt = rt, group = rt.gameObject.AddComponent<CanvasGroup>() };
        r.bullet = NewImage("Bullet", rt, Vector2.zero, Vector2.zero, bulletSprite, Color.white);
        r.label = NewText("Label", rt, Vector2.zero, TextAlignmentOptions.Left);
        r.value = NewText("Value", rt, Vector2.zero, TextAlignmentOptions.Center);
        r.line = NewImage("Line", rt, Vector2.zero, Vector2.zero, lineSprite, Color.white);
        r.line.type = Image.Type.Filled;
        r.line.fillMethod = Image.FillMethod.Horizontal;
        r.line.fillOrigin = 0;
        // 行の中の部品は左端を基準にする
        foreach (var c in new RectTransform[] { r.bullet.rectTransform, r.label.rectTransform, r.line.rectTransform })
        {
            c.anchorMin = c.anchorMax = new Vector2(0f, 0.5f);
            c.pivot = new Vector2(c == r.bullet.rectTransform ? 0.5f : 0f, 0.5f);
        }
        r.value.rectTransform.anchorMin = r.value.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        r.value.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return r;
    }

    static RectTransform NewRect(string n, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(n, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image NewImage(string n, Transform parent, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        var img = NewRect(n, parent, pos, size).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI NewText(string n, Transform parent, Vector2 size, TextAlignmentOptions align)
    {
        var t = NewRect(n, parent, Vector2.zero, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // =========================================================================
    // 画像の自動生成
    // =========================================================================

    static Texture2D NewTex(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    static Sprite ToSprite(Texture2D tex, Color32[] px)
    {
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    static Color32 White(float a) => new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));

    /// <summary>真ん中が明るく、左右の端に向かって消える細い線</summary>
    static Sprite MakeLineSprite()
    {
        const int w = 256, h = 8;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float ends = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.25f);
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                px[y * w + x] = White(ends * Mathf.Clamp01(1.4f - v * 1.4f));
            }
        return ToSprite(tex, px);
    }

    /// <summary>横長の帯（上下と左右の端がやわらかく消える）</summary>
    static Sprite MakeBandSprite()
    {
        const int w = 128, h = 64;
        var tex = NewTex(w, h);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1), v = y / (float)(h - 1);
                float sides = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.2f);
                float edge = Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.12f);
                px[y * w + x] = White(sides * edge);
            }
        return ToSprite(tex, px);
    }

    /// <summary>ぼんやりした煙（中心ほど濃い）</summary>
    static Sprite MakeSmokeSprite()
    {
        const int s = 128;
        var tex = NewTex(s, s);
        var px = new Color32[s * s];
        var rnd = new System.Random(3);
        var blobs = new Vector3[7];
        for (int i = 0; i < blobs.Length; i++)
            blobs[i] = new Vector3((float)rnd.NextDouble() * 0.6f + 0.2f, (float)rnd.NextDouble() * 0.5f + 0.25f, (float)rnd.NextDouble() * 0.15f + 0.15f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)(s - 1), v = y / (float)(s - 1);
                float a = 0f;
                foreach (var b in blobs)
                {
                    float dx = u - b.x, dy = v - b.y;
                    a += Mathf.Exp(-(dx * dx + dy * dy) / (b.z * b.z));
                }
                px[y * s + x] = White(Mathf.Clamp01(a * 0.5f));
            }
        return ToSprite(tex, px);
    }

    /// <summary>角の丸い四角（ボタンの背景・9スライス）</summary>
    static Sprite MakeRoundRectSprite()
    {
        const int s = 64, r = 16;
        var tex = NewTex(s, s);
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                px[y * s + x] = White(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    /// <summary>白い丸（ボタンの印）</summary>
    static Sprite MakeCircleSprite()
    {
        const int s = 64;
        var tex = NewTex(s, s);
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = x + 0.5f - s * 0.5f, dy = y + 0.5f - s * 0.5f;
                px[y * s + x] = White(s * 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
            }
        return ToSprite(tex, px);
    }

    /// <summary>小さなひし形</summary>
    static Sprite MakeDiamondSprite()
    {
        const int s = 32;
        var tex = NewTex(s, s);
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Abs(x + 0.5f - s * 0.5f) + Mathf.Abs(y + 0.5f - s * 0.5f);
                px[y * s + x] = White(s * 0.5f - d);
            }
        return ToSprite(tex, px);
    }

    /// <summary>とがった星（points: とがりの数）。キラキラと、項目の印に使う</summary>
    static Sprite MakeStarSprite(int points, int s, float core)
    {
        var tex = NewTex(s, s);
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                // 角度によって半径が変わる（とがり）
                float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * points * 0.5f)), 6f);
                float limit = Mathf.Lerp(0.18f, 1f, spike);
                float a = Mathf.Clamp01((limit - r) * s * 0.25f);
                a = Mathf.Max(a, Mathf.Exp(-r * r / (core * core)));   // 中心の光
                px[y * s + x] = White(a);
            }
        return ToSprite(tex, px);
    }

    /// <summary>魔法陣（輪と目盛りと四角形）。inner = 内側の小さい方</summary>
    static Sprite MakeEmblemSprite(int s, bool inner)
    {
        var tex = NewTex(s, s);
        var px = new Color32[s * s];
        float pxSize = 2f / s;   // 1ピクセルの大きさ（-1〜1 の座標で）
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float a = 0f;

                if (!inner)
                {
                    a = Mathf.Max(a, Ring(r, 0.97f, 0.012f, pxSize));
                    a = Mathf.Max(a, Ring(r, 0.90f, 0.006f, pxSize));
                    a = Mathf.Max(a, Ring(r, 0.80f, 0.004f, pxSize) * 0.8f);
                    // 目盛り（0.90〜0.97 の間）
                    if (r > 0.905f && r < 0.965f)
                    {
                        float m = Mathf.Repeat(ang, 5f);
                        float d = Mathf.Min(m, 5f - m) * Mathf.Deg2Rad * r;
                        a = Mathf.Max(a, Mathf.Clamp01(1f - (d - 0.004f) / pxSize) * 0.9f);
                    }
                    // 小さな点の列（0.85 の輪の上）
                    {
                        float m = Mathf.Repeat(ang, 15f);
                        float da = Mathf.Min(m, 15f - m) * Mathf.Deg2Rad * 0.85f;
                        float dr = r - 0.85f;
                        float d = Mathf.Sqrt(da * da + dr * dr);
                        a = Mathf.Max(a, Mathf.Clamp01(1f - (d - 0.012f) / pxSize));
                    }
                    // 重なった2つの四角形（輪の内側）
                    a = Mathf.Max(a, Square(dx, dy, 0.56f, 0f, pxSize) * 0.85f);
                    a = Mathf.Max(a, Square(dx, dy, 0.56f, 45f, pxSize) * 0.85f);
                }
                else
                {
                    a = Mathf.Max(a, Ring(r, 0.95f, 0.02f, pxSize));
                    a = Mathf.Max(a, Ring(r, 0.75f, 0.012f, pxSize));
                    // 放射状の線
                    if (r > 0.3f && r < 0.75f)
                    {
                        float m = Mathf.Repeat(ang, 30f);
                        float d = Mathf.Min(m, 30f - m) * Mathf.Deg2Rad * r;
                        a = Mathf.Max(a, Mathf.Clamp01(1f - (d - 0.006f) / pxSize) * 0.7f);
                    }
                    a = Mathf.Max(a, Ring(r, 0.3f, 0.012f, pxSize));
                }
                // 全体を外側ほど少し薄く
                px[y * s + x] = White(a * Mathf.Lerp(1f, 0.75f, r));
            }
        return ToSprite(tex, px);
    }

    static float Ring(float r, float radius, float halfWidth, float aa)
    {
        float d = Mathf.Abs(r - radius) - halfWidth;
        return Mathf.Clamp01(1f - d / aa);
    }

    /// <summary>中心にある正方形のふち（size: 半分の大きさ、angle: 傾き）</summary>
    static float Square(float x, float y, float size, float angle, float aa)
    {
        float rad = angle * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        float rx = x * c + y * s, ry = -x * s + y * c;
        float d = Mathf.Abs(Mathf.Max(Mathf.Abs(rx), Mathf.Abs(ry)) - size) - 0.004f;
        return Mathf.Clamp01(1f - d / aa);
    }
}