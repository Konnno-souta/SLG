
using UnityEngine;

/// <summary>
/// シーン間で引き継ぐ起動方法。
/// </summary>
public enum BattleLaunchMode
{
    Direct,
    NewGame,
    Continue
}

/// <summary>
/// 第24段階：
/// タイトルで選択したDATA番号を
/// マップ選択・会話・戦闘Sceneへ渡す。
///
/// staticなのでSceneManager.LoadSceneを
/// またいでも情報を保持する。
///
/// JSONデータそのものは保持しない。
/// </summary>
public static class BattleLaunchSession
{
    public static BattleLaunchMode Mode
    {
        get;
        private set;
    } = BattleLaunchMode.Direct;

    public static int CurrentSlot
    {
        get;
        private set;
    } = 1;

    public static string StageId
    {
        get;
        private set;
    } = "";

    private static bool continuePending;
    private static bool savedAfterNewGame;

    public static bool HasContinuePending =>
        Mode == BattleLaunchMode.Continue &&
        continuePending;

    // 新規開始直後は、上書き前の古いデータを
    // F10で誤ってロードしないようにする。
    public static bool CanDebugLoad =>
        Mode != BattleLaunchMode.NewGame ||
        savedAfterNewGame;

    public static bool BeginNewGame(int slot)
    {
        if (!IsValidSlot(slot))
        {
            return false;
        }

        Mode = BattleLaunchMode.NewGame;
        CurrentSlot = slot;
        StageId = "";
        continuePending = false;
        savedAfterNewGame = false;

        Debug.Log(
            $"【新規ゲーム要求】DATA{slot}"
        );

        return true;
    }

    public static bool BeginContinue(
        int slot,
        string stageId)
    {
        if (!IsValidSlot(slot) ||
            string.IsNullOrWhiteSpace(stageId))
        {
            return false;
        }

        Mode = BattleLaunchMode.Continue;
        CurrentSlot = slot;
        StageId = stageId.Trim();
        continuePending = true;
        savedAfterNewGame = false;

        Debug.Log(
            $"【コンテニュー要求】DATA{slot}" +
            $" / StageID={StageId}"
        );

        return true;
    }

    public static void NotifySaveCompleted(int slot)
    {
        if (slot == CurrentSlot &&
            Mode == BattleLaunchMode.NewGame)
        {
            savedAfterNewGame = true;
        }
    }

    public static void NotifyContinueRestored()
    {
        continuePending = false;
    }

    public static void Clear()
    {
        Mode = BattleLaunchMode.Direct;
        CurrentSlot = 1;
        StageId = "";
        continuePending = false;
        savedAfterNewGame = false;
    }

    private static bool IsValidSlot(int slot)
    {
        if (slot >= 1 && slot <= 3)
        {
            return true;
        }

        Debug.LogError(
            $"不正なDATA番号：{slot}"
        );

        return false;
    }
}
