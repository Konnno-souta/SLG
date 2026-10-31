using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 「GAME OVER」の文字の演出（TextMeshPro）。
///   出てくる : 1文字ずつ、縦に長く伸びた半透明の状態から、元の大きさに縮みながらはっきり出てくる（左から順番に）
///   表示中   : そのまま → 少し待ってから上へ移動（Move Up）。移動し終わると On Moved が呼ばれる（メニューを出す）
///   消える   : 一瞬光る → 縦に伸びながら薄くなって消える
/// TextMeshPro の文字（GameOverText を付けた物、または Text 欄に入れた物）の頂点を動かして演出する。
/// </summary>
[DisallowMultipleComponent]
public class GameOverText : MonoBehaviour
{
    public enum State { Hidden, Intro, Showing, Moving, Outro }

    [Header("文字")]
    [Tooltip("演出する TextMeshPro。空なら同じオブジェクトのものを使う")]
    public TMP_Text text;

    [Header("再生")]
    [Tooltip("オブジェクトが表示された時に自動で再生する")]
    public bool playOnEnable = true;
    [Tooltip("ゲームを止めて（Time.timeScale = 0）いても動くようにする")]
    public bool useUnscaledTime = true;

    [Header("出てくる演出")]
    [Tooltip("再生してから最初の文字が出始めるまで（秒）")]
    public float introDelay = 0f;
    [Tooltip("1文字が出きるまでの時間（秒）")]
    public float charDuration = 0.5f;
    [Tooltip("次の文字が出始めるまでのずれ（秒）。0 で全部同時")]
    public float charStagger = 0.05f;
    [Tooltip("出始めの縦の伸び（倍）")]
    public float introStretchY = 4f;
    [Tooltip("出始めの横の大きさ（倍）。1 より小さいと細長くなる")]
    public float introScaleX = 0.9f;
    [Tooltip("1文字の時間のうち、何割ではっきり見えるようになるか")]
    [Range(0.05f, 1f)] public float introFadePortion = 0.6f;

    [Header("出た後に上へ移動")]
    [Tooltip("出きった後、そのまま少し待ってから上へ移動する")]
    public bool moveUp = true;
    [Tooltip("出きってから移動し始めるまで待つ時間（秒）")]
    public float moveDelay = 1f;
    [Tooltip("上へ移動する距離 (px)")]
    public float moveDistance = 300f;
    [Tooltip("移動にかける時間（秒）")]
    public float moveTime = 0.5f;

    [Header("自動で消す")]
    [Tooltip("出きった後（移動する時は移動し終わった後）、自動で消す")]
    public bool autoHide = false;
    [Tooltip("自動で消すまでの時間（秒）")]
    public float holdTime = 5f;

    [Header("消える演出")]
    [Tooltip("消える前に一瞬光る時間（秒）")]
    public float flashTime = 0.08f;
    [Tooltip("光る強さ")]
    [Range(0f, 1f)] public float flashAlpha = 0.8f;
    [Tooltip("縦に伸びながら消える時間（秒）")]
    public float outroTime = 0.2f;
    [Tooltip("消える時の文字の縦の伸び（倍）")]
    public float outroStretchY = 1.3f;

    [Header("残像（後ろに重なる、伸びた薄い文字）")]
    public bool ghost = true;
    [Tooltip("残像の色")]
    public Color ghostColor = new Color(1f, 0.45f, 0.75f, 1f);
    [Tooltip("残像の濃さ")]
    [Range(0f, 1f)] public float ghostAlpha = 0.45f;
    [Tooltip("出てくる時、残像が文字より何秒遅れるか")]
    public float ghostLag = 0.06f;
    [Tooltip("出てくる時の残像の縦の伸び（文字の伸びに対する倍率）")]
    public float ghostIntroStretch = 1.3f;
    [Tooltip("消える時の残像の縦の伸び（倍）")]
    public float ghostOutroStretchY = 3f;

    [Header("伸びる中心")]
    [Tooltip("文字が縦に伸びる時の中心。0: 文字の下 / 0.5: 真ん中 / 1: 文字の上")]
    [Range(0f, 1f)] public float pivotY = 0.5f;

    [Header("終わった後")]
    [Tooltip("消え終わったら非表示にするオブジェクト（ゲームオーバーのパネルなど）。空なら何もしない")]
    public GameObject hideOnFinish;

    [Header("イベント")]
    [Tooltip("文字が出きった時")]
    public UnityEvent onShown;
    [Tooltip("上へ移動し終わった時（ゲームオーバーのメニューを出すなど）")]
    public UnityEvent onMoved;
    [Tooltip("文字が消え終わった時")]
    public UnityEvent onFinished;

    /// <summary>今の状態</summary>
    public State CurrentState { get; private set; } = State.Hidden;
    public bool IsPlaying => CurrentState != State.Hidden;

    // ---- 内部 ----
    class Cache
    {
        public TMP_Text text;
        public Vector3[][] vertices;
        public Color32[][] colors;
        public bool ready;
    }

    readonly Cache mainCache = new Cache();
    readonly Cache ghostCache = new Cache();
    readonly List<int> visibleChars = new List<int>();
    TextMeshProUGUI ghostText;
    float timer;
    bool drawing;
    bool moved;
    bool hasBasePos;
    Vector2 basePos, ghostBasePos;

    float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // =========================================================================
    // 公開API
    // =========================================================================

    /// <summary>最初から再生する</summary>
    [ContextMenu("再生（Play）")]
    public void Play()
    {
        if (!text) { Debug.LogWarning("[GameOverText] TextMeshPro が見つかりません", this); return; }
        if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        if (hideOnFinish && !hideOnFinish.activeSelf) hideOnFinish.SetActive(true);
        EnsureGhost();
        RebuildAll();
        SetMoveOffset(0f);   // 元の位置に戻す
        moved = false;
        timer = 0f;
        CurrentState = State.Intro;
        Draw();
    }

    /// <summary>消える演出を始める（出てくる途中でも OK）</summary>
    [ContextMenu("消す（Hide）")]
    public void Hide()
    {
        if (CurrentState == State.Hidden || CurrentState == State.Outro) return;
        timer = 0f;
        CurrentState = State.Outro;
        Draw();
    }

    /// <summary>演出なしで、すぐに消す</summary>
    public void HideImmediately()
    {
        CurrentState = State.Hidden;
        Draw();
    }

    // =========================================================================
    // Unity
    // =========================================================================

    void Reset() => text = GetComponent<TMP_Text>();

    void Awake()
    {
        if (!text) text = GetComponent<TMP_Text>();
        if (!text) text = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        if (playOnEnable) Play();
        else { CurrentState = State.Hidden; RebuildAll(); Draw(); }
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        if (ghostText) ghostText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (CurrentState == State.Hidden) return;
        timer += Dt;

        switch (CurrentState)
        {
            case State.Intro:
                if (timer >= IntroLength())
                {
                    timer = 0f;
                    CurrentState = State.Showing;
                    onShown?.Invoke();
                }
                break;

            case State.Showing:
                if (moveUp && !moved)
                {
                    if (timer >= moveDelay)
                    {
                        timer = 0f;
                        CurrentState = State.Moving;
                    }
                }
                else if (autoHide && timer >= holdTime) Hide();
                break;

            case State.Moving:
                {
                    float k = EaseOutCubic(timer / Mathf.Max(0.01f, moveTime));
                    SetMoveOffset(moveDistance * k);
                    if (timer >= moveTime)
                    {
                        SetMoveOffset(moveDistance);
                        moved = true;
                        timer = 0f;
                        CurrentState = State.Showing;
                        onMoved?.Invoke();
                    }
                    break;
                }

            case State.Outro:
                if (timer >= flashTime + outroTime)
                {
                    CurrentState = State.Hidden;
                    Draw();
                    if (hideOnFinish) hideOnFinish.SetActive(false);
                    onFinished?.Invoke();
                    return;
                }
                break;
        }
    }

    void LateUpdate()
    {
        // 文字が書き換えられていたら残像も合わせる
        if (ghostText && text && ghostText.text != text.text) ghostText.text = text.text;
        Draw();
    }

    /// <summary>TextMeshPro が文字を作り直した時（解像度の変更など）。元の形を覚え直して描き直す</summary>
    void OnTextChanged(Object obj)
    {
        if (drawing) return;
        if (obj == text) { Capture(mainCache); CollectVisible(); Draw(); }
        else if (ghostText && obj == ghostText) { Capture(ghostCache); Draw(); }
    }

    // =========================================================================
    // 描画
    // =========================================================================

    float IntroLength()
    {
        int n = Mathf.Max(1, visibleChars.Count);
        return introDelay + (n - 1) * charStagger + charDuration + (ghost ? ghostLag : 0f);
    }

