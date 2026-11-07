using System;
using UnityEngine;

// ステージの進行状況（クリア済みか・最後に遊んだステージ）を保存する
// どのシーンからでも StageProgress.〇〇 で呼べる
public static class StageProgress
{
    const string ClearKey = "StageClear_";
    const string RoadKey = "RoadOpened_";
    const string CurrentKey = "CurrentStage";
    const int MaxStageId = 100; // リセット時に消す範囲

    // ステージがクリアされたときに呼ばれる（マップ上の道が開く演出に使う）
    public static event Action<int> OnCleared;

    // 今遊んでいる（最後に選んだ）ステージの番号。マップに戻ったときの立ち位置にも使う
    public static int CurrentStageId
    {
        get => PlayerPrefs.GetInt(CurrentKey, -1);
        set { PlayerPrefs.SetInt(CurrentKey, value); PlayerPrefs.Save(); }
    }

    public static bool IsCleared(int stageId) => PlayerPrefs.GetInt(ClearKey + stageId, 0) == 1;

    public static void SetCleared(int stageId)
    {
        if (IsCleared(stageId)) return;
        PlayerPrefs.SetInt(ClearKey + stageId, 1);
        PlayerPrefs.Save();
        OnCleared?.Invoke(stageId);
    }

    // ゲームプレイシーンでクリアしたときはこれを呼ぶだけでOK
    public static void ClearCurrentStage()
    {
        if (CurrentStageId >= 0) SetCleared(CurrentStageId);
    }

    // 道の開放演出を一度見たかどうか（2回目以降は最初から開いた状態で表示する）
    public static bool IsRoadShown(string roadKey) => PlayerPrefs.GetInt(RoadKey + roadKey, 0) == 1;

    public static void SetRoadShown(string roadKey)
    {
        PlayerPrefs.SetInt(RoadKey + roadKey, 1);
        PlayerPrefs.Save();
    }

    // 進行状況をすべて消す（デバッグ用）
    public static void ResetAll()
    {
        for (int a = 0; a <= MaxStageId; a++)
        {
            PlayerPrefs.DeleteKey(ClearKey + a);
            for (int b = 0; b <= MaxStageId; b++)
                PlayerPrefs.DeleteKey(RoadKey + a + "_" + b);
        }
        PlayerPrefs.DeleteKey(CurrentKey);
        PlayerPrefs.Save();
        Debug.Log("[StageProgress] 進行状況をリセットしました");
    }
}