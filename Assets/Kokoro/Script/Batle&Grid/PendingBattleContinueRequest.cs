
using UnityEngine;

/// <summary>
/// タイトルで選択したContinue情報を保持する。
///
/// 現時点ではシーン遷移を実行しない。
///
/// 使い方：
/// 1. タイトルのセーブスロットを選択
/// 2. SetRequest()で番号とステージIDを保持
/// 3. 将来のシーン遷移処理がTryPeek()で取得
/// 4. ロード成功後にClear()する
///
/// staticのため同じゲーム実行中は
/// シーンが切り替わっても情報を保持できる。
///
/// ただし、アプリを終了したら消える。
/// 永続保存は既存のJSONが担当する。
/// </summary>
public static class PendingBattleContinueRequest
{
    public static bool HasPendingRequest
    {
        get;
        private set;
    }

    public static int SlotNumber
    {
        get;
        private set;
    }

    public static string StageId
    {
        get;
        private set;
    } = "";

    // ========================================
    // Play開始時の初期化
    // ========================================

    /// <summary>
    /// Unity EditorでDomain Reloadを
    /// 無効にしていても、前のPlayの
    /// 選択情報を残さないようにする。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        Clear();
    }

    // ========================================
    // Continue要求の登録
    // ========================================

    public static bool SetRequest(
        int slotNumber,
        string stageId)
    {
        if (slotNumber < 1 ||
            string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogError(
                "Continue要求のスロット番号または" +
                "ステージIDが不正です。"
            );

            return false;
        }

        SlotNumber = slotNumber;
        StageId = stageId;
        HasPendingRequest = true;

        Debug.Log(
            "【Continue要求を登録】" +
            $"\nスロット：{SlotNumber}" +
            $"\nステージ：{StageId}"
        );

        return true;
    }

    // ========================================
    // Continue要求を取得
    // ========================================

    /// <summary>
    /// 登録中のロード要求を取得。
    ///
    /// 読み取るだけで、要求は消さない。
    /// ロード成功後にClear()で解除する。
    /// </summary>
    public static bool TryPeek(
        out int slotNumber,
        out string stageId)
    {
        slotNumber = SlotNumber;
        stageId = StageId;

        return HasPendingRequest;
    }

    // ========================================
    // Continue要求を解除
    // ========================================

    public static void Clear()
    {
        SlotNumber = 0;
        StageId = "";
        HasPendingRequest = false;
    }
}
