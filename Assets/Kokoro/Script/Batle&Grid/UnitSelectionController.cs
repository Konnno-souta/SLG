
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 味方ユニットの選択・移動・コマンド管理。
///
/// 第12段階では正式CSVの攻撃範囲に対応。
///
/// 機能：
/// ・味方選択
/// ・青い移動可能範囲表示
/// ・経路探索による移動
/// ・移動キャンセル
/// ・攻撃対象選択
/// ・赤い攻撃可能範囲表示
/// ・射程内の敵だけを候補として判定
/// ・待機による行動終了
///
/// ダメージ・命中・攻撃演出は未実装。
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

    [Header("コマンドUI")]
    [SerializeField]
    private BattleCommandPanel commandPanel;

    [SerializeField]
    private AttackTargetPreviewPanel attackPreviewPanel;

    [Header("攻撃範囲表示（今回追加）")]
    [SerializeField]
    private AttackRangeOverlay attackRangeOverlay;

    [Header("手動配置用ユニット")]
    [SerializeField]
    private List<UnitView> units =
        new List<UnitView>();

    [Header("開発用")]
    [SerializeField]
    private bool enableDebugResetKey = false;

    // ====================================
    // 操作状態
    // ====================================

    private enum SelectionStep
    {
        None,
        SelectingDestination,
        CommandMenu,
        SelectingAttackTarget
    }

    private SelectionStep currentStep =
        SelectionStep.None;

    // 登録済みユニット
    private readonly List<UnitView> activeUnits =
        new List<UnitView>();

    // 操作中の味方
    private UnitView selectedUnit;

    // 選択中の敵候補
    private UnitView selectedAttackTarget;

    // 移動可能範囲
    private HashSet<Vector2Int> reachableCells =
        new HashSet<Vector2Int>();

    // 攻撃可能範囲
    private HashSet<Vector2Int> attackableCells =
        new HashSet<Vector2Int>();

    // 移動前の位置
    private Vector2Int originalPosition;

    // 移動した経路
    private List<Vector2Int> movePath =
        new List<Vector2Int>();

    // 移動アニメーション中か
    private bool isMoving;

    // UIの決定ボタンがグリッド側で
    // 二重処理されることを防ぐ
    private int suppressCellSelectionUntilFrame = -1;


    /// <summary>
    /// セーブデータを復元してもよい操作状態か。
    ///
    /// ユニットの選択中や移動中は
    /// ロードを禁止する。
    /// </summary>
    public bool CanRestoreBattleState =>
        currentStep == SelectionStep.None &&
        !isMoving;


    // ====================================
    // 初期化
    // ====================================

    private void Start()
    {
        if (grid == null ||
            gridDebugView == null ||
            gridInput == null ||
            commandPanel == null ||
            attackPreviewPanel == null ||
            attackRangeOverlay == null)
        {
            Debug.LogError(
                "UnitSelectionControllerの参照が" +
                "不足しています。",
                this
            );

            enabled = false;
            return;
        }

        InitializeManualUnits();

        gridInput.CellSelected += OnCellSelected;

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
        {
            return;
        }

        HandleCancelInput();
        HandleDebugInput();
    }

    // ====================================
    // キャンセル入力
    // ====================================

    /// <summary>
    /// Esc / 右クリック / ○・Bボタン。
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
    /// 開発用Rキー。
    /// 通常はOFFにする。
    /// </summary>
    private void HandleDebugInput()
    {
        if (!enableDebugResetKey)
        {
            return;
        }

        if (Keyboard.current == null ||
            !Keyboard.current.rKey.wasPressedThisFrame)
        {
            return;
        }

        if (currentStep != SelectionStep.None)
        {
            return;
        }

        ResetAllyActions();
    }

    // ====================================
    // ユニット登録
    // ====================================

    /// <summary>
    /// 手動配置ユニットの初期登録。
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
    /// BattleUnitSpawnerから呼ぶ。
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
    /// 指定マスにいるユニットを返す。
    /// </summary>
    private UnitView FindUnitAt(Vector2Int position)
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

    // ====================================
    // マス決定
    // ====================================

    private void OnCellSelected(Vector2Int position)
    {
        if (isMoving)
        {
            return;
        }

        // UIから攻撃対象選択に切り替えた直後の
        // 二重決定を防ぐ
        if (Time.frameCount <=
            suppressCellSelectionUntilFrame)
        {
            return;
        }

        // 攻撃対象選択中
        if (currentStep ==
            SelectionStep.SelectingAttackTarget)
        {
            SelectAttackTargetCandidate(position);
            return;
        }

        // コマンドが開いている間は
        // グリッド側の入力を受け付けない
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

        // 移動先を選択
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

    // ====================================
    // 味方選択
    // ====================================

    private void HandleUnitSelection(UnitView clicked)
    {
        UnitState state = clicked.State;

        if (state == null)
        {
            return;
        }

        // 敵や行動終了済みの味方は操作しない
        if (state.Team != UnitTeam.Ally ||
            state.HasActed ||
            state.HasMoved)
        {
            DeselectUnit();
            return;
        }

        // 同じ味方をもう一度決定した場合、
        // 移動せずコマンドを表示する
        if (selectedUnit == clicked)
        {
            OpenCommandMenu();
            return;
        }

        SelectUnit(clicked);
    }

    private void SelectUnit(UnitView unit)
    {
        selectedUnit = unit;
        selectedAttackTarget = null;

        originalPosition =
            unit.State.Position;

        movePath.Clear();
        attackableCells.Clear();

        attackRangeOverlay.Clear();

        currentStep =
            SelectionStep.SelectingDestination;

        ShowSelectedUnitMovementRange();

        Debug.Log(
            $"{unit.name}を選択 / " +
            $"移動力：{unit.State.MoveRange}"
        );
    }

    // ====================================
    // 移動可能範囲
    // ====================================

    private void ShowSelectedUnitMovementRange()
    {
        if (selectedUnit == null)
        {
            return;
        }

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

    // ====================================
    // 移動処理
    // ====================================

    private void TryMoveSelectedUnit(
        Vector2Int destination)
    {
        if (selectedUnit == null || isMoving)
        {
            return;
        }

        UnitState state =
            selectedUnit.State;

        if (state.HasActed || state.HasMoved)
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
                destination
            );

            return;
        }

        originalPosition = state.Position;
        movePath = new List<Vector2Int>(path);

        StartCoroutine(
            MoveUnitRoutine(
                selectedUnit,
                destination,
                path
            )
        );
    }

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

    // ====================================
    // コマンドメニュー
    // ====================================

    private void OpenCommandMenu()
    {
        if (selectedUnit == null || isMoving)
        {
            return;
        }

        currentStep = SelectionStep.CommandMenu;

        gridDebugView.ClearMovementRange();

        // 前回の攻撃範囲表示を消す
        attackableCells.Clear();
        attackRangeOverlay.Clear();

        selectedAttackTarget = null;

        attackPreviewPanel.Hide();
        commandPanel.Show();

        Debug.Log(
            $"{selectedUnit.name}: コマンド選択"
        );
    }

    // ====================================
    // 攻撃対象選択（今回の追加部分）
    // ====================================

    /// <summary>
    /// 攻撃コマンドが押された。
    ///
    /// 現在のクラスから攻撃可能マスを計算し、
    /// 赤い枠を表示する。
    /// </summary>
    private void OnAttackRequested()
    {
        if (currentStep != SelectionStep.CommandMenu ||
            selectedUnit == null ||
            selectedUnit.State == null ||
            isMoving)
        {
            return;
        }

        selectedAttackTarget = null;

        // 以前の攻撃可能範囲を消す
        attackableCells.Clear();
        attackRangeOverlay.Clear();

        UnitBattleStats stats =
            selectedUnit.State.CombatStats;

        if (stats == null)
        {
            Debug.LogWarning(
                $"{selectedUnit.name}: " +
                "戦闘ステータスが初期化されていません。"
            );

            return;
        }

        // =================================
        // 攻撃可能マスを計算する
        // =================================

        attackableCells =
            AttackRangeCalculator.Calculate(
                grid,
                selectedUnit.State.Position,
                stats
            );

        // =================================
        // コマンドを閉じて攻撃選択へ
        // =================================

        commandPanel.Hide();

        currentStep =
            SelectionStep.SelectingAttackTarget;

        gridDebugView.ClearMovementRange();

        // 赤い攻撃範囲を表示
        attackRangeOverlay.ShowRange(
            attackableCells
        );

        attackPreviewPanel.Show();

        // 攻撃可能マスがない場合は通知
        if (attackableCells.Count == 0)
        {
            attackPreviewPanel.ShowNoRange(
                stats.CurrentClassName,
                stats.RangeType
            );
        }

        // 同じフレームの決定入力を無視
        suppressCellSelectionUntilFrame =
            Time.frameCount + 1;

        Debug.Log(
            $"【攻撃範囲】{stats.CharacterName}" +
            $" / {stats.CurrentClassName}" +
            $" / {stats.RangeType}" +
            $" / {attackableCells.Count}マス"
        );
    }

    /// <summary>
    /// 攻撃対象候補の選択。
    ///
    /// 赤い範囲内にいる敵だけ有効。
    ///
    /// 今回は情報表示まで。
    /// ダメージも行動終了も行わない。
    /// </summary>
    private void SelectAttackTargetCandidate(
        Vector2Int position)
    {
        if (selectedUnit == null)
        {
            return;
        }

        UnitView target =
            FindUnitAt(position);

        // 空白マスや味方を選んだ場合
        if (target == null ||
            target.State == null ||
            target.State.Team != UnitTeam.Enemy)
        {
            selectedAttackTarget = null;

            attackPreviewPanel.ShowSearching();

            return;
        }

        // =================================
        // 攻撃可能範囲の判定
        // =================================

        bool isInRange =
            AttackRangeCalculator.Contains(
                attackableCells,
                position
            );

        if (!isInRange)
        {
            selectedAttackTarget = null;

            attackPreviewPanel.ShowOutOfRange(target);

            Debug.Log(
                $"{target.name}は攻撃範囲外です。"
            );

            return;
        }

        // =================================
        // 範囲内の敵として選択する
        // =================================

        selectedAttackTarget = target;

        attackPreviewPanel.ShowCandidate(
            selectedUnit,
            selectedAttackTarget
        );

        Debug.Log(
            "【攻撃対象】" +
            $"{selectedUnit.name} → " +
            $"{selectedAttackTarget.name}" +
            "（射程内・攻撃実行は未実装）"
        );
    }

    /// <summary>
    /// 攻撃対象の選択をキャンセルする。
    /// </summary>
    private void CancelAttackTargetSelection()
    {
        selectedAttackTarget = null;

        attackableCells.Clear();

        // 赤い攻撃範囲を消す
        attackRangeOverlay.Clear();

        // 対象選択UIを消す
        attackPreviewPanel.Hide();

        currentStep = SelectionStep.CommandMenu;

        // 元のコマンドUIへ戻る
        commandPanel.Show();

        Debug.Log(
            "攻撃対象選択をキャンセルしました。"
        );
    }

    // ====================================
    // 待機
    // ====================================

    private void OnWaitRequested()
    {
        if (currentStep != SelectionStep.CommandMenu ||
            selectedUnit == null ||
            isMoving)
        {
            return;
        }

        // 待機したときだけ行動終了
        selectedUnit.State.CompleteAction();

        Debug.Log(
            $"{selectedUnit.name}: 待機・行動終了"
        );

        DeselectUnit();
    }

    // ====================================
    // キャンセル
    // ====================================

    /// <summary>
    /// Esc / 右クリックで一段階戻る。
    /// </summary>
    private void CancelCurrentStep()
    {
        if (isMoving)
        {
            return;
        }

        // 攻撃対象選択からコマンドへ戻る
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
                currentStep =
                    SelectionStep.SelectingDestination;

                ShowSelectedUnitMovementRange();
            }

            return;
        }

        // 移動先を選択中
        if (currentStep ==
            SelectionStep.SelectingDestination)
        {
            DeselectUnit();
        }
    }

    /// <summary>
    /// 移動経路を逆にたどって元のマスへ戻る。
    /// </summary>
    private IEnumerator UndoMoveRoutine()
    {
        if (selectedUnit == null)
        {
            yield break;
        }

        isMoving = true;

        gridInput.SetGridInputEnabled(false);

        UnitView unit = selectedUnit;

        List<Vector2Int> reversePath =
            new List<Vector2Int>(movePath);

        reversePath.Reverse();

        yield return StartCoroutine(
            unit.MoveAlongPath(
                grid,
                reversePath
            )
        );

        // UnitStateの座標を元へ戻す
        unit.State.UndoMove(
            originalPosition
        );

        // 3Dモデルの位置を補正
        unit.SyncPosition(grid);

        movePath.Clear();

        currentStep =
            SelectionStep.SelectingDestination;

        ShowSelectedUnitMovementRange();

        isMoving = false;

        gridInput.SetGridInputEnabled(true);

        Debug.Log(
            $"{unit.name}: 移動をキャンセルしました。"
        );
    }

    // ====================================
    // 選択解除
    // ====================================

    private void DeselectUnit()
    {
        // 攻撃範囲・コマンド表示を消す
        attackRangeOverlay.Clear();
        attackPreviewPanel.Hide();
        commandPanel.Hide();

        selectedUnit = null;
        selectedAttackTarget = null;

        reachableCells.Clear();
        attackableCells.Clear();
        movePath.Clear();

        currentStep = SelectionStep.None;

        gridDebugView.ClearMovementRange();
    }

    // ====================================
    // 開発用リセット
    // ====================================

    /// <summary>
    /// 味方の行動をリセットする。
    /// 通常はTurnManagerが管理する。
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