    void Draw()
    {
        if (!mainCache.ready) return;
        drawing = true;

        for (int k = 0; k < visibleChars.Count; k++)
        {
            int i = visibleChars[k];
            Pose(k, false, out float sx, out float sy, out float a, out float white);
            SetChar(mainCache, i, sx, sy, a, white, null);

            if (ghostCache.ready)
            {
                Pose(k, true, out sx, out sy, out a, out white);
                SetChar(ghostCache, i, sx, sy, a, white, ghostColor);
            }
        }

        Push(mainCache);
        if (ghostCache.ready) Push(ghostCache);
        drawing = false;
    }

    /// <summary>k 番目の文字の、今の横の大きさ・縦の伸び・濃さ・白さ</summary>
    void Pose(int k, bool isGhost, out float sx, out float sy, out float a, out float white)
    {
        sx = 1f; sy = 1f; a = 1f; white = 0f;

        switch (CurrentState)
        {
            case State.Hidden:
                a = 0f;
                break;

            case State.Intro:
                {
                    float start = introDelay + k * charStagger + (isGhost ? ghostLag : 0f);
                    float p = Mathf.Clamp01((timer - start) / Mathf.Max(0.01f, charDuration));
                    float e = EaseOutCubic(p);
                    float stretch = isGhost ? introStretchY * ghostIntroStretch : introStretchY;
                    sy = Mathf.Lerp(stretch, 1f, e);
                    sx = Mathf.Lerp(introScaleX, 1f, e);
                    float fadeIn = Mathf.Clamp01(p / introFadePortion);
                    if (p <= 0f) fadeIn = 0f;
                    // 残像は伸びている間だけ見えて、縮みきると消える
                    a = isGhost ? ghostAlpha * fadeIn * (1f - e) : fadeIn;
                    break;
                }

            case State.Showing:
            case State.Moving:
                a = isGhost ? 0f : 1f;
                break;

            case State.Outro:
                if (timer < flashTime)
                {
                    // 一瞬光る（残像が白く、少し大きく重なる）
                    float p = Mathf.Clamp01(timer / Mathf.Max(0.01f, flashTime));
                    if (isGhost)
                    {
                        sx = Mathf.Lerp(1f, 1.03f, p);
                        sy = Mathf.Lerp(1f, 1.1f, p);
                        a = flashAlpha * Mathf.Sin(p * Mathf.PI * 0.5f);
                        white = 1f;
                    }
                }
                else
                {
                    // 縦に伸びながら薄くなって消える
                    float p = Mathf.Clamp01((timer - flashTime) / Mathf.Max(0.01f, outroTime));
                    float e = EaseOutCubic(p);
                    if (isGhost)
                    {
                        sx = 1.03f;
                        sy = Mathf.Lerp(1.1f, ghostOutroStretchY, e);
                        a = Mathf.Lerp(flashAlpha, 0f, e);
                        white = 1f - p;
                    }
                    else
                    {
                        sy = Mathf.Lerp(1f, outroStretchY, e);
                        a = 1f - p * p;
                    }
                }
                break;
        }

        if (isGhost && !ghost) a = 0f;
    }

    /// <summary>1文字の4つの頂点を、元の形から 伸ばす・縮める・色を変える</summary>
    void SetChar(Cache c, int index, float sx, float sy, float alpha, float white, Color? tint)
    {
        var info = c.text.textInfo;
        if (index >= info.characterCount) return;
        var ch = info.characterInfo[index];
        if (!ch.isVisible) return;

        int m = ch.materialReferenceIndex;
        int v = ch.vertexIndex;
        if (m >= c.vertices.Length || c.vertices[m] == null || v + 3 >= c.vertices[m].Length) return;

        var src = c.vertices[m];
        var srcCol = c.colors[m];
        var dst = info.meshInfo[m].vertices;
        var dstCol = info.meshInfo[m].colors32;
        if (dst == null || v + 3 >= dst.Length) return;

        // 伸びる中心：横は文字の真ん中、縦は行の高さの pivotY の位置（全部の文字でそろえる）
        var line = info.lineInfo[ch.lineNumber];
        float cx = (src[v].x + src[v + 2].x) * 0.5f;
        float cy = Mathf.Lerp(line.descender, line.ascender, pivotY);

        for (int j = 0; j < 4; j++)
        {
            Vector3 p = src[v + j];
            p.x = cx + (p.x - cx) * sx;
            p.y = cy + (p.y - cy) * sy;
            dst[v + j] = p;

            Color col = srcCol[v + j];
            if (tint.HasValue)
            {
                Color t = tint.Value;
                col = new Color(t.r, t.g, t.b, col.a * t.a);
            }
            if (white > 0f) col = Color.Lerp(col, new Color(1f, 1f, 1f, col.a), white);
            col.a *= Mathf.Clamp01(alpha);
            dstCol[v + j] = col;
        }
    }

