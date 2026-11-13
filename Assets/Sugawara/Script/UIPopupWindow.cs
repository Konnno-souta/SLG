using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ボタンで開閉するウィンドウ（設定・仲間・セーブなど、どのウィンドウにも使える）
//
// ・開く：ボタンの OnClick に Open() を登録
// ・閉じる：閉じるボタンの OnClick に Close() を登録 / Esc キー / コントローラーの B ボタン
// ・開いたら First Selected を選択し、閉じたら開く前に選んでいたボタンに選択を戻す
//   （UISelectCursor を使っていれば、カーソルもウィンドウとメニューを自然に行き来する）
// ・開いている間は UIPopupWindow.AnyOpen が true になる（後ろの操作を止めるのに使う）
[RequireComponent(typeof(CanvasGroup))]
public class UIPopupWindow : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("ポンっと拡大して出す部分（ウィンドウ本体）。空ならこのオブジェクト")]
    [SerializeField] RectTransform content;
    [SerializeField] float duration = 0.15f;
    [Tooltip("開き始めの大きさ")]
    [SerializeField, Range(0.5f, 1f)] float startScale = 0.85f;

    [Header("操作")]
    [Tooltip("開いたときに最初に選択するボタン")]
    [SerializeField] Selectable firstSelected;
    [Tooltip("Esc キー / コントローラーの B ボタンで閉じる")]
    [SerializeField] bool closeOnCancel = true;

    // 開いているウィンドウの数
    public static int OpenCount { get; private set; }
    public static bool AnyOpen => OpenCount > 0;

    public bool IsOpen { get; private set; }

    CanvasGroup group;
    GameObject previousSelected;
    Coroutine running;
    bool opening;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (content == null) content = (RectTransform)transform;

        // 最初は閉じた状態（Open() で有効化されたときは閉じない）
        if (!opening) gameObject.SetActive(false);
    }

    void Update()
    {
        if (!IsOpen || !closeOnCancel) return;

        var k = Keyboard.current;
        var g = Gamepad.current;
        bool cancel = (k != null && k.escapeKey.wasPressedThisFrame)
                   || (g != null && g.buttonEast.wasPressedThisFrame);
        if (cancel) Close();
    }

    public void Open()
    {
        if (IsOpen) return;

        IsOpen = true;
        OpenCount++;

        // 開く前に選んでいたボタンを覚えておく（閉じたときに戻す）
        previousSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        opening = true;
        gameObject.SetActive(true);
        opening = false;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Animate(true));
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        OpenCount--;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Animate(false));
    }

    IEnumerator Animate(bool open)
    {
        group.interactable = false;
        group.blocksRaycasts = true;

        float fromA = open ? 0f : 1f, toA = open ? 1f : 0f;
        float fromS = open ? startScale : 1f, toS = open ? 1f : startScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            group.alpha = Mathf.Lerp(fromA, toA, e);
            content.localScale = Vector3.one * Mathf.Lerp(fromS, toS, e);
            yield return null;
        }
        group.alpha = toA;
        content.localScale = Vector3.one * toS;

        if (open)
        {
            group.interactable = true;
            if (EventSystem.current != null && firstSelected != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }
        else
        {
            // 開く前のボタンに選択を戻してから非表示にする
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelected);
            gameObject.SetActive(false);
        }
        running = null;
    }

    void OnDestroy()
    {
        // 開いたままシーンを抜けたときにカウントが残らないようにする
        if (IsOpen) OpenCount--;
    }
}
