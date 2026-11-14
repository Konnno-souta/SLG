using UnityEngine;
using UnityEngine.Events;

// ボタンを押したら確認ダイアログを出し、「はい」なら指定した処理を実行する
// スクリプトを書かずに、Inspector だけで「確認してから〇〇する」ボタンが作れる
//
// 使い方
//   1. ボタンにこれを付ける
//   2. ボタンの OnClick に Ask() を登録する
//   3. On Yes に「はい」のときに実行したい処理（セーブなど）を登録する
public class UIConfirmAction : MonoBehaviour
{
    [Tooltip("使う確認ダイアログ（DiaLogPanel）")]
    [SerializeField] UIConfirmDialog dialog;
    [Tooltip("ダイアログに出すメッセージ")]
    [SerializeField, TextArea(1, 3)] string message = "セーブしますか？";

    [Tooltip("「はい」を押したときに実行する処理")]
    public UnityEvent onYes = new UnityEvent();
    [Tooltip("「いいえ」を押したときに実行する処理（なくてもOK）")]
    public UnityEvent onNo = new UnityEvent();

    public void Ask()
    {
        if (dialog == null)
        {
            Debug.LogWarning($"[UIConfirmAction] {name} の Dialog が設定されていません", this);
            return;
        }
        dialog.Open(message, () => onYes.Invoke(), () => onNo.Invoke());
    }
}
