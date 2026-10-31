using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ゲームオーバーのメニュー（青いパネル ＋ 説明文 ＋ 選択肢2つ）。
/// GAME OVER の文字（GameOverText）が上へ移動し終わったら、自動で出てくる。
/// パネルやボタンは自動で作るので、空の GameObject に付けて Game Over Text を入れるだけで動く。
///
/// 操作：
///   キーボード … ↑↓ / W S で選ぶ、Enter / Space / Z で決定
///   パッド     … 十字キー / 左スティックで選ぶ、A で決定
///   マウス     … ボタンに乗せると選ばれる、クリックで決定
/// </summary>
public class GameOverMenu : MonoBehaviour
{
    [Header("つなぐもの")]
    [Tooltip("GAME OVER の文字。上へ移動し終わったらメニューが出る（空なら自動で探す）")]
    public GameOverText gameOverText;
    [Tooltip("日本語が出る TMP フォント（空だと TMP の標準フォント。日本語が □ になる時は入れる）")]
    public TMP_FontAsset font;

    [Header("文字")]
    [Tooltip("説明文（あとで書き換える）")]
    [TextArea(2, 5)] public string description = "（ここに説明文が入ります）";
    public string continueLabel = "コンテニューする";
    public string mapLabel = "マップに戻る";

    [Header("決定した時")]
    [Tooltip("「コンテニューする」で読み込むシーン名。空ならシーンは読み込まない（On Continue だけ呼ぶ）")]
    public string continueSceneName = "";
    [Tooltip("「マップに戻る」で読み込むシーン名。空ならシーンは読み込まない（On Return To Map だけ呼ぶ）")]
    public string mapSceneName = "";
    [Tooltip("決定してから実行するまでの間（秒）。ボタンが光るのを見せる")]
    public float decideDelay = 0.25f;
    public UnityEvent onContinue;
    public UnityEvent onReturnToMap;

    [Header("パネルの見た目")]
    public Vector2 panelSize = new Vector2(1000f, 520f);
    [Tooltip("パネルの位置（画面の中心から）")]
    public Vector2 panelPosition = new Vector2(0f, -80f);
    public Color panelColorTop = new Color(0.10f, 0.30f, 0.62f, 0.95f);
    public Color panelColorBottom = new Color(0.04f, 0.14f, 0.38f, 0.95f);
    [Tooltip("パネルのふちの光")]
    public Color panelBorderColor = new Color(0.55f, 0.85f, 1f, 1f);
    public float descriptionSize = 34f;

    [Header("ボタンの見た目")]
    public Vector2 buttonSize = new Vector2(600f, 76f);
    public float buttonSpacing = 22f;
    public float buttonTextSize = 34f;
    public Color buttonColor = new Color(0.08f, 0.26f, 0.60f, 1f);
    [Tooltip("選ばれている時の色")]
    public Color buttonSelectedColor = new Color(0.36f, 0.62f, 0.95f, 1f);
    public Color buttonTextColor = Color.white;
    [Tooltip("選ばれているボタンの左に出る矢印の色")]
    public Color cursorColor = new Color(0.85f, 0.95f, 1f, 1f);

    [Header("出てくる演出")]
    [Tooltip("パネルが出てくる時間（秒）")]
    public float showTime = 0.3f;

    [Header("その他")]
    [Tooltip("他の UI より手前に出す順番")]
    public int sortingOrder = 210;

    /// <summary>メニューが出ているか</summary>
    public bool IsShown { get; private set; }

    // ---- 内部 ----
    class MenuButton
    {
        public RectTransform rt;
        public Image image, outline;
        public TextMeshProUGUI label;
    }

    Canvas canvas;
    RectTransform panel, cursor;
    CanvasGroup group;
    TextMeshProUGUI descriptionText;
    readonly List<MenuButton> buttons = new List<MenuButton>();
    int selected;
    float showTimer, cursorTimer, decideTimer = -1f, flashTimer;
    bool locked;
    float prevVertical;

    static float Dt => Time.unscaledDeltaTime;

    // =========================================================================
    // 公開API
    // =========================================================================

