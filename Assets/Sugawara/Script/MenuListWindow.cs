using System.Collections.Generic;
using UnityEngine;

// 左のメニューリストウィンドウ（移動中は左へスライドして隠す）
public class MenuListWindow : MonoBehaviour
{
    [SerializeField] RectTransform panel;
    [SerializeField] float shownX = 40f;    // 表示時のX
    [SerializeField] float hiddenX = -500f; // 画面外のX
    [SerializeField] float slideSpeed = 10f;
    [SerializeField] RectTransform cursor;  // panelの子にしておくと一緒にスライドする
    [SerializeField] List<RectTransform> items; // 章開始, 仲間, セーブ, タイトルへ の順

    public int Index { get; private set; }
    public bool IsShown { get; private set; } = true;

    public void Show(bool show) => IsShown = show;

    public void MoveCursor(int delta)
    {
        Index = (Index + delta + items.Count) % items.Count;
        UpdateCursor();
    }

    void Start() => UpdateCursor();

    void Update()
    {
        Vector2 pos = panel.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, IsShown ? shownX : hiddenX, slideSpeed * Time.deltaTime);
        panel.anchoredPosition = pos;
    }

    void UpdateCursor()
    {
        Vector3 p = cursor.position;
        p.y = items[Index].position.y;
        cursor.position = p;
    }
}
