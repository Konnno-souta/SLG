
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 既存のDATA1～DATA3を
/// JSONのセーブデータと接続する。
///
/// 第18段階：
/// ・NewGameなら空きスロットも選べる
/// ・Continueなら有効な保存済みだけ選べる
/// ・選択結果はControllerに渡す
///
/// 既存のSaveDataSlotは変更しない。
/// </summary>
[RequireComponent(typeof(SaveDataSlot))]
public sealed class TitleSaveSlotBridge : MonoBehaviour
{
    [Header("スロット番号")]
    [SerializeField, Min(1)]
    private int slotNumber = 1;

    [Header("既存の表示スクリプト")]
    [SerializeField]
    private SaveDataSlot saveDataSlot;

    [Header("選択ボタン")]
    [SerializeField]
    private Button selectButton;

    [Header("ログ")]
    [SerializeField]
    private bool showLogs = true;

    // ========================================
    // 内部状態
    // ========================================

    private bool hasValidSave;
    private bool hasSaveFile;

    private string savedStageId = "";

    private bool initialized;

    private TitleSlotOpenMode currentMode =
        TitleSlotOpenMode.None;

    private TitleSaveSlotSelectionController
        selectionController;

    // ========================================
    // 初期化
    // ========================================

    private void Awake()
    {
        if (saveDataSlot == null)
        {
            saveDataSlot =
                GetComponent<SaveDataSlot>();
        }

        if (selectButton == null)
        {
            selectButton =
                GetComponent<Button>();
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(
                OnSlotSelected
            );
        }
        else
        {
            Debug.LogWarning(
                $"{name}: Select Buttonがありません。",
                this
            );
        }
    }

    private void Start()
    {
        initialized = true;
        RefreshSlot();
    }

    private void OnEnable()
    {
        if (initialized)
        {
            RefreshSlot();
        }
    }

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(
                OnSlotSelected
            );
        }
    }

    // ========================================
    // Controllerとの接続
    // ========================================

    public void SetController(
        TitleSaveSlotSelectionController controller)
    {
        selectionController = controller;
    }

    /// <summary>
    /// タイトルで選ばれたモードを設定。
    /// </summary>
    public void Configure(
        TitleSlotOpenMode mode,
        TitleSaveSlotSelectionController controller)
    {
        currentMode = mode;
        selectionController = controller;

        RefreshSlot();
    }

    // ========================================
    // JSON保存先
    // ========================================

    private string GetSavePath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            "BattleSaves",
            $"battle_slot_{slotNumber}.json"
        );
    }

    // ========================================
    // データ状態の更新
    // ========================================

    public void RefreshSlot()
    {
        if (saveDataSlot == null)
        {
            Debug.LogError(
                $"{name}: SaveDataSlotがありません。",
                this
            );
            return;
        }

        string path = GetSavePath();

        // ファイル自体が存在するか。
        // 不正なJSONであっても、
        // 上書き確認なしには削除・更新しない。
        hasSaveFile = File.Exists(path);

        hasValidSave = false;
        savedStageId = "";

        if (hasSaveFile &&
            TryReadSave(out BattleSaveData data))
        {
            hasValidSave = true;
            savedStageId = data.stageId;
        }

        // 既存のUI更新処理を使う。
        saveDataSlot.SetSaveInfo(
            hasValidSave,
            savedStageId
        );

        // ====================================
        // モードごとのボタン切り替え
        // ====================================

        if (selectButton != null)
        {
            switch (currentMode)
            {
                case TitleSlotOpenMode.NewGame:
                    // 空きスロットも保存済みも選べる。
                    selectButton.interactable = true;
                    break;

                case TitleSlotOpenMode.Continue:
                    // 正しい保存データだけ。
                    selectButton.interactable =
                        hasValidSave;
                    break;

                default:
                    selectButton.interactable = false;
                    break;
            }
        }

        if (showLogs)
        {
            string status =
                hasValidSave
                    ? $"保存済み：{savedStageId}"
                    : hasSaveFile
                        ? "保存ファイルあり（形式未確認）"
                        : "NO DATA";

            Debug.Log(
                $"【DATA{slotNumber}】" +
                $"\nモード：{currentMode}" +
                $"\n状態：{status}",
                this
            );
        }
    }

    // ========================================
    // JSONの確認
    // ========================================

    private bool TryReadSave(
        out BattleSaveData data)
    {
        data = null;

        try
        {
            string json = File.ReadAllText(
                GetSavePath(),
                Encoding.UTF8
            );

            BattleSaveData loaded =
                JsonUtility.FromJson<BattleSaveData>(
                    json
                );

            if (loaded == null ||
                loaded.version != 1 ||
                string.IsNullOrWhiteSpace(
                    loaded.stageId) ||
                loaded.turnNumber < 1 ||
                loaded.battlePhase != "Player" ||
                (loaded.classMode != "Light" &&
                 loaded.classMode != "Dark") ||
                loaded.units == null ||
                loaded.units.Count == 0)
            {
                return false;
            }

            data = loaded;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"DATA{slotNumber}: " +
                $"JSON読み込みに失敗しました。\n" +
                ex.Message,
                this
            );

            return false;
        }
    }

    // ========================================
    // DATAをクリックしたとき
    // ========================================

    private void OnSlotSelected()
    {
        // クリック時にファイルを再確認する。
        RefreshSlot();

        if (selectionController == null)
        {
            selectionController =
                GetComponentInParent<
                    TitleSaveSlotSelectionController
                >();
        }

        if (selectionController == null)
        {
            Debug.LogError(
                "TitleSaveSlotSelectionController" +
                "が見つかりません。",
                this
            );
            return;
        }

        // モードに応じた処理は
        // Controllerへ任せる。
        selectionController.OnSlotClicked(
            slotNumber,
            hasValidSave,
            hasSaveFile,
            savedStageId
        );
    }
}