    /// <summary>メニューを出す</summary>
    [ContextMenu("メニューを出す（Show）")]
    public void Show()
    {
        Build();
        ApplyTexts();
        canvas.gameObject.SetActive(true);
        IsShown = true;
        locked = false;
        decideTimer = -1f;
        showTimer = 0f;
        Select(0);
        UpdateShowAnimation();
    }

    /// <summary>メニューをすぐ消す</summary>
    public void HideImmediately()
    {
        IsShown = false;
        if (canvas) canvas.gameObject.SetActive(false);
    }

    // =========================================================================
    // Unity
    // =========================================================================

    void Awake()
    {
        if (!gameOverText) gameOverText = FindAnyObjectByType<GameOverText>(FindObjectsInactive.Include);
        Build();
        HideImmediately();
    }

    void OnEnable()
    {
        if (gameOverText) gameOverText.onMoved.AddListener(Show);
    }

    void OnDisable()
    {
        if (gameOverText) gameOverText.onMoved.RemoveListener(Show);
    }

    void Update()
    {
        if (!IsShown) return;

        if (showTimer < showTime)
        {
            showTimer += Dt;
            UpdateShowAnimation();
        }

        // 矢印を左右にゆらす
        cursorTimer += Dt;
        if (cursor && selected < buttons.Count)
        {
            var b = buttons[selected].rt;
            float sway = Mathf.Sin(cursorTimer * 6f) * 6f;
            cursor.anchoredPosition = b.anchoredPosition + new Vector2(-buttonSize.x * 0.5f - 34f + sway, 0f);
        }

        // 決定したボタンを光らせる
        if (flashTimer > 0f)
        {
            flashTimer -= Dt;
            var b = buttons[selected];
            bool on = Mathf.Repeat(flashTimer, 0.1f) > 0.05f;
            b.image.color = on ? Color.white : buttonSelectedColor;
            b.label.color = on ? buttonSelectedColor : buttonTextColor;
        }

        if (decideTimer >= 0f)
        {
            decideTimer -= Dt;
            if (decideTimer < 0f) Execute(selected);
            return;
        }

        if (locked || showTimer < showTime) return;
        HandleInput();
    }

    // =========================================================================
    // 操作
    // =========================================================================

    void HandleInput()
    {
        int move = 0;
        bool decide = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) move = -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) move = 1;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                kb.spaceKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame) decide = true;
        }

        float vertical = 0f;
        foreach (var pad in Gamepad.all)
        {
            if (pad.dpad.up.wasPressedThisFrame) move = -1;
            if (pad.dpad.down.wasPressedThisFrame) move = 1;
            if (pad.buttonSouth.wasPressedThisFrame) decide = true;
            float y = pad.leftStick.ReadValue().y;
            if (Mathf.Abs(y) > Mathf.Abs(vertical)) vertical = y;
        }
        // スティックは倒した瞬間だけ1つ動かす
        if (vertical > 0.5f && prevVertical <= 0.5f) move = -1;
        if (vertical < -0.5f && prevVertical >= -0.5f) move = 1;
        prevVertical = vertical;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            int hover = HoveredButton(mp);
            if (hover >= 0 && mouse.delta.ReadValue().sqrMagnitude > 0.01f && hover != selected) Select(hover);
            if (hover >= 0 && mouse.leftButton.wasPressedThisFrame) { Select(hover); decide = true; }
        }
#else
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) move = -1;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) move = 1;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.JoystickButton0)) decide = true;
        float vertical = Input.GetAxisRaw("Vertical");
        if (vertical > 0.5f && prevVertical <= 0.5f) move = -1;
        if (vertical < -0.5f && prevVertical >= -0.5f) move = 1;
        prevVertical = vertical;

        int hover = HoveredButton(Input.mousePosition);
        if (hover >= 0 && hover != selected && (Input.GetAxis("Mouse X") != 0f || Input.GetAxis("Mouse Y") != 0f)) Select(hover);
        if (hover >= 0 && Input.GetMouseButtonDown(0)) { Select(hover); decide = true; }
#endif

        if (move != 0) Select((selected + move + buttons.Count) % buttons.Count);
        if (decide) Decide();
    }

    int HoveredButton(Vector2 screenPos)
    {
        for (int i = 0; i < buttons.Count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(buttons[i].rt, screenPos, null)) return i;
        return -1;
    }

    void Select(int index)
    {
        selected = Mathf.Clamp(index, 0, buttons.Count - 1);
        for (int i = 0; i < buttons.Count; i++)
        {
            bool on = i == selected;
            var b = buttons[i];
            b.image.color = on ? buttonSelectedColor : buttonColor;
            b.label.color = buttonTextColor;
            b.outline.enabled = on;
            b.rt.localScale = on ? new Vector3(1.03f, 1.03f, 1f) : Vector3.one;
        }
        cursorTimer = 0f;
    }

    void Decide()
    {
        locked = true;
        flashTimer = decideDelay;
        decideTimer = decideDelay;
    }

    void Execute(int index)
    {
        if (index == 0)
        {
            Debug.Log("[GameOverMenu] コンテニューする");
            onContinue?.Invoke();
            LoadScene(continueSceneName);
        }
        else
        {
            Debug.Log("[GameOverMenu] マップに戻る");
            onReturnToMap?.Invoke();
            LoadScene(mapSceneName);
        }
    }

    static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        Time.timeScale = 1f;   // ゲームを止めていた時のために戻す
        SceneManager.LoadScene(sceneName);
    }

    // =========================================================================
    // 見た目
    // =========================================================================

    void UpdateShowAnimation()
    {
        float k = showTime > 0f ? Mathf.Clamp01(showTimer / showTime) : 1f;
        float e = 1f - (1f - k) * (1f - k) * (1f - k);
        group.alpha = e;
        panel.localScale = new Vector3(Mathf.Lerp(0.92f, 1f, e), Mathf.Lerp(0.6f, 1f, e), 1f);
    }

    void ApplyTexts()
    {
        descriptionText.text = description;
        descriptionText.fontSize = descriptionSize;
        string[] labels = { continueLabel, mapLabel };
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].label.text = labels[i];
            buttons[i].label.fontSize = buttonTextSize;
        }
        if (font)
        {
            descriptionText.font = font;
            foreach (var b in buttons) b.label.font = font;
        }
    }

    void Build()
    {
        if (canvas) return;

        // 専用の Canvas（いちばん手前）
        var cgo = new GameObject("GameOverMenuCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = cgo.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        // パネル（外側の光 → 本体）
        panel = NewRect("Panel", cgo.transform, panelPosition, panelSize);
        var glow = NewImage("Glow", panel, Vector2.zero, panelSize + new Vector2(60f, 60f), MakeGlowSprite(), panelBorderColor);
        glow.color = new Color(panelBorderColor.r, panelBorderColor.g, panelBorderColor.b, 0.55f);
        NewImage("Body", panel, Vector2.zero, panelSize, MakePanelSprite(panelColorTop, panelColorBottom, panelBorderColor), Color.white);

        // 四隅の飾り（小さなひし形）
        float cx = panelSize.x * 0.5f - 14f, cy = panelSize.y * 0.5f - 14f;
        var diamond = MakeDiamondSprite();
        foreach (var p in new[] { new Vector2(-cx, cy), new Vector2(cx, cy), new Vector2(-cx, -cy), new Vector2(cx, -cy) })
            NewImage("Corner", panel, p, new Vector2(26f, 26f), diamond, panelBorderColor);

        // 説明文
        descriptionText = NewText("Description", panel, new Vector2(0f, panelSize.y * 0.5f - 130f),
            new Vector2(panelSize.x - 120f, 170f), descriptionSize);

        // 選択肢
        var pill = MakePillSprite();
        var pillOutline = MakePillOutlineSprite();
        float firstY = -panelSize.y * 0.5f + 70f + (buttonSize.y + buttonSpacing) * 1f + buttonSize.y * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            var pos = new Vector2(0f, firstY - i * (buttonSize.y + buttonSpacing) - buttonSize.y * 0.5f);
            var img = NewImage("Button" + i, panel, pos, buttonSize, pill, buttonColor);
            img.type = Image.Type.Sliced;
            var outline = NewImage("Outline", img.rectTransform, Vector2.zero, buttonSize + new Vector2(8f, 8f), pillOutline, Color.white);
            outline.type = Image.Type.Sliced;
            var label = NewText("Label", img.rectTransform, Vector2.zero, buttonSize, buttonTextSize);
            buttons.Add(new MenuButton { rt = img.rectTransform, image = img, outline = outline, label = label });
        }

        // 選ばれているボタンの左の矢印
        cursor = NewImage("Cursor", panel, Vector2.zero, new Vector2(40f, 34f), MakeArrowSprite(), cursorColor).rectTransform;

        ApplyTexts();
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
        var rt = NewRect(n, parent, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sprite && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    TextMeshProUGUI NewText(string n, Transform parent, Vector2 pos, Vector2 size, float fontSize)
    {
        var rt = NewRect(n, parent, pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.fontSize = fontSize;
        t.color = buttonTextColor;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    // =========================================================================
    // 画像の自動生成
    // =========================================================================

    /// <summary>上下グラデーション＋光るふちのパネル（9スライス）</summary>
    static Sprite MakePanelSprite(Color top, Color bottom, Color border)
    {
        const int s = 64, b = 16;
        var tex = NewTex(s, s);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Mathf.Min(Mathf.Min(x, s - 1 - x), Mathf.Min(y, s - 1 - y));   // ふちからの距離
            Color c = Color.Lerp(bottom, top, y / (float)(s - 1));
            if (d < 3f) c = border;                                                  // 外側の線
            else if (d < 4f) c = Color.Lerp(border, c, 0.5f);
            else if (d >= 7f && d < 8f) c = Color.Lerp(c, border, 0.55f);            // 内側の細い線
            else if (d < 12f) c = Color.Lerp(c, border, 0.25f * (12f - d) / 8f);     // ふちの近くを少し明るく
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>パネルの外側のぼんやりした光（9スライス）</summary>
    static Sprite MakeGlowSprite()
    {
        const int s = 64, b = 30;
        var tex = NewTex(s, s);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Mathf.Min(Mathf.Min(x, s - 1 - x), Mathf.Min(y, s - 1 - y));
            float a = Mathf.Clamp01(d / 30f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    /// <summary>両端が丸いボタン（9スライス）</summary>
    static Sprite MakePillSprite()
    {
        const int w = 64, h = 64, r = 32;
        var tex = NewTex(w, h);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x < r ? r - x - 0.5f : (x >= w - r ? x - (w - r) + 0.5f : 0f);
            float dy = y + 0.5f - h * 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(r - dist);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, 0, r, 0));
    }

    /// <summary>両端が丸い白い枠（選ばれているボタン用・9スライス）</summary>
    static Sprite MakePillOutlineSprite()
    {
        const int w = 64, h = 64, r = 32;
        var tex = NewTex(w, h);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = x < r ? r - x - 0.5f : (x >= w - r ? x - (w - r) + 0.5f : 0f);
            float dy = y + 0.5f - h * 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(r - dist) * Mathf.Clamp01(dist - (r - 4f));   // 太さ 4px の線
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, 0, r, 0));
    }

    /// <summary>右向きの矢印</summary>
    static Sprite MakeArrowSprite()
    {
        const int w = 40, h = 34;
        var tex = NewTex(w, h);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float half = (h * 0.5f) * (1f - x / (float)w);   // 右に行くほど細い三角形
            float dy = Mathf.Abs(y + 0.5f - h * 0.5f);
            float a = Mathf.Clamp01(half - dy + 0.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>小さなひし形（パネルの四隅の飾り）</summary>
    static Sprite MakeDiamondSprite()
    {
        const int s = 32;
        var tex = NewTex(s, s);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Mathf.Abs(x + 0.5f - s * 0.5f) + Mathf.Abs(y + 0.5f - s * 0.5f);
            float a = Mathf.Clamp01(s * 0.5f - d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    static Texture2D NewTex(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
