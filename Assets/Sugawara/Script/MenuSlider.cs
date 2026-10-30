using System.Collections;
using UnityEngine;

// MenuPanel に付ける。横からスライドして出入りする
// 隠す位置はパネルの幅から自動計算するので、ボタンが増えて幅が変わっても調整不要
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class MenuSlider : MonoBehaviour
{
    [SerializeField] float duration = 0.25f;  // スライドにかかる秒数
    [SerializeField] float hideMargin = 50f;  // 画面外へどれだけ余分に出すか
    [SerializeField] bool fromLeft = true;    // false にすると右から出る
    [SerializeField] bool debugLog = true;    // 原因が分かったらオフにする

    RectTransform rect;
    CanvasGroup group;
    Vector2 shownPos;   // 配置した位置 = 表示位置
    Coroutine running;

    public bool IsShown { get; private set; }

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        shownPos = rect.anchoredPosition;

        Log($"Awake 表示位置を記録: {shownPos} / 幅: {rect.rect.width}");

        // 最初は隠した状態
        rect.anchoredPosition = HiddenPos();
        SetInteractable(false);
    }

    [ContextMenu("Show（テスト）")]
    public void Show() => Play(true);

    [ContextMenu("Hide（テスト）")]
    public void Hide() => Play(false);

    void Play(bool show)
    {
        Log($"{(show ? "Show" : "Hide")} 呼ばれた → 目標: {(show ? shownPos : HiddenPos())}");
        IsShown = show;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Slide(show ? shownPos : HiddenPos(), show));
    }

    IEnumerator Slide(Vector2 target, bool show)
    {
        if (!show) SetInteractable(false); // 隠れ始めたらすぐ押せなくする

        Vector2 start = rect.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // 最後にふわっと止まる
            rect.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
            yield return null;
        }
        rect.anchoredPosition = target;

        if (show) SetInteractable(true); // 出きってから押せるようにする
        running = null;

        Log($"スライド完了: {rect.anchoredPosition}");
        if (show) StartCoroutine(CheckOverwritten(target));
    }

    // 1秒後も位置が保たれているか（別のスクリプトに書き換えられていないか）を確認
    IEnumerator CheckOverwritten(Vector2 target)
    {
        yield return new WaitForSecondsRealtime(1f);
        if ((rect.anchoredPosition - target).sqrMagnitude > 1f)
            Debug.LogWarning($"[MenuSlider] 位置が書き換えられています！ 目標:{target} 現在:{rect.anchoredPosition}。MenuPanel を動かしている別のスクリプト（MenuListWindow など）を探してください");
        else
            Log("1秒後も位置は保たれています");
    }

    Vector2 HiddenPos()
    {
        float offset = rect.rect.width + hideMargin;
        return shownPos + new Vector2(fromLeft ? -offset : offset, 0f);
    }

    void SetInteractable(bool on)
    {
        group.interactable = on;
        group.blocksRaycasts = on;
    }

    void Log(string msg)
    {
        if (debugLog) Debug.Log($"[MenuSlider] {msg}", this);
    }
}