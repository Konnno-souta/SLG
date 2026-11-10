using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 作) Claudeちゃん
/// 本をパラパラ〜っと捲る演出 (uGUI)。
/// このコンポーネントを付けた RectTransform が「見開き全体」。中央が背表紙(スパイン)。
/// ページは X スケールを 1 ⇔ -1 に動かして捲りを表現する。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PageFlipEffect : MonoBehaviour
{
    public enum FlipDirection
    {
        ToLeft,
        ToRight,
    }

    [Header("Defaults")]
    [SerializeField] FlipDirection direction = FlipDirection.ToLeft;
    [Min(1)] [SerializeField] int pageCount = 8;
    [Tooltip("1枚が捲り終わるまでの秒数")][Min(0.01f)] [SerializeField] float flipDuration = 0.35f;
    [Tooltip("次のページが捲り始めるまでの間隔(秒)。flipDuration より短いと重なる")][Min(0f)] [SerializeField] float interval = 0.06f;
    [SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Page Look")]
    [SerializeField] Sprite[] frontSprites;   // 表面。複数ならランダム
    [SerializeField] Sprite[] backSprites;    // 裏面。空なら表面を流用
    [SerializeField] Color pageColor = Color.white;
    [Tooltip("捲り途中の暗さ(0=変化なし)")][Range(0f, 1f)] [SerializeField] float shadeAmount = 0.35f;
    [Tooltip("捲り途中で縦方向にふくらむ量(0=なし)")][Range(0f, 0.2f)] [SerializeField] float bulge = 0.04f;

    [Header("Events")]
    public UnityEvent onStart;
    public UnityEvent onComplete;

    RectTransform rect;
    Coroutine running;
    readonly List<GameObject> spawned = new List<GameObject>();

    public bool IsPlaying => running != null;

    void Awake() => rect = (RectTransform)transform;

    /// <summary>インスペクタ設定のまま再生</summary>
    [ContextMenu("Play")]
    public void Play() => Play(direction, pageCount, flipDuration, interval);

    /// <summary>方向・枚数・速度を指定して再生。duration=1枚あたり秒数、interval=開始間隔秒数</summary>
    public void Play(FlipDirection dir, int count, float duration, float interval, Action onFinished = null)
    {
        if (rect == null) rect = (RectTransform)transform;
        Stop();
        running = StartCoroutine(Run(dir, Mathf.Max(1, count), Mathf.Max(0.01f, duration), Mathf.Max(0f, interval), onFinished));
    }

    /// <summary>speed 倍率指定版(2なら2倍速)</summary>
    public void PlayWithSpeed(FlipDirection dir, int count, float speed)
    {
        speed = Mathf.Max(0.01f, speed);
        Play(dir, count, flipDuration / speed, interval / speed);
    }

    public void Stop()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        Cleanup();
    }

    IEnumerator Run(FlipDirection dir, int count, float duration, float gap, Action onFinished)
    {
        int finished = 0;
        for (int i = 0; i < count; i++)
        {
            var page = CreatePage();
            spawned.Add(page.root.gameObject);
            StartCoroutine(FlipOne(page, dir, duration, () => finished++));
            if (i < count - 1 && gap > 0f) yield return new WaitForSeconds(gap);
        }

        while (finished < count) yield return null;

        Cleanup();
        running = null;
        onFinished?.Invoke();
        onComplete?.Invoke();
    }

    IEnumerator FlipOne(Page page, FlipDirection dir, float duration, Action done)
    {
        float from = dir == FlipDirection.ToLeft ? 1f : -1f;
        float to = -from;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
        {
            if (page.root == null) yield break; // Stop 済み
            Apply(page, Mathf.Lerp(from, to, ease.Evaluate(t)));
            yield return null;
        }

        if (page.root != null) Apply(page, to);
        done();
    }

    void Apply(Page p, float x)
    {
        // 捲り途中(x=0付近)ほど暗く・縦にふくらませる
        float mid = 1f - Mathf.Abs(x);
        p.root.localScale = new Vector3(x, 1f + bulge * mid, 1f);

        // x>0 は右側にある=表面、x<0 は左側にある=裏面(を見ている)
        bool showFront = x > 0f;
        p.front.gameObject.SetActive(showFront);
        p.back.gameObject.SetActive(!showFront);

        float shade = 1f - shadeAmount * mid;
        var c = new Color(pageColor.r * shade, pageColor.g * shade, pageColor.b * shade, pageColor.a);
        p.front.color = c;
        p.back.color = c;
    }

    struct Page
    {
        public RectTransform root;
        public Image front, back;
    }

    Page CreatePage()
    {
        var go = new GameObject("Page", typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(rect, false);

        // 中央を軸に、右半分を覆うサイズ。pivot を左端(=背表紙)にする
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(rect.rect.width * 0.5f, rect.rect.height);
        r.anchoredPosition = Vector2.zero;

        var front = CreateFace(r, "Front", Pick(frontSprites), false);
        var backSprite = backSprites != null && backSprites.Length > 0 ? Pick(backSprites) : Pick(frontSprites);
        // 親が -1 スケールで反転して見えるので、裏面は子側でさらに反転して正像に戻す
        var back = CreateFace(r, "Back", backSprite, true);

        var page = new Page { root = r, front = front, back = back };
        return page;
    }

    Image CreateFace(RectTransform parent, string name, Sprite sprite, bool mirror)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        if (mirror) r.localScale = new Vector3(-1f, 1f, 1f);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = pageColor;
        img.raycastTarget = false;
        return img;
    }

    static Sprite Pick(Sprite[] arr) =>
        arr == null || arr.Length == 0 ? null : arr[UnityEngine.Random.Range(0, arr.Length)];

    void Cleanup()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }

    void OnDisable() => Stop();
}
