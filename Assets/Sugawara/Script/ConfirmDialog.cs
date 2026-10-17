using System;
using UnityEngine;
using TMPro;

// 「はい／いいえ」の確認ダイアログ
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] TMP_Text messageText;
    [SerializeField] RectTransform cursor;
    [SerializeField] RectTransform yesItem;
    [SerializeField] RectTransform noItem;

    bool yesSelected;
    Action onYes;

    public bool IsOpen => root.activeSelf;

    void Awake() => root.SetActive(false);

    public void Open(string message, Action yesAction)
    {
        messageText.text = message;
        onYes = yesAction;
        yesSelected = false; // 誤操作防止で初期は「いいえ」
        root.SetActive(true);
        UpdateCursor();
    }

    public void Toggle() { yesSelected = !yesSelected; UpdateCursor(); }

    public void Decide()
    {
        root.SetActive(false);
        if (yesSelected) onYes?.Invoke();
    }

    public void Cancel() => root.SetActive(false);

    void UpdateCursor()
    {
        RectTransform t = yesSelected ? yesItem : noItem;
        Vector3 p = cursor.position;
        p.y = t.position.y;
        cursor.position = p;
    }
}
