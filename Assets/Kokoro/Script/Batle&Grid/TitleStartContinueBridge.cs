
using UnityEngine;

/// <summary>
/// 第18段階・前半。
///
/// タイトルの「始めから」と
/// 「コンテニュー」の選択状態を整理する。
///
/// 既存のTitle_ButtonActionや
/// SaveDataSelectは変更しない。
///
/// シーン遷移もこのスクリプトでは行わない。
/// </summary>
public sealed class TitleStartContinueBridge : MonoBehaviour
{
    [Header("デバッグ")]
    [SerializeField]
    private bool showLogs = true;

    // ========================================
    // タイトルを開いたとき
    // ========================================

    private void Awake()
    {
        // 以前のContinue要求が残っていた場合、
        // 新しくタイトルを開いた時点で解除する。
        PendingBattleContinueRequest.Clear();
    }

    // ========================================
    // 「始めから」
    // ========================================

    public void OnNewGameClicked()
    {
        // 新規ゲームなので、
        // 過去のセーブを復元する要求は消す。
        PendingBattleContinueRequest.Clear();

        if (showLogs)
        {
            Debug.Log(
                "【新規ゲーム開始】\n" +
                "Continue要求を解除しました。\n" +
                "既存のMAP選択への遷移処理を使用します。",
                this
            );
        }

        // ここではSceneManagerを呼ばない。
        // MAP選択への移動は既存のタイトル処理。
    }

    // ========================================
    // 「コンテニュー」
    // ========================================

    public void OnContinueClicked()
    {
        // 新しくセーブデータを選び直す。
        PendingBattleContinueRequest.Clear();

        if (showLogs)
        {
            Debug.Log(
                "【コンテニュー選択】\n" +
                "セーブスロットの選択を開始します。",
                this
            );
        }

        // SaveDataSelectを開く処理は
        // 既存のタイトル側に任せる。
    }

    // ========================================
    // セーブ選択をキャンセル
    // ========================================

    public void OnContinueCancelled()
    {
        PendingBattleContinueRequest.Clear();

        if (showLogs)
        {
            Debug.Log(
                "【コンテニューキャンセル】\n" +
                "選択情報を解除しました。",
                this
            );
        }

        // 画面を閉じる処理は
        // 既存のSaveDataSelectが担当。
    }

    // ========================================
    // 選択状態の確認（開発用）
    // ========================================

    public void DebugCurrentRequest()
    {
        if (PendingBattleContinueRequest.TryPeek(
            out int slotNumber,
            out string stageId))
        {
            Debug.Log(
                "【現在のContinue要求】" +
                $"\nスロット：{slotNumber}" +
                $"\nステージ：{stageId}",
                this
            );
        }
        else
        {
            Debug.Log(
                "【現在のContinue要求】なし",
                this
            );
        }
    }
}
