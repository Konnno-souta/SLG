
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// セーブスロット選択画面の動作モード。
/// </summary>
public enum TitleSlotOpenMode
{
    None,
    NewGame,
    Continue
}

/// <summary>
/// 他のスクリプトへスロット番号を渡すイベント。
/// </summary>
[Serializable]
public sealed class TitleSlotPickedEvent
    : UnityEvent<int>
{
}

/// <summary>
/// タイトルのセーブスロット選択を管理。
///
/// ・始めから：空き／使用中の両方を選べる
/// ・コンテニュー：保存済みだけ選べる
/// ・保存済みへの新規開始は上書き確認
/// ・選択したスロット情報を保持
///
/// 既存のSaveDataSelectや
/// Title_ButtonActionの画面演出は変更しない。
///
/// このスクリプト自体は
/// シーン切り替えやJSON保存を行わない。
/// </summary>
public sealed class TitleSaveSlotSelectionController
    : MonoBehaviour
{
    [Header("セーブスロット")]
    [SerializeField]
    private TitleSaveSlotBridge[] slotBridges;

    [Header("既存の閉じるボタン")]
    [SerializeField]
    private Button closeButton;

    [Header("上書き確認パネル")]
    [SerializeField]
    private GameObject overwriteConfirmPanel;

    [SerializeField]
    private TMP_Text overwriteMessageText;

    [SerializeField]
    private Button overwriteYesButton;

    [SerializeField]
    private Button overwriteNoButton;

    [Header("他の担当処理への接続（今は空でもOK）")]
    [SerializeField]
    private TitleSlotPickedEvent
        onNewGameSlotChosen =
            new TitleSlotPickedEvent();

    [SerializeField]
    private TitleSlotPickedEvent
        onContinueSlotChosen =
            new TitleSlotPickedEvent();

    [Header("デバッグ")]
    [SerializeField]
    private bool showLogs = true;

    private TitleSlotOpenMode currentMode =
        TitleSlotOpenMode.None;

    private int overwriteTargetSlot = 0;

    // ========================================
    // 初期化
    // ========================================

    private void Awake()
    {
        if (slotBridges == null ||
            slotBridges.Length == 0)
        {
            slotBridges =
                GetComponentsInChildren<
                    TitleSaveSlotBridge
                >(true);
        }

        foreach (TitleSaveSlotBridge bridge
            in slotBridges)
        {
            if (bridge != null)
            {
                bridge.SetController(this);
            }
        }

        // 既存のClose処理は削除しない。
        // ここでは選択状態の取り消しだけ担当。
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(
                CancelSelection
            );
        }

        if (overwriteYesButton != null)
        {
            overwriteYesButton.onClick.AddListener(
                ConfirmOverwrite
            );
        }

        if (overwriteNoButton != null)
        {
            overwriteNoButton.onClick.AddListener(
                CancelOverwrite
            );
        }

        HideOverwritePanel();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(
                CancelSelection
            );
        }

        if (overwriteYesButton != null)
        {
            overwriteYesButton.onClick.RemoveListener(
                ConfirmOverwrite
            );
        }

        if (overwriteNoButton != null)
        {
            overwriteNoButton.onClick.RemoveListener(
                CancelOverwrite
            );
        }
    }

    // ========================================
    // モード設定
    // ========================================

    /// <summary>
    /// Title_ButtonActionがウィンドウ生成後に
    /// 呼び出す。
    /// </summary>
    public void Configure(
        TitleSlotOpenMode mode)
    {
        currentMode = mode;

        overwriteTargetSlot = 0;
        HideOverwritePanel();

        // 新しく画面を開いたので、
        // 前の選択情報は解除する。
        PendingNewGameSlotRequest.Clear();
        PendingBattleContinueRequest.Clear();

        foreach (TitleSaveSlotBridge bridge
            in slotBridges)
        {
            if (bridge != null)
            {
                bridge.Configure(
                    currentMode,
                    this
                );
            }
        }

        if (showLogs)
        {
            Debug.Log(
                "【セーブスロット画面】" +
                $"\nモード：{currentMode}",
                this
            );
        }
    }

    // ========================================
    // スロットが押された
    // ========================================

    public void OnSlotClicked(
        int slotNumber,
        bool validSaveExists,
        bool fileExists,
        string stageId)
    {
        if (currentMode ==
            TitleSlotOpenMode.NewGame)
        {
            HandleNewGame(
                slotNumber,
                fileExists
            );
            return;
        }

        if (currentMode ==
            TitleSlotOpenMode.Continue)
        {
            HandleContinue(
                slotNumber,
                validSaveExists,
                stageId
            );
            return;
        }

        Debug.LogWarning(
            "セーブ選択モードが設定されていません。",
            this
        );
    }

    // ========================================
    // 始めから
    // ========================================

    private void HandleNewGame(
        int slotNumber,
        bool fileExists)
    {
        if (!fileExists)
        {
            // 空きスロットなので確認不要。
            SelectNewGameSlot(
                slotNumber,
                false
            );
            return;
        }

        // 保存ファイルが存在する場合は
        // 上書き確認を必須にする。
        overwriteTargetSlot = slotNumber;

        if (overwriteConfirmPanel == null ||
            overwriteYesButton == null ||
            overwriteNoButton == null)
        {
            Debug.LogError(
                "上書き確認パネルの参照がありません。" +
                "データ保護のため新規開始を中止します。",
                this
            );

            overwriteTargetSlot = 0;
            return;
        }

        if (overwriteMessageText != null)
        {
            overwriteMessageText.text =
                $"DATA{slotNumber}には" +
                "保存ファイルがあります。\n" +
                "新しくゲームを始めて、" +
                "最初の保存時に置き換えますか？";
        }

        overwriteConfirmPanel.SetActive(true);
    }

    // ========================================
    // 上書き確認
    // ========================================

    private void ConfirmOverwrite()
    {
        if (overwriteTargetSlot < 1)
        {
            return;
        }

        int slotNumber =
            overwriteTargetSlot;

        overwriteTargetSlot = 0;

        HideOverwritePanel();

        // ここでは古いJSONを消さない。
        // 新しいゲームの保存ができた時点で
        // 同じスロットへ書き込む。
        SelectNewGameSlot(
            slotNumber,
            true
        );
    }

    private void CancelOverwrite()
    {
        overwriteTargetSlot = 0;
        HideOverwritePanel();

        if (showLogs)
        {
            Debug.Log(
                "【上書き確認キャンセル】",
                this
            );
        }
    }

    private void HideOverwritePanel()
    {
        if (overwriteConfirmPanel != null)
        {
            overwriteConfirmPanel.SetActive(false);
        }
    }

    // ========================================
    // 新規ゲーム保存先の確定
    // ========================================

    private void SelectNewGameSlot(
        int slotNumber,
        bool overwriteConfirmed)
    {
        // 続きからの要求を残さない。
        PendingBattleContinueRequest.Clear();

        bool success =
            PendingNewGameSlotRequest.SetRequest(
                slotNumber,
                overwriteConfirmed
            );

        if (!success)
        {
            return;
        }

        Debug.Log(
            "【始めから：スロット選択完了】" +
            $"\nDATA：{slotNumber}" +
            $"\n上書き確認：{overwriteConfirmed}" +
            "\n次はMAP選択へ進む準備ができました。",
            this
        );

        // 将来のMAP選択遷移処理に渡せる。
        onNewGameSlotChosen?.Invoke(
            slotNumber
        );
    }

    // ========================================
    // コンテニュー
    // ========================================

    private void HandleContinue(
        int slotNumber,
        bool validSaveExists,
        string stageId)
    {
        if (!validSaveExists ||
            string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogWarning(
                $"DATA{slotNumber}には" +
                "読み込み可能なデータがありません。",
                this
            );

            return;
        }

        PendingNewGameSlotRequest.Clear();

        bool success =
            PendingBattleContinueRequest.SetRequest(
                slotNumber,
                stageId
            );

        if (!success)
        {
            return;
        }

        Debug.Log(
            "【コンテニュー：スロット選択完了】" +
            $"\nDATA：{slotNumber}" +
            $"\nステージ：{stageId}",
            this
        );

        // 将来のロード遷移処理に渡せる。
        onContinueSlotChosen?.Invoke(
            slotNumber
        );
    }

    // ========================================
    // 画面を閉じてキャンセル
    // ========================================

    public void CancelSelection()
    {
        overwriteTargetSlot = 0;
        HideOverwritePanel();

        PendingNewGameSlotRequest.Clear();
        PendingBattleContinueRequest.Clear();

        if (showLogs)
        {
            Debug.Log(
                "【スロット選択キャンセル】",
                this
            );
        }
    }
}
