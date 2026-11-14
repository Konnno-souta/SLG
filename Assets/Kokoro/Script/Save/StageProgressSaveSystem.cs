
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// DATA1～DATA3のマップセーブファイル。
/// </summary>
[Serializable]
public sealed class StageProgressSaveFile
{
    public int version = 1;
    public int slotNumber;
    public string savedAtUtc;
    public StageProgressData progress;
}

/// <summary>
/// マップ進行状況のセーブ・ロード。
///
/// マップ担当者のStageProgressをそのまま使用。
///
/// ・クリア済みステージ
/// ・道の開放演出
/// ・現在のステージ
///
/// これらをスロットごとにJSON保存する。
/// </summary>
public static class StageProgressSaveSystem
{
    private const int SaveVersion = 1;
    private const int SlotCount = 3;

    // ====================================
    // 保存先
    // ====================================

    public static string SaveDirectory =>
        Path.Combine(
            Application.persistentDataPath,
            "BattleSaves"
        );

    public static string GetSavePath(int slot)
    {
        return Path.Combine(
            SaveDirectory,
            $"map_slot_{slot}.json"
        );
    }

    private static bool IsValidSlot(int slot)
    {
        return slot >= 1 && slot <= SlotCount;
    }

    // ====================================
    // 保存
    // ====================================

    /// <summary>
    /// StageProgress.DataをJSONに保存。
    /// マップ画面のセーブボタンから呼べる。
    /// </summary>
    public static bool SaveSlot(int slot)
    {
        if (!IsValidSlot(slot))
        {
            Debug.LogError(
                $"マップ保存：不正なスロット {slot}"
            );
            return false;
        }

        StageProgressData data =
            StageProgress.Data;

        if (data == null)
        {
            Debug.LogError(
                "マップ保存：進行データがありません。"
            );
            return false;
        }

        try
        {
            Directory.CreateDirectory(
                SaveDirectory
            );

            StageProgressSaveFile save =
                new StageProgressSaveFile
                {
                    version = SaveVersion,
                    slotNumber = slot,
                    savedAtUtc =
                        DateTime.UtcNow.ToString("o"),
                    progress = data
                };

            string json =
                JsonUtility.ToJson(save, true);

            File.WriteAllText(
                GetSavePath(slot),
                json,
                new UTF8Encoding(false)
            );

            Debug.Log(
                $"【マップ保存成功】DATA{slot}" +
                $"\nクリア数：{data.clearedStages.Count}" +
                $"\n解放済み演出数：{data.shownRoads.Count}" +
                $"\n現在ステージ：{data.currentStageId}"
            );

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"【マップ保存失敗】{ex.Message}"
            );
            return false;
        }
    }

    // ====================================
    // 読み込み
    // ====================================

    /// <summary>
    /// 指定スロットのマップ進行情報を復元。
    ///
    /// 未作成スロットは初期状態にする。
    /// 壊れたファイルは上書きしない。
    /// </summary>
    public static bool LoadSlot(int slot)
    {
        if (!IsValidSlot(slot))
        {
            Debug.LogError(
                $"マップロード：不正なスロット {slot}"
            );
            return false;
        }

        string path = GetSavePath(slot);

        if (!File.Exists(path))
        {
            StageProgress.ResetAll();

            Debug.Log(
                $"DATA{slot}: マップ初期状態を読み込み"
            );

            return true;
        }

        try
        {
            string json =
                File.ReadAllText(
                    path,
                    Encoding.UTF8
                );

            StageProgressSaveFile save =
                JsonUtility.FromJson<
                    StageProgressSaveFile>(json);

            if (save == null ||
                save.version != SaveVersion ||
                save.slotNumber != slot ||
                save.progress == null ||
                save.progress.clearedStages == null ||
                save.progress.shownRoads == null ||
                save.progress.currentStageId < -1)
            {
                Debug.LogError(
                    "マップロード：保存データが不正です。" +
                    $"\n{path}"
                );

                return false;
            }

            // クリアデータの二重登録を防ぐ。
            save.progress.clearedStages =
                new List<int>(
                    new HashSet<int>(
                        save.progress.clearedStages
                    )
                );

            save.progress.shownRoads =
                new List<string>(
                    new HashSet<string>(
                        save.progress.shownRoads
                    )
                );

            StageProgress.Load(save.progress);

            Debug.Log(
                $"【マップロード成功】DATA{slot}" +
                $"\nクリア数：" +
                $"{StageProgress.Data.clearedStages.Count}" +
                $"\n現在ステージ：" +
                $"{StageProgress.CurrentStageId}"
            );

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"【マップロード失敗】{ex.Message}"
            );
            return false;
        }
    }

    // ====================================
    // NEW GAME
    // ====================================

    /// <summary>
    /// 新規ゲーム開始時に1回だけ実行。
    /// マップ情報を初期化して保存する。
    ///
    /// 戦闘セーブファイルは変更しない。
    /// </summary>
    public static bool ResetSlotForNewGame(
        int slot)
    {
        if (!IsValidSlot(slot))
        {
            return false;
        }

        StageProgress.ResetAll();

        return SaveSlot(slot);
    }

    // ====================================
    // ステージクリア
    // ====================================

    /// <summary>
    /// 戦闘勝利時に使用。
    ///
    /// 指定スロットの情報を読み込み、
    /// クリア情報を追加して保存する。
    /// </summary>
    public static bool RecordStageClear(
        int slot,
        int stageId)
    {
        if (!IsValidSlot(slot) ||
            stageId < 1)
        {
            return false;
        }

        if (!LoadSlot(slot))
        {
            return false;
        }

        StageProgress.CurrentStageId = stageId;

        StageProgress.SetCleared(stageId);

        return SaveSlot(slot);
    }

    // ====================================
    // セーブデータの有無
    // ====================================

    public static bool HasSave(int slot)
    {
        return IsValidSlot(slot) &&
            File.Exists(GetSavePath(slot));
    }
}
