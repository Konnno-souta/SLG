
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 味方ユニットの選択・移動・コマンド管理。
///
/// ・味方選択、移動範囲、経路探索
/// ・移動と移動キャンセル
/// ・攻撃範囲、対象の2回決定
/// ・ダメージ適用、撃破表示、行動終了
/// ・セーブ・ロード時の操作状態確認
///
/// ・現在クラスに応じた攻撃エフェクト
/// ・被弾エフェクト
/// ・攻撃演出中の入力防止
///
/// 命中率・クリティカル判定は未実装。
/// </summary>
public sealed class UnitSelectionController : MonoBehaviour
{
    // ========================================
    // Inspector参照
    // ========================================

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

    [Header("攻撃範囲表示")]
    [SerializeField]
    private AttackRangeOverlay attackRangeOverlay;

    [Header("第25段階：クラス別攻撃エフェクト")]
    [SerializeField]
    private BattleCombatEffectPlayer effectPlayer;

    [Header("手動配置用ユニット")]
    [SerializeField]
    private List<UnitView> units =
        new List<UnitView>();

    [Header("戦闘カメラ")]
    [SerializeField]
    private BattleCombatCameraController combatCamera;

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
        SelectingAttackTarget
    }

    private SelectionStep currentStep =
        SelectionStep.None;

    private readonly List<UnitView> activeUnits =
        new List<UnitView>();

    private UnitView selectedUnit;
    private UnitView selectedAttackTarget;

    private HashSet<Vector2Int> reachableCells =
        new HashSet<Vector2Int>();

    private HashSet<Vector2Int> attackableCells =
        new HashSet<Vector2Int>();

    private Vector2Int originalPosition;

    private List<Vector2Int> movePath =
        new List<Vector2Int>();

    private bool isMoving;

    // 第25段階：攻撃演出中の多重操作防止
    private bool isAttacking;

    public bool IsAttacking => isAttacking;

    private int suppressCellSelectionUntilFrame = -1;

    // ========================================
    // セーブ・ロード用
    // ========================================

    /// <summary>
    /// ユニット未選択・移動なし・攻撃演出なし
    /// のときだけロード可能。
    /// </summary>
    public bool CanRestoreBattleState =>
        currentStep == SelectionStep.None &&
        !isMoving &&
        !isAttacking;

    // ========================================
    // 初期化
    // ========================================

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

        if (effectPlayer == null)
        {
            Debug.LogWarning(
                "攻撃エフェクトの参照がありません。" +
                "ダメージ処理は実行できますが、" +
                "エフェクトは表示されません。",
                this
            );
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
        if (isMoving || isAttacking)
        {
            return;
        }

        HandleCancelInput();
        HandleDebugInput();
    }

    // ========================================
    // キャンセル入力
    // ========================================

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

    // ========================================
    // ユニット登録
    // ========================================

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

    // ========================================
    // ユニット検索
    // ========================================

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

            // HP0のユニットは占有判定から除外。
            UnitBattleStats stats =
                unit.State.CombatStats;

            if (stats != null &&
                stats.CurrentHP <= 0)
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
    // マス決定
    // ========================================

    private void OnCellSelected(Vector2Int position)
    {
        if (isMoving || isAttacking)
        {
            return;
        }

        // UIから攻撃対象選択に切り替えた直後の
        // 二重決定を防ぐ。
        if (Time.frameCount <=
            suppressCellSelectionUntilFrame)
        {
            return;
        }

        if (currentStep ==
            SelectionStep.SelectingAttackTarget)
        {
            SelectAttackTargetCandidate(position);
            return;
        }

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

    // ========================================
    // 味方選択
    // ========================================

    private void HandleUnitSelection(UnitView clicked)
    {
        UnitState state = clicked.State;

        if (state == null)
        {
            return;
        }

        if (state.Team != UnitTeam.Ally ||
            state.HasActed ||
            state.HasMoved)
        {
            DeselectUnit();
            return;
        }

        // 同じ味方を2回決定すると
        // 移動せずコマンドを表示。
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

    // ========================================
    // 移動範囲表示
    // ========================================

    private void ShowSelectedUnitMovementRange()
    {
        if (selectedUnit == null)
        {
            return;
        }

        UnitState state =
            selectedUnit.State;

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
    // 移動処理
    // ========================================

    private void TryMoveSelectedUnit(
        Vector2Int destination)
    {
        if (selectedUnit == null ||
            isMoving ||
            isAttacking)
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
                $"移動経路が見つかりません：{destination}"
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

    // ========================================
    // コマンドメニュー
    // ========================================

    private void OpenCommandMenu()
    {
        if (selectedUnit == null ||
            isMoving ||
            isAttacking)
        {
            return;
        }

        currentStep = SelectionStep.CommandMenu;

        gridDebugView.ClearMovementRange();

        attackableCells.Clear();
        attackRangeOverlay.Clear();

        selectedAttackTarget = null;

        attackPreviewPanel.Hide();
        commandPanel.Show();

        Debug.Log(
            $"{selectedUnit.name}: コマンド選択"
        );
    }

    // ========================================
    // 攻撃コマンド
    // ========================================

    private void OnAttackRequested()
    {
        if (currentStep != SelectionStep.CommandMenu ||
            selectedUnit == null ||
            selectedUnit.State == null ||
            isMoving ||
            isAttacking)
        {
            return;
        }

        selectedAttackTarget = null;

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

        attackableCells =
            AttackRangeCalculator.Calculate(
                grid,
                selectedUnit.State.Position,
                stats
            );

        commandPanel.Hide();

        currentStep =
            SelectionStep.SelectingAttackTarget;

        gridDebugView.ClearMovementRange();

        attackRangeOverlay.ShowRange(
            attackableCells
        );

        attackPreviewPanel.Show();

        if (attackableCells.Count == 0)
        {
            attackPreviewPanel.ShowNoRange(
                stats.CurrentClassName,
                stats.RangeType
            );
        }

        suppressCellSelectionUntilFrame =
            Time.frameCount + 1;

        Debug.Log(
            $"【攻撃範囲】{stats.CharacterName}" +
            $" / {stats.CurrentClassName}" +
            $" / {stats.RangeType}" +
            $" / {attackableCells.Count}マス"
        );
    }

    // ========================================
    // 攻撃対象の選択
    // ========================================

    private void SelectAttackTargetCandidate(
        Vector2Int position)
    {
        if (selectedUnit == null ||
            selectedUnit.State == null ||
            selectedUnit.State.HasActed ||
            isAttacking)
        {
            return;
        }

        UnitView target =
            FindUnitAt(position);

        if (target == null ||
            target.State == null ||
            target.State.Team != UnitTeam.Enemy)
        {
            selectedAttackTarget = null;
            attackPreviewPanel.ShowSearching();
            return;
        }

        bool isInRange =
            AttackRangeCalculator.Contains(
                attackableCells,
                position
            );

        if (!isInRange)
        {
            selectedAttackTarget = null;

            attackPreviewPanel.ShowOutOfRange(
                target
            );

            Debug.Log(
                $"{target.name}は攻撃範囲外です。"
            );
            return;
        }

        // 2回目の決定で攻撃実行。
        if (selectedAttackTarget == target)
        {
            ExecuteSelectedAttack(target);
            return;
        }

        // 1回目の決定で予測ダメージ表示。
        selectedAttackTarget = target;

        attackPreviewPanel.ShowCandidate(
            selectedUnit,
            selectedAttackTarget
        );

        Debug.Log(
            "【攻撃対象を選択】" +
            $"{selectedUnit.name} → " +
            $"{selectedAttackTarget.name}" +
            " / 同じ敵をもう一度決定すると攻撃実行"
        );
    }

    // ========================================
    // 第25段階：攻撃エフェクト
    // ========================================

    /// <summary>
    /// 攻撃可能か事前確認して演出を開始。
    /// </summary>
    private void ExecuteSelectedAttack(UnitView target)
    {
        if (isMoving ||
            isAttacking ||
            selectedUnit == null ||
            target == null ||
            target.State == null ||
            selectedAttackTarget != target ||
            selectedUnit.State == null ||
            selectedUnit.State.HasActed)
        {
            return;
        }

        if (!AttackRangeCalculator.Contains(
            attackableCells,
            target.State.Position))
        {
            return;
        }

        // エフェクト前に攻撃の成立を検証。
        if (!BattleAttackExecutor.TryPreview(
            selectedUnit,
            target,
            out _))
        {
            Debug.LogWarning(
                "攻撃の事前検証に失敗しました。",
                this
            );

            selectedAttackTarget = null;
            attackPreviewPanel.ShowSearching();
            return;
        }

        // 二重攻撃を防ぐ。
        isAttacking = true;

        StartCoroutine(
            ExecuteAttackEffectRoutine(
                selectedUnit,
                target
            )
        );
    }

    /// <summary>
    /// クラス別攻撃エフェクト
    /// → 命中まで待機
    /// → ダメージ適用
    /// → 被弾エフェクト
    /// → 撃破判定
    /// → 行動終了
    /// </summary>
 
private IEnumerator ExecuteAttackEffectRoutine(
    UnitView attacker,
    UnitView target)
{
    isAttacking = true;

    gridInput.SetGridInputEnabled(false);

    attackRangeOverlay.Clear();
    attackPreviewPanel.Hide();

    // ====================================
    // 1. 戦闘カメラへ移動
    // ====================================

    if (combatCamera != null)
    {
        yield return combatCamera.EnterCombat(
            attacker,
            target
        );
    }

        // ====================================
        // 2. 攻撃エフェクト
        // ====================================

        float hitDelay = 0.18f;

        if (effectPlayer != null)
    {
        // 現在のクラスに応じて再生
        hitDelay = effectPlayer.PlayAttack(
            attacker,
            target
        );
    }

    if (hitDelay > 0f)
    {
        yield return new WaitForSeconds(hitDelay);
    }

    // ====================================
    // 3. 生存確認
    // ====================================

    if (!BattleUnitLife.IsAlive(attacker) ||
        !BattleUnitLife.IsAlive(target))
    {
        if (combatCamera != null)
        {
            yield return combatCamera.ExitCombat(false);
        }

        DeselectUnit();

        isAttacking = false;
        gridInput.SetGridInputEnabled(true);

        yield break;
    }

    // ====================================
    // 4. ダメージ・HP減少
    // ====================================

    if (!BattleAttackExecutor.TryExecute(
        attacker,
        target,
        out BattleAttackPreview result))
    {
        Debug.LogWarning(
            "攻撃を実行できませんでした。",
            this
        );

        if (combatCamera != null)
        {
            yield return combatCamera.ExitCombat(false);
        }

        selectedAttackTarget = null;

        attackPreviewPanel.Show();
        attackPreviewPanel.ShowSearching();

        attackRangeOverlay.ShowRange(
            attackableCells
        );

        isAttacking = false;
        gridInput.SetGridInputEnabled(true);

        yield break;
    }

    // ====================================
    // 5. 被弾エフェクト
    // ====================================

    if (effectPlayer != null)
    {
        effectPlayer.PlayHit(
            attacker,
            target
        );
    }

    bool isDefeated =
        result.RemainingHP <= 0;

    string attackerName =
        attacker.State.CombatStats.CharacterName;

    string targetName =
        target.State.CombatStats.CharacterName;

    // ====================================
    // 6. 戦闘不能表示
    // ====================================

    BattleUnitDefeatVisual defeatVisual =
        target.GetComponent<BattleUnitDefeatVisual>();

    if (defeatVisual != null)
    {
        defeatVisual.Refresh();
    }
    else if (isDefeated)
    {
        Debug.LogWarning(
            $"{target.name} に" +
            "BattleUnitDefeatVisualがありません。",
            target
        );
    }

    if (isDefeated)
    {
        Debug.Log(
            $"【敵を撃破】{targetName}",
            target
        );
    }

    // ====================================
    // 7. 戦闘結果
    // ====================================

    Debug.Log(
        "【味方の攻撃成功】" +
        $"\n攻撃側：{attackerName}" +
        $"\n対象：{targetName}" +
        $"\n攻撃種別：{result.DamageType}" +
        $"\n計算ダメージ：{result.CalculatedDamage}" +
        $"\n実際に減ったHP：{result.AppliedDamage}" +
        $"\n残HP：{result.RemainingHP}" +
        $"\n戦闘不能：{isDefeated}",
        this
    );

    // ====================================
    // 8. 元の上俯瞰へ戻る
    // ====================================

    if (combatCamera != null)
    {
        yield return combatCamera.ExitCombat();
    }

    // ====================================
    // 9. 行動終了
    // ====================================

    attacker.State.CompleteAction();

    DeselectUnit();

    isAttacking = false;

    gridInput.SetGridInputEnabled(true);
}

    /// <summary>
    /// ダメージ処理に失敗した場合、
    /// 行動終了せず対象選択へ戻す。
    /// </summary>
    private void FinishFailedAttack()
    {
        selectedAttackTarget = null;
        isAttacking = false;

        if (selectedUnit != null &&
            selectedUnit.State != null &&
            BattleUnitLife.IsAlive(selectedUnit))
        {
            currentStep =
                SelectionStep.SelectingAttackTarget;

            attackPreviewPanel.Show();
            attackPreviewPanel.ShowSearching();

            attackRangeOverlay.ShowRange(
                attackableCells
            );
        }
        else
        {
            DeselectUnit();
        }

        gridInput.SetGridInputEnabled(true);
    }

    // ========================================
    // 攻撃対象選択キャンセル
    // ========================================

    private void CancelAttackTargetSelection()
    {
        selectedAttackTarget = null;

        attackableCells.Clear();
        attackRangeOverlay.Clear();

        attackPreviewPanel.Hide();

        currentStep =
            SelectionStep.CommandMenu;

        commandPanel.Show();

        Debug.Log(
            "攻撃対象選択をキャンセルしました。"
        );
    }

    // ========================================
    // 待機
    // ========================================

    private void OnWaitRequested()
    {
        if (currentStep != SelectionStep.CommandMenu ||
            selectedUnit == null ||
            isMoving ||
            isAttacking)
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

    private void CancelCurrentStep()
    {
        if (isMoving || isAttacking)
        {
            return;
        }

        if (currentStep ==
            SelectionStep.SelectingAttackTarget)
        {
            CancelAttackTargetSelection();
            return;
        }

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

        if (currentStep ==
            SelectionStep.SelectingDestination)
        {
            DeselectUnit();
        }
    }

    // ========================================
    // 移動キャンセル
    // ========================================

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

        unit.State.UndoMove(
            originalPosition
        );

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

    // ========================================
    // 選択解除
    // ========================================

    private void DeselectUnit()
    {
        attackRangeOverlay.Clear();
        attackPreviewPanel.Hide();
        commandPanel.Hide();

        selectedUnit = null;
        selectedAttackTarget = null;

        reachableCells.Clear();
        attackableCells.Clear();
        movePath.Clear();

        currentStep =
            SelectionStep.None;

        gridDebugView.ClearMovementRange();
    }

    // ========================================
    // 開発用リセット
    // ========================================

    public void ResetAllyActions()
    {
        if (isMoving ||
            isAttacking ||
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
