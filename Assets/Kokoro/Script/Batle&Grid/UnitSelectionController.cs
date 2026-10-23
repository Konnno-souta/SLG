
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 味方ユニットの操作を管理する。
///
/// 第10段階では攻撃対象の候補選択を追加。
///
/// 既存機能：
/// ・ユニット選択
/// ・移動可能範囲表示
/// ・最短経路で移動
/// ・移動のキャンセル
/// ・待機による行動終了
///
/// 今回追加：
/// ・攻撃コマンドから対象選択へ移行
/// ・敵を候補として選択
/// ・対象選択からコマンドへ戻る
///
/// ダメージや正式な射程判定は未実装。
/// </summary>
public sealed class UnitSelectionController : MonoBehaviour
{
    [Header("グリッド関連")]
    [SerializeField]
    private BattleGrid grid;

    [SerializeField]
    private GridDebugView gridDebugView;

    [SerializeField]
    private GridClickInput gridInput;

    [Header("UI関連")]
    [SerializeField]
    private BattleCommandPanel commandPanel;

    [SerializeField]
    private AttackTargetPreviewPanel attackPreviewPanel;

    [Header("手動配置用ユニット")]
    [SerializeField]
    private List<UnitView> units = new List<UnitView>();

    [Header("開発用")]
    [SerializeField]
    private bool enableDebugResetKey = false;

    // ========================================
    // 操作状態
    // ========================================

    private enum SelectionStep
    {
        None,
        SelectingDestination,
        CommandMenu,

        // 今回追加
        SelectingAttackTarget
    }

    private SelectionStep currentStep =
        SelectionStep.None;

    // CSV生成・手動配置されたユニット
    private readonly List<UnitView> activeUnits =
        new List<UnitView>();

    // 操作中の味方
    private UnitView selectedUnit;

    // 今回選択した敵候補
    private UnitView selectedAttackTarget;

    // 移動できるマス
    private HashSet<Vector2Int> reachableCells =
        new HashSet<Vector2Int>();

    // 移動前の座標
    private Vector2Int originalPosition;

    // 移動経路
    private List<Vector2Int> movePath =
        new List<Vector2Int>();

    // 移動アニメーション中か
    private bool isMoving;

    // UIの決定とグリッドの決定が
    // 同じフレームに二重反応するのを防ぐ
    private int suppressCellSelectionUntilFrame = -1;

    // ========================================
    // 初期化
    // ========================================

    private void Start()
    {
        if (grid == null ||
            gridDebugView == null ||
            gridInput == null ||
            commandPanel == null ||
            attackPreviewPanel == null)
        {
            Debug.LogError(
                "UnitSelectionControllerの参照が不足しています。",
                this
            );

            enabled = false;
            return;
        }

        InitializeManualUnits();

        // グリッド入力
        gridInput.CellSelected += OnCellSelected;

        // コマンドボタン
        commandPanel.AttackRequested +=
            OnAttackRequested;

        commandPanel.WaitRequested +=
            OnWaitRequested;

        commandPanel.CancelRequested +=
            CancelCurrentStep;
    }

    private void OnDestroy()
    {
        if (gridInput != null)
        {
            gridInput.CellSelected -= OnCellSelected;
        }

        if (commandPanel != null)
        {
            commandPanel.AttackRequested -=
                OnAttackRequested;

            commandPanel.WaitRequested -=
                OnWaitRequested;

            commandPanel.CancelRequested -=
                CancelCurrentStep;
        }
    }

    private void Update()
    {
        if (isMoving)
            return;

        HandleCancelInput();
        HandleDebugInput();
    }

    // ========================================
    // キャンセル入力
    // ========================================

    /// <summary>
    /// Esc・右クリック・ゲームパッドのB/○。
    /// </summary>
    private void HandleCancelInput()
    {
        bool cancel = false;

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.escapeKey.wasPressedThisFrame)
        {
            cancel = true;
        }

        Mouse mouse = Mouse.current;

        if (mouse != null &&
            mouse.rightButton.wasPressedThisFrame)
        {
            cancel = true;
        }

        Gamepad gamepad = Gamepad.current;

        if (gamepad != null &&
            gamepad.buttonEast.wasPressedThisFrame)
        {
            cancel = true;
        }

        if (cancel)
        {
            CancelCurrentStep();
        }
    }

    /// <summary>
    /// 開発用の行動リセット。
    /// 通常ゲームではOFFにしておく。
    /// </summary>
    private void HandleDebugInput()
    {
        if (!enableDebugResetKey)
            return;

        if (Keyboard.current == null ||
            !Keyboard.current.rKey.wasPressedThisFrame)
        {
            return;
        }

        // 何か操作中ならリセットしない
        if (currentStep != SelectionStep.None)
            return;

        ResetAllyActions();
    }

    // ========================================
    // ユニット登録
    // ========================================

    /// <summary>
    /// Hierarchyの手動配置ユニットを登録。
    /// CSV自動生成時はUnitsを空にする。
    /// </summary>
    private void InitializeManualUnits()
    {
        foreach (UnitView unit in units)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy)
            {
                continue;
            }

            RegisterSpawnedUnit(unit);
        }
    }

    /// <summary>
    /// BattleUnitSpawnerから呼ばれる。
    /// CSVによる自動生成と手動配置の両方に対応。
    /// </summary>
    public bool RegisterSpawnedUnit(UnitView unit)
    {
        if (grid == null ||
            unit == null ||
            activeUnits.Contains(unit))
        {
            return false;
        }

        Vector2Int position =
            unit.InitialPosition;

        if (FindUnitAt(position) != null)
        {
            Debug.LogError(
                $"{unit.name}: 初期配置が重複しています。",
                unit
            );

            return false;
        }

        if (!unit.Initialize(grid))
        {
            return false;
        }

        activeUnits.Add(unit);

        return true;
    }

    /// <summary>
    /// 指定座標のユニットを取得する。
    /// </summary>
    private UnitView FindUnitAt(
        Vector2Int position)
    {
        foreach (UnitView unit in activeUnits)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy ||
                unit.State == null)
            {
                continue;
            }

            if (unit.State.Position == position)
            {
                return unit;
            }
        }

        return null;
    }

    private bool IsOccupied(Vector2Int position)
    {
        return FindUnitAt(position) != null;
    }

    // ========================================
    // グリッド決定
    // ========================================

    /// <summary>
    /// マスが決定されたとき。
    /// </summary>
    private void OnCellSelected(
        Vector2Int position)
    {
        if (isMoving)
            return;

        // UIから攻撃モードへ切り替わった直後の
        // 決定入力が二重に入らないようにする
        if (Time.frameCount <=
            suppressCellSelectionUntilFrame)
        {
            return;
        }

        // 攻撃対象選択中なら先に処理する
        if (currentStep ==
            SelectionStep.SelectingAttackTarget)
        {
            SelectAttackTargetCandidate(position);
            return;
        }

        // コマンド表示中はグリッドを選ばない
        if (currentStep ==
            SelectionStep.CommandMenu)
        {
            return;
        }

        UnitView clickedUnit =
            FindUnitAt(position);

        if (clickedUnit != null)
        {
            HandleUnitSelection(clickedUnit);
            return;
        }

        if (currentStep ==
                SelectionStep.SelectingDestination &&
            selectedUnit != null &&
            reachableCells.Contains(position))
        {
            TryMoveSelectedUnit(position);
            return;
        }

        DeselectUnit();
    }

    /// <summary>
    /// クリックしたユニットを操作する。
    /// </summary>
    private void HandleUnitSelection(
        UnitView clicked)
    {
        UnitState state = clicked.State;

        if (state == null)
            return;

        // 敵や行動終了済みは操作できない
        if (state.Team != UnitTeam.Ally ||
            state.HasActed ||
            state.HasMoved)
        {
            DeselectUnit();
            return;
        }

        // 選択中の味方をもう一度決定すると
        // 移動せずにコマンドを開く
        if (selectedUnit == clicked)
        {
            OpenCommandMenu();
            return;
        }

        SelectUnit(clicked);
    }

    /// <summary>
    /// 味方を選択する。
    /// </summary>
    private void SelectUnit(UnitView unit)
    {
        selectedUnit = unit;
        selectedAttackTarget = null;

        originalPosition =
            unit.State.Position;

        movePath.Clear();

        currentStep =
            SelectionStep.SelectingDestination;

        ShowSelectedUnitMovementRange();

        Debug.Log(
            $"{unit.name}を選択。移動力：" +
            $"{unit.State.MoveRange}"
        );
    }

    /// <summary>
    /// 移動可能マスを表示。
    /// </summary>
    private void ShowSelectedUnitMovementRange()
    {
        if (selectedUnit == null)
            return;

        UnitState state = selectedUnit.State;

        reachableCells =
            GridRangeCalculator.Calculate(
                grid,
                state.Position,
                state.MoveRange,
                IsOccupied
            );

        gridDebugView.ShowMovementRange(
            reachableCells
        );
    }

    // ========================================
    // 移動
    // ========================================

    private void TryMoveSelectedUnit(
        Vector2Int destination)
    {
        if (selectedUnit == null ||
            isMoving)
        {
            return;
        }

        UnitState state =
            selectedUnit.State;

        if (state.HasActed ||
            state.HasMoved)
        {
            return;
        }

        if (!reachableCells.Contains(destination) ||
            !grid.IsWalkable(destination) ||
            IsOccupied(destination))
        {
            return;
        }

        bool found =
            GridPathfinder.TryFindPath(
                grid,
                state.Position,
                destination,
                state.MoveRange,
                IsOccupied,
                out List<Vector2Int> path
            );

        if (!found)
        {
            Debug.LogWarning(
                $"移動経路が見つかりません：" +
                $"{destination}"
            );

            return;
        }

        originalPosition =
            state.Position;

        movePath =
            new List<Vector2Int>(path);

        StartCoroutine(
            MoveUnitRoutine(
                selectedUnit,
                destination,
                path
            )
        );
    }

    /// <summary>
    /// 経路に沿って移動する。
    /// </summary>
    private IEnumerator MoveUnitRoutine(
        UnitView unit,
        Vector2Int destination,
        List<Vector2Int> path)
    {
        isMoving = true;

        gridInput.SetGridInputEnabled(false);

        gridDebugView.ClearMovementRange();

        yield return StartCoroutine(
            unit.MoveAlongPath(grid, path)
        );

        unit.State.CompleteMove(destination);

        isMoving = false;

        gridInput.SetGridInputEnabled(true);

        OpenCommandMenu();
    }

    // ========================================
    // コマンドメニュー
    // ========================================

    /// <summary>
    /// 攻撃・戦技・待機・戻るを表示。
    /// </summary>
    private void OpenCommandMenu()
    {
        if (selectedUnit == null ||
            isMoving)
        {
            return;
        }

        currentStep = SelectionStep.CommandMenu;

        gridDebugView.ClearMovementRange();

        attackPreviewPanel.Hide();

        commandPanel.Show();

        Debug.Log(
            $"{selectedUnit.name}: コマンド選択"
        );
    }

    // ========================================
    // 攻撃対象の候補選択（今回追加）
    // ========================================

    /// <summary>
    /// 攻撃ボタンが押された。
    ///
    /// この段階では実際の攻撃を開始せず、
    /// 敵の候補選択モードへ切り替える。
    /// </summary>
    private void OnAttackRequested()
    {
        if (currentStep !=
                SelectionStep.CommandMenu ||
            selectedUnit == null ||
            isMoving)
        {
            return;
        }

        selectedAttackTarget = null;

        // 先にコマンドを閉じる。
        // Hideでグリッド入力が有効になる。
        commandPanel.Hide();

        currentStep =
            SelectionStep.SelectingAttackTarget;

        gridDebugView.ClearMovementRange();

        attackPreviewPanel.Show();

        // UIのEnter/Aボタンがそのまま
        // グリッド決定に流れないようにする
        suppressCellSelectionUntilFrame =
            Time.frameCount + 1;

        Debug.Log(
            $"{selectedUnit.name}: " +
            "攻撃対象の候補選択開始"
        );
    }

    /// <summary>
    /// 指定マスにいる敵を候補として選ぶ。
    ///
    /// 注意：
    /// 正式な攻撃射程・範囲判定は未実装。
    /// 射程外の敵でも候補として表示する。
    /// ここではHPも行動状態も変更しない。
    /// </summary>
    private void SelectAttackTargetCandidate(
        Vector2Int position)
    {
        if (selectedUnit == null)
            return;

        UnitView target =
            FindUnitAt(position);

        if (target == null ||
            target.State == null ||
            target.State.Team != UnitTeam.Enemy)
        {
            selectedAttackTarget = null;

            attackPreviewPanel.ShowSearching();

            Debug.Log(
                "攻撃対象の候補として敵を選んでください。"
            );

            return;
        }

        selectedAttackTarget = target;

        // 敵候補の情報をUIに表示
        attackPreviewPanel.ShowCandidate(
            selectedUnit,
            selectedAttackTarget
        );

        Debug.Log(
            $"攻撃対象の候補：" +
            $"{selectedUnit.name} → " +
            $"{selectedAttackTarget.name}" +
            "（射程未判定・攻撃未実行）"
        );
    }

    /// <summary>
    /// 攻撃対象選択を中止し、
    /// 元のコマンドメニューへ戻る。
    /// </summary>
    private void CancelAttackTargetSelection()
    {
        selectedAttackTarget = null;

        attackPreviewPanel.Hide();

        currentStep =
            SelectionStep.CommandMenu;

        // コマンドに戻るときに
        // グリッド入力も停止する
        commandPanel.Show();

        Debug.Log(
            "攻撃対象選択をキャンセルしました。"
        );
    }

    // ========================================
    // 待機
    // ========================================

    /// <summary>
    /// 待機を選んだときに行動終了する。
    /// </summary>
    private void OnWaitRequested()
    {
        if (currentStep !=
                SelectionStep.CommandMenu ||
            selectedUnit == null ||
            isMoving)
        {
            return;
        }

        selectedUnit.State.CompleteAction();

        Debug.Log(
            $"{selectedUnit.name}: 待機・行動終了"
        );

        DeselectUnit();
    }

    // ========================================
    // キャンセル
    // ========================================

    /// <summary>
    /// 操作を一段階戻す。
    ///
    /// 攻撃対象選択 → コマンド
    /// コマンド → 移動前
    /// 移動先選択 → 選択解除
    /// </summary>
    private void CancelCurrentStep()
    {
        if (isMoving)
            return;

        // 攻撃対象選択中
        if (currentStep ==
            SelectionStep.SelectingAttackTarget)
        {
            CancelAttackTargetSelection();
            return;
        }

        // コマンド選択中
        if (currentStep ==
            SelectionStep.CommandMenu)
        {
            commandPanel.Hide();

            if (selectedUnit != null &&
                selectedUnit.State.HasMoved &&
                movePath.Count > 1)
            {
                StartCoroutine(
                    UndoMoveRoutine()
                );
            }
            else
            {
                // 移動していなければ
                // 移動先選択に戻る
                currentStep =
                    SelectionStep.SelectingDestination;

                ShowSelectedUnitMovementRange();
            }

            return;
        }

        // 移動先選択中
        if (currentStep ==
            SelectionStep.SelectingDestination)
        {
            DeselectUnit();
        }
    }

    /// <summary>
    /// 移動経路を逆順にたどり、
    /// 移動前のマスへ戻る。
    /// </summary>
    private IEnumerator UndoMoveRoutine()
    {
        if (selectedUnit == null)
            yield break;

        isMoving = true;

        gridInput.SetGridInputEnabled(false);

        UnitView unit =
            selectedUnit;

        List<Vector2Int> reversePath =
            new List<Vector2Int>(movePath);

        reversePath.Reverse();

        yield return StartCoroutine(
            unit.MoveAlongPath(
                grid,
                reversePath
            )
        );

        unit.State.UndoMove(
            originalPosition
        );

        unit.SyncPosition(grid);

        movePath.Clear();

        selectedAttackTarget = null;

        currentStep =
            SelectionStep.SelectingDestination;

        ShowSelectedUnitMovementRange();

        isMoving = false;

        gridInput.SetGridInputEnabled(true);

        Debug.Log(
            $"{unit.name}: 移動を取り消しました。"
        );
    }

    // ========================================
    // 選択解除・ターン処理
    // ========================================

    /// <summary>
    /// 操作中の情報を初期化する。
    /// </summary>
    private void DeselectUnit()
    {
        attackPreviewPanel.Hide();
        commandPanel.Hide();

        selectedUnit = null;
        selectedAttackTarget = null;

        reachableCells.Clear();
        movePath.Clear();

        currentStep = SelectionStep.None;

        gridDebugView.ClearMovementRange();
    }

    /// <summary>
    /// 開発用：味方の行動状態をリセット。
    ///
    /// 通常はTurnManagerがResetActionを呼ぶ。
    /// </summary>
    public void ResetAllyActions()
    {
        if (isMoving ||
            currentStep != SelectionStep.None)
        {
            return;
        }

        DeselectUnit();

        foreach (UnitView unit in activeUnits)
        {
            if (unit == null ||
                unit.State == null)
            {
                continue;
            }

            if (unit.State.Team == UnitTeam.Ally)
            {
                unit.State.ResetAction();
            }
        }

        Debug.Log(
            "味方の行動状態をリセットしました。"
        );
    }
}
