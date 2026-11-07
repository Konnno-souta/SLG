using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 画面を暗くする／明るくする
// 空の GameObject に付けるだけで、黒い板（Canvas と Image）を自動で作る
public class MapScreenFader : MonoBehaviour
{
    [Tooltip("フェードの色")]
    [SerializeField] Color color = Color.black;
    [Tooltip("描画の順番。会話のUIはこれより大きい Sort Order にすると、暗い画面の上に表示される")]
    [SerializeField] int sortingOrder = 100;
    [Tooltip("シーン開始時に、暗い状態から明るくする")]
    [SerializeField] bool fadeInOnStart = false;
    [SerializeField] float fadeInDuration = 0.5f;

    Image image;

    public bool IsFading { get; private set; }

    void Awake()
    {
        // フェード用の Canvas と Image を作る
        var canvasObj = new GameObject("FadeCanvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        image = imageObj.AddComponent<Image>();
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        SetAlpha(fadeInOnStart ? 1f : 0f);
    }

    void Start()
    {
        if (fadeInOnStart) StartCoroutine(FadeIn(fadeInDuration));
    }

    // だんだん暗くする
    public IEnumerator FadeOut(float duration) => Fade(1f, duration);

    // だんだん明るくする
    public IEnumerator FadeIn(float duration) => Fade(0f, duration);

    IEnumerator Fade(float targetAlpha, float duration)
    {
        IsFading = true;
        float start = image.color.a;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            SetAlpha(Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(t)));
            yield return null;
        }
        SetAlpha(targetAlpha);
        IsFading = false;
    }

    void SetAlpha(float a)
    {
        Color c = color;
        c.a = a;
        image.color = c;
        image.raycastTarget = a > 0f; // 暗い間は後ろのボタンを押せないようにする
    }
}