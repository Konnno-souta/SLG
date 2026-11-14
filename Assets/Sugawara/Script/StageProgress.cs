using System;
using System.Collections.Generic;
using UnityEngine;

// ステージの進行状況（セーブデータに入れる中身）
// セーブシステムはこのクラスをそのまま JSON などにして保存すればよい
[Serializable]
public class StageProgressData
{
    public List<int> clearedStages = new List<int>();   // クリアしたステージ番号
    public List<string> shownRoads = new List<string>(); // 開放演出を見た道（"1_2" など）
    public int currentStageId = -1;                      // 最後に遊んだステージ番号
}

// ステージの進行状況を管理する（どのシーンからでも StageProgress.〇〇 で呼べる）
//
// ・ゲーム中の読み書きはすべてここを通る（StagePoint / StageRoad / StageSelectManager / StageMarker）
// ・セーブシステムとの連携
//     保存するとき： StageProgress.Data をセーブデータに入れる
//     読み込むとき： StageProgress.Load(読み込んだデータ) を呼ぶ
//     新しく始めるとき： StageProgress.ResetAll() を呼ぶ
public static class StageProgress
{
    static StageProgressData data = new StageProgressData();

    // ステージがクリアされたときに呼ばれる（マップ上の道が開く演出などに使う）
    public static event Action<int> OnCleared;

    // ---- セーブシステム用 ----

    // 今の進行状況（セーブするときにこれを保存する）
    public static StageProgressData Data => data;

    // セーブデータから読み込んだ進行状況を反映する
    public static void Load(StageProgressData loaded)
    {
        data = loaded ?? new StageProgressData();
    }

    // 進行状況をすべて消す（ニューゲーム・デバッグ用）
    public static void ResetAll()
    {
        data = new StageProgressData();
        Debug.Log("[StageProgress] 進行状況をリセットしました");
    }

    // ---- ゲーム中に使う ----

    // 今遊んでいる（最後に選んだ）ステージの番号。マップに戻ったときの立ち位置にも使う
    public static int CurrentStageId
    {
        get => data.currentStageId;
        set => data.currentStageId = value;
    }

    public static bool IsCleared(int stageId) => data.clearedStages.Contains(stageId);

    public static void SetCleared(int stageId)
    {
        if (IsCleared(stageId)) return;
        data.clearedStages.Add(stageId);
        OnCleared?.Invoke(stageId);
    }

    // ゲームプレイシーンでクリアしたときはこれを呼ぶだけでOK
    public static void ClearCurrentStage()
    {
        if (CurrentStageId >= 0) SetCleared(CurrentStageId);
    }

    // 道の開放演出を一度見たかどうか（2回目以降は最初から開いた状態で表示する）
    public static bool IsRoadShown(string roadKey) => data.shownRoads.Contains(roadKey);

    public static void SetRoadShown(string roadKey)
    {
        if (!IsRoadShown(roadKey)) data.shownRoads.Add(roadKey);
    }
}