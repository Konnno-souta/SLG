
using UnityEngine;

/// <summary>
/// 「始めから」で選択した
/// 新規ゲームの保存先スロットを保持する。
///
/// 実際のJSON保存処理は
/// BattleSaveManagerが担当する。
///
/// このクラスはスロットの選択状態だけを
/// シーンをまたいで保持する。
/// </summary>
public static class PendingNewGameSlotRequest
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

    public static bool OverwriteConfirmed
    {
        get;
        private set;
    }

    // ========================================
    // Play開始時の初期化
    // ========================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        Clear();
    }

    // ========================================
    // 新規ゲーム要求の登録
    // ========================================

    public static bool SetRequest(
        int slotNumber,
        bool overwriteConfirmed)
    {
        if (slotNumber < 1)
        {
            Debug.LogError(
                "新規ゲームのスロット番号が不正です。"
            );

            return false;
        }

        SlotNumber = slotNumber;
        OverwriteConfirmed = overwriteConfirmed;
        HasPendingRequest = true;

        Debug.Log(
            "【新規ゲーム保存先決定】" +
            $"\nスロット：{SlotNumber}" +
            $"\n上書き確認済み：{OverwriteConfirmed}"
        );

        return true;
    }

    // ========================================
    // 取得
    // ========================================

    public static bool TryPeek(
        out int slotNumber,
        out bool overwriteConfirmed)
    {
        slotNumber = SlotNumber;
        overwriteConfirmed = OverwriteConfirmed;

        return HasPendingRequest;
    }

    // ========================================
    // 解除
    // ========================================

    public static void Clear()
    {
        SlotNumber = 0;
        OverwriteConfirmed = false;
        HasPendingRequest = false;
    }
}
