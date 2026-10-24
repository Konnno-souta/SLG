
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// タイトル用JSON読み込み。
///
/// 既存のSaveDataSlotは表示処理を担当。
/// このクラスは保存ファイルの確認を担当。
/// </summary>
[RequireComponent(typeof(SaveDataSlot))]
public sealed class TitleSaveSlotJsonBinder : MonoBehaviour
{
    [Serializable]
    private sealed class SaveUnitHeader
    {
        public string spawnId;
    }

    /// <summary>
    /// BattleSaveDataのうち、
    /// タイトル画面で必要な情報だけ取得。
    /// </summary>
    [Serializable]
    private sealed class SaveHeader
    {
        public int version;
        public string stageId;
        public int turnNumber;
        public string classMode;

        public List<SaveUnitHeader> units;
    }

    [Header("対応するスロット番号")]
    [SerializeField, Min(1)]
    private int slotNumber = 1;

    [Header("既存の表示スクリプト")]
    [SerializeField]
    private SaveDataSlot saveDataSlot;

    [Header("ログ")]
    [SerializeField]
    private bool showLogs = false;

    private bool initialized;

    private void Awake()
    {
        if (saveDataSlot == null)
        {
            saveDataSlot = GetComponent<SaveDataSlot>();
        }
    }

    private void Start()
    {
        initialized = true;
        RefreshSlot();
    }

    private void OnEnable()
    {
        // 画面が再表示されたときに更新。
        // 初回はStartで実行する。
        if (initialized)
        {
            RefreshSlot();
        }
    }

    /// <summary>
    /// 指定番号のJSONを確認する。
    ///
    /// 保存データがない場合は
    /// 既存のNO DATA表示を使用する。
    /// </summary>
    public void RefreshSlot()
    {
        if (saveDataSlot == null)
        {
            Debug.LogError(
                "SaveDataSlotが見つかりません。",
                this
            );
            return;
        }

        string path = Path.Combine(
            Application.persistentDataPath,
            "BattleSaves",
            $"battle_slot_{slotNumber}.json"
        );

        // セーブファイルが存在しない。
        if (!File.Exists(path))
        {
            saveDataSlot.SetSaveInfo(false, "");

            if (showLogs)
            {
                Debug.Log(
                    $"DATA{slotNumber}: NO DATA",
                    this
                );
            }

            return;
        }

        try
        {
            string json = File.ReadAllText(
                path,
                Encoding.UTF8
            );

            SaveHeader data =
                JsonUtility.FromJson<SaveHeader>(
                    json
                );

            // JSON形式か簡易確認。
            bool valid =
                data != null &&
                data.version == 1 &&
                !string.IsNullOrWhiteSpace(
                    data.stageId
                ) &&
                data.turnNumber >= 1 &&
                (data.classMode == "Light" ||
                 data.classMode == "Dark") &&
                data.units != null &&
                data.units.Count > 0;

            if (!valid)
            {
                saveDataSlot.SetSaveInfo(false, "");

                Debug.LogWarning(
                    $"DATA{slotNumber}: " +
                    "JSONの形式を確認できません。",
                    this
                );

                return;
            }

            // 既存のSaveDataSlotへ情報を渡す。
            saveDataSlot.SetSaveInfo(
                true,
                data.stageId
            );

            if (showLogs)
            {
                Debug.Log(
                    $"DATA{slotNumber}: " +
                    $"{data.stageId} / " +
                    $"TURN {data.turnNumber}",
                    this
                );
            }
        }
        catch (Exception ex)
        {
            saveDataSlot.SetSaveInfo(false, "");

            Debug.LogWarning(
                $"DATA{slotNumber}: " +
                $"JSON読み込み失敗\n{ex.Message}",
                this
            );
        }
    }
}
