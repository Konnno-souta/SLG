
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 戦闘中の行動コマンドUI。
///
/// 攻撃・戦技・待機・戻るの4ボタンを管理する。
///
/// ボタンを押したときはイベントを通知し、
/// 実際のユニット操作は
/// UnitSelectionControllerが担当する。
/// </summary>
public sealed class BattleCommandPanel : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private Button attackButton;

    [SerializeField]
    private Button skillButton;

    [SerializeField]
    private Button waitButton;

    [SerializeField]
    private Button cancelButton;

    [Header("グリッド入力")]
    [SerializeField]
    private GridClickInput gridInput;

    /// <summary>
    /// 攻撃コマンドが押された通知。
    /// 今回追加する。
    /// </summary>
    public event Action AttackRequested;

    /// <summary>
    /// 待機コマンドが押された通知。
    /// </summary>
    public event Action WaitRequested;

    /// <summary>
    /// 戻るボタンが押された通知。
    /// </summary>
    public event Action CancelRequested;

    public bool IsOpen =>
        panelRoot != null &&
        panelRoot.activeSelf;

    private void Awake()
    {
        if (panelRoot == null ||
            attackButton == null ||
            skillButton == null ||
            waitButton == null ||
            cancelButton == null ||
            gridInput == null)
        {
            Debug.LogError(
                "BattleCommandPanelの参照が不足しています。",
                this
            );

            enabled = false;
            return;
        }

        // 今回から攻撃ボタンを使えるようにする
        attackButton.interactable = true;

        // 戦技はまだ未実装
        skillButton.interactable = false;

        waitButton.interactable = true;
        cancelButton.interactable = true;

        // ボタンイベント登録
        attackButton.onClick.AddListener(OnAttackClicked);
        waitButton.onClick.AddListener(OnWaitClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);

        // 起動時は閉じる
        panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (attackButton != null)
        {
            attackButton.onClick.RemoveListener(
                OnAttackClicked
            );
        }

        if (waitButton != null)
        {
            waitButton.onClick.RemoveListener(
                OnWaitClicked
            );
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(
                OnCancelClicked
            );
        }
    }

    /// <summary>
    /// コマンドを表示する。
    /// </summary>
    public void Show()
    {
        if (!enabled || panelRoot == null)
            return;

        // UI操作中はグリッドを動かさない
        gridInput.SetGridInputEnabled(false);

        panelRoot.SetActive(true);

        // キーボード・ゲームパッドの
        // 初期選択位置を待機にする
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);

            EventSystem.current.SetSelectedGameObject(
                waitButton.gameObject
            );
        }
    }

    /// <summary>
    /// コマンドを閉じる。
    /// </summary>
    public void Hide()
    {
        if (panelRoot == null)
            return;

        if (EventSystem.current != null)
        {
            GameObject current =
                EventSystem.current.currentSelectedGameObject;

            if (current != null &&
                current.transform.IsChildOf(
                    panelRoot.transform))
            {
                EventSystem.current.SetSelectedGameObject(
                    null
                );
            }
        }

        panelRoot.SetActive(false);

        // グリッド操作に戻す
        if (gridInput != null)
        {
            gridInput.SetGridInputEnabled(true);
        }
    }

    private void OnAttackClicked()
    {
        if (!IsOpen)
            return;

        AttackRequested?.Invoke();
    }

    private void OnWaitClicked()
    {
        if (!IsOpen)
            return;

        WaitRequested?.Invoke();
    }

    private void OnCancelClicked()
    {
        if (!IsOpen)
            return;

        CancelRequested?.Invoke();
    }
}
