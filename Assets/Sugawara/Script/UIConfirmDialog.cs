using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 「はい／いいえ」の確認ダイアログ（DiaLogPanel に付ける）
// 開くたびに、メッセージと「はいを押したときの処理」を渡せるので、
// ステージ開始・セーブ・タイトルへ戻る など、どの確認にも同じパネルを使い回せる
//
// 使い方
//   スクリプトから：confirmDialog.Open("セーブしますか？", () => Save());
//   Inspector から：ボタンに UIConfirmAction を付ける（スクリプトを書かなくてよい）
//
// はいボタンの OnClick → Yes()、いいえボタンの OnClick → No() を登録しておく
public class UIConfirmDialog : MonoBehaviour
{
    [Tooltip("表示・非表示を切り替えるパネル（空ならこのオブジェクト）")]
    [SerializeField] GameObject root;
    [Tooltip("メッセージを表示する Text（空ならメッセージは変えない）")]
    [SerializeField] TMP_Text messageText;
    [Tooltip("開いたときに最初に選ぶボタン（「いいえ」がおすすめ）")]
    [SerializeField] Selectable firstSelected;
    [Tooltip("Esc キー / コントローラーの B ボタンで「いいえ」と同じ動きにする")]
    [SerializeField] bool cancelWithEscape = true;

    Action onYes;
    Action onNo;
    GameObject selectedBeforeOpen;

    GameObject Root => root != null ? root : gameObject;
    public bool IsOpen => Root.activeSelf;

    // ダイアログを開く
    public void Open(string message, Action yes, Action no = null)
    {
        onYes = yes;
        onNo = no;

        if (messageText != null && message != null) messageText.text = message;

        // 開く前に選んでいたボタンを覚えておく（閉じたときにカーソルを戻す）
        selectedBeforeOpen = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        Root.SetActive(true);

        if (EventSystem.current != null && firstSelected != null)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
    }

    // 「はい」ボタンの OnClick に登録する
    public void Yes()
    {
        Action callback = onYes;
        Close();
        callback?.Invoke();
    }

    // 「いいえ」ボタンの OnClick に登録する
    public void No()
    {
        Action callback = onNo;
        Close();
        callback?.Invoke();
    }

    void Close()
    {
        onYes = null;
        onNo = null;
        Root.SetActive(false);

        // 開く前のボタンに選択を戻す（カーソルが戻る）
        if (EventSystem.current != null && selectedBeforeOpen != null && selectedBeforeOpen.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(selectedBeforeOpen);
    }

    void Update()
    {
        if (!cancelWithEscape || !IsOpen) return;

        var k = Keyboard.current;
        var g = Gamepad.current;
        if ((k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame))
            No();
    }
}