    void Push(Cache c)
    {
        c.text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    // =========================================================================
    // 準備
    // =========================================================================

    void RebuildAll()
    {
        if (!text) return;
        mainCache.text = text;
        ghostCache.text = ghostText;
        drawing = true;
        text.ForceMeshUpdate();
        Capture(mainCache);
        if (ghostText)
        {
            ghostText.gameObject.SetActive(ghost);
            if (ghost)
            {
                ghostText.text = text.text;
                ghostText.ForceMeshUpdate();
                Capture(ghostCache);
            }
            else ghostCache.ready = false;
        }
        drawing = false;
        CollectVisible();
    }

    /// <summary>今の文字の形と色を「元の形」として覚える</summary>
    static void Capture(Cache c)
    {
        if (c.text == null) { c.ready = false; return; }
        var info = c.text.textInfo;
        int count = info.meshInfo.Length;
        c.vertices = new Vector3[count][];
        c.colors = new Color32[count][];
        for (int m = 0; m < count; m++)
        {
            var mi = info.meshInfo[m];
            c.vertices[m] = mi.vertices != null ? (Vector3[])mi.vertices.Clone() : null;
            c.colors[m] = mi.colors32 != null ? (Color32[])mi.colors32.Clone() : null;
        }
        c.ready = true;
    }

    void CollectVisible()
    {
        visibleChars.Clear();
        if (!text) return;
        var info = text.textInfo;
        for (int i = 0; i < info.characterCount; i++)
            if (info.characterInfo[i].isVisible) visibleChars.Add(i);
    }

    /// <summary>残像用に、同じ見た目の文字をもう1つ作って後ろに置く（UI の TextMeshPro の時だけ）</summary>
    void EnsureGhost()
    {
        mainCache.text = text;
        if (!ghost || ghostText) { ghostCache.text = ghostText; return; }

        var src = text as TextMeshProUGUI;
        if (!src) return;   // 3D の TextMeshPro の時は残像なし

        var go = new GameObject(text.name + "_Ghost", typeof(RectTransform));
        go.layer = text.gameObject.layer;
        var rt = (RectTransform)go.transform;
        var srt = src.rectTransform;
        rt.SetParent(srt.parent, false);
        rt.SetSiblingIndex(srt.GetSiblingIndex());   // 文字の後ろ
        rt.anchorMin = srt.anchorMin;
        rt.anchorMax = srt.anchorMax;
        rt.pivot = srt.pivot;
        rt.anchoredPosition3D = srt.anchoredPosition3D;
        rt.sizeDelta = srt.sizeDelta;
        rt.localRotation = srt.localRotation;
        rt.localScale = srt.localScale;
        go.AddComponent<LayoutElement>().ignoreLayout = true;   // Layout Group の中でも位置がずれないように

        var g = go.AddComponent<TextMeshProUGUI>();
        g.font = src.font;
        g.fontSharedMaterial = src.fontSharedMaterial;
        g.fontSize = src.fontSize;
        g.enableAutoSizing = src.enableAutoSizing;
        g.fontSizeMin = src.fontSizeMin;
        g.fontSizeMax = src.fontSizeMax;
        g.fontStyle = src.fontStyle;
        g.alignment = src.alignment;
        g.characterSpacing = src.characterSpacing;
        g.wordSpacing = src.wordSpacing;
        g.lineSpacing = src.lineSpacing;
        g.textWrappingMode = src.textWrappingMode;
        g.overflowMode = src.overflowMode;
        g.margin = src.margin;
        g.color = src.color;
        g.raycastTarget = false;
        g.text = src.text;

        ghostText = g;
        ghostCache.text = g;
    }

    /// <summary>元の位置から上へ dy だけずらす（残像も一緒に）</summary>
    void SetMoveOffset(float dy)
    {
        if (!text) return;
        var rt = text.rectTransform;
        if (!hasBasePos)
        {
            basePos = rt.anchoredPosition;
            if (ghostText) ghostBasePos = ghostText.rectTransform.anchoredPosition;
            hasBasePos = true;
        }
        rt.anchoredPosition = basePos + new Vector2(0f, dy);
        if (ghostText) ghostText.rectTransform.anchoredPosition = ghostBasePos + new Vector2(0f, dy);
    }

    static float EaseOutCubic(float k) { k = Mathf.Clamp01(k); float u = 1f - k; return 1f - u * u * u; }
}