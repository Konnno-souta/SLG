
using System;
using System.IO;
using System.Text;
using UnityEngine;


/// <summary>
/// マップ進行情報の保存データ。
/// ST01～ST06の順番に解放する仕様。
/// </summary>
[Serializable]
public sealed class MapProgressData
{
    public int version = 1;
    public int slotNumber = 1;

    // 1ならST01のみ、3ならST01～ST03が解放。
    public int highestUnlockedStage = 1;

    // マップで最後に選択したステージ。
    public int lastSelectedStage = 1;
}

/// <summary>
/// 第27段階：マップ解放情報のセーブ管理。
///
/// DATA1～DATA3に対応。
/// 戦闘セーブとは別のJSONに保存する。
///
/// MapScene、TitleScene、BattleSceneの
/// どこからでも呼び出せる。
/// </summary>
public static class MapProgressSave
{
    public const int SlotCount = 3;
    public const int StageCount = 6;

    private const int SaveVersion = 1;

    public enum ContinueDestination
    {
        None,
        Map,
        Battle
    }

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

    private static string GetBattleSavePath(int slot)
    {
        return Path.Combine(
            SaveDirectory,
            $"battle_slot_{slot}.json"
        );
    }

    private static bool IsValidSlot(int slot)
    {
        return slot >= 1 && slot <= SlotCount;
    }

    private static bool IsValidStage(int stage)
    {
        return stage >= 1 && stage <= StageCount;
    }

    // ====================================
    // 新規データ
    // ====================================

    private static MapProgressData CreateNewData(
        int slot)
    {
        return new MapProgressData
        {
            version = SaveVersion,
            slotNumber = slot,
            highestUnlockedStage = 1,
            lastSelectedStage = 1
        };
    }

    // ====================================
    // 読み込み
    // ====================================

    /// <summary>
    /// スロットのマップデータを読み込む。
    /// ファイルがない場合はST01のみ解放。
    /// ファイル破損時はfalseを返す。
    /// </summary>
    public static bool TryLoad(
        int slot,
        out MapProgressData data)
    {
        data = null;

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
            data = CreateNewData(slot);
            return true;
        }

        try
        {
            string json =
                File.ReadAllText(path, Encoding.UTF8);

            MapProgressData loaded =
                JsonUtility.FromJson<MapProgressData>(
                    json
                );

            if (loaded == null ||
                loaded.version != SaveVersion ||
                loaded.slotNumber != slot ||
                loaded.highestUnlockedStage < 1 ||
                loaded.highestUnlockedStage > StageCount)
            {
                Debug.LogError(
                    $"マップセーブデータが不正です：{path}"
                );
                return false;
            }

            loaded.lastSelectedStage = Mathf.Clamp(
                loaded.lastSelectedStage,
                1,
                loaded.highestUnlockedStage
            );

            data = loaded;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"マップロード失敗：{ex.Message}"
            );
            return false;
        }
    }

    // ====================================
    // 書き込み
    // ====================================

    private static bool Write(
        int slot,
        MapProgressData data)
    {
        if (!IsValidSlot(slot) || data == null)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(
                SaveDirectory
            );

            data.version = SaveVersion;
            data.slotNumber = slot;

            string json =
                JsonUtility.ToJson(data, true);

            File.WriteAllText(
                GetSavePath(slot),
                json,
                new UTF8Encoding(false)
            );

            Debug.Log(
                $"【マップセーブ成功】DATA{slot}" +
                $" / 解放ST01～ST{data.highestUnlockedStage:D2}" +
                $" / 選択ST{data.lastSelectedStage:D2}"
            );

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"マップセーブ失敗：{ex.Message}"
            );
            return false;
        }
    }

    // ====================================
    // マップ担当者が使うAPI
    // ====================================

    /// <summary>
    /// ステージが解放済みか取得する。
    /// </summary>
    public static bool IsStageUnlocked(
        int slot,
        int stage)
    {
        if (!IsValidStage(stage) ||
            !TryLoad(slot, out MapProgressData data))
        {
            return false;
        }

        return stage <= data.highestUnlockedStage;
    }

    /// <summary>
    /// 最後に選択したステージを取得。
    /// </summary>
    public static int GetLastSelectedStage(
        int slot)
    {
        if (!TryLoad(slot, out MapProgressData data))
        {
            return 1;
        }

        return data.lastSelectedStage;
    }

    /// <summary>
    /// マップシーンからセーブする。
    /// 既存の解放状況は維持する。
    ///
    /// 未解放ステージは選択できない。
    /// </summary>
    public static bool SaveFromMap(
        int slot,
        int selectedStage)
    {
        if (!TryLoad(slot, out MapProgressData data))
        {
            return false;
        }

        if (!IsValidStage(selectedStage) ||
            selectedStage > data.highestUnlockedStage)
        {
            Debug.LogWarning(
                $"ST{selectedStage:D2}は未解放です。"
            );
            return false;
        }

        data.lastSelectedStage = selectedStage;

        return Write(slot, data);
    }

    // ====================================
    // 戦闘終了時の解放
    // ====================================

    /// <summary>
    /// ステージクリア後、次のステージを解放。
    ///
    /// 例：
    /// ST01クリア → ST02解放。
    /// ST05クリア → ST06解放。
    ///
    /// 解放情報は即座に保存される。
    /// GAME OVER時には呼び出さない。
    /// </summary>
    public static bool UnlockAfterClear(
        int slot,
        int clearedStage)
    {
        if (!IsValidStage(clearedStage) ||
            !TryLoad(slot, out MapProgressData data))
        {
            return false;
        }

        int nextStage =
            Mathf.Min(
                StageCount,
                clearedStage + 1
            );

        data.highestUnlockedStage =
            Mathf.Max(
                data.highestUnlockedStage,
                nextStage
            );

        data.lastSelectedStage = nextStage;

        return Write(slot, data);
    }

    // ====================================
    // NEW GAME用
    // ====================================

    /// <summary>
    /// マップ解放情報を初期化する。
    ///
    /// タイトル側でNEW GAMEが確定したときに
    /// 1回だけ呼び出す。
    ///
    /// 既存の戦闘セーブは削除しない。
    /// </summary>
    public static bool ResetForNewGame(
        int slot)
    {
        if (!IsValidSlot(slot))
        {
            return false;
        }

        return Write(
            slot,
            CreateNewData(slot)
        );
    }

    // ====================================
    // セーブデータの存在確認
    // ====================================

    public static bool HasMapSave(int slot)
    {
        return IsValidSlot(slot) &&
               File.Exists(GetSavePath(slot));
    }

    public static bool HasAnySave(int slot)
    {
        return IsValidSlot(slot) &&
               (File.Exists(GetSavePath(slot)) ||
                File.Exists(GetBattleSavePath(slot)));
    }

    // ====================================
    // タイトルのCONTINUE用
    // ====================================

    /// <summary>
    /// 最後にセーブした場所を取得。
    /// ファイルの更新日時で判定する。
    ///
    /// 実際のロード前にはデータ検証も行う。
    /// </summary>
    public static ContinueDestination
        GetContinueDestination(int slot)
    {
        if (!IsValidSlot(slot))
        {
            return ContinueDestination.None;
        }

        bool hasMap = HasMapSave(slot);
        bool hasBattle =
            File.Exists(GetBattleSavePath(slot));

        if (!hasMap && !hasBattle)
        {
            return ContinueDestination.None;
        }

        if (!hasBattle)
        {
            return ContinueDestination.Map;
        }

        if (!hasMap)
        {
            return ContinueDestination.Battle;
        }

        DateTime mapTime =
            File.GetLastWriteTimeUtc(
                GetSavePath(slot)
            );

        DateTime battleTime =
            File.GetLastWriteTimeUtc(
                GetBattleSavePath(slot)
            );

        return mapTime >= battleTime
            ? ContinueDestination.Map
            : ContinueDestination.Battle;
    }

    // ====================================
    // StageIDの変換
    // ====================================

    /// <summary>
    /// "1"、"01"、"ST01"を
    /// ステージ番号1に変換する。
    /// </summary>
    public static bool TryParseStageNumber(
        string stageId,
        out int stageNumber)
    {
        stageNumber = 0;

        if (string.IsNullOrWhiteSpace(stageId))
        {
            return false;
        }

        string value =
            stageId.Trim();

        if (value.StartsWith(
            "ST",
            StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(2);
        }

        return int.TryParse(
                   value,
                   out stageNumber
               ) &&
               IsValidStage(stageNumber);
    }
}
