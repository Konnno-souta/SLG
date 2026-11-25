using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("攻撃範囲表示")]
    [SerializeField]
    private AttackRangeOverlay attackRangeOverlay;

    [Header("第25段階：クラス別攻撃エフェクト")]
    [SerializeField]
    private BattleCombatEffectPlayer effectPlayer;

    [Header("手動配置用ユニット")]
    [SerializeField]
    private List<UnitView> units = new List<UnitView>();

    [Header("戦闘カメラ")]
    [SerializeField]
    private BattleCombatCameraController combatCamera;

    [Header("開発用")]
    [SerializeField]
    private bool enableDebugResetKey = false;

    // ========================================
    // 選択状態
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

    private bool isAttacking;

    public bool IsAttacking => isAttacking;

    public bool IsMoving => isMoving;

    public UnitView SelectedUnit => selectedUnit;

    // ========================================
    // スキル共通基盤との接続
    // ========================================

    public bool IsSkillSelectionActive
    {
        get;
        private set;
    }

    public bool CanBeginSkillSelection =>
        enabled &&
        !isMoving &&
        !isAttacking &&
        !IsSkillSelectionActive &&
        currentStep == SelectionStep.CommandMenu &&
        SkillBattleAPI.IsAlive(selectedUnit) &&
        !selectedUnit.State.HasActed;

    public bool TryBeginSkillSelection()
    {
        if (!CanBeginSkillSelection)
        {
            return false;
        }

        IsSkillSelectionActive = true;

        return true;
    }

    public bool TryEndSkillSelection(
        bool succeeded,
        bool completeAction)
    {
        if (!IsSkillSelectionActive)
        {
            return false;
        }

        IsSkillSelectionActive = false;

        if (!SkillBattleAPI.IsAlive(selectedUnit))
        {
            DeselectUnit();

            return true;
        }

        if (succeeded && completeAction)
        {
            SkillBattleAPI.TryCompleteAction(
                selectedUnit
            );

            DeselectUnit();
        }
        else
        {
            if (succeeded)
            {
                // 過去の移動経路で座標を
                // 巻き戻さないようにする。
                movePath.Clear();

                originalPosition =
                    selectedUnit.State.Position;
            }

            currentStep =
                SelectionStep.CommandMenu;

            commandPanel.Show();
        }

        return true;
    }

    private int suppressCellSelectionUntilFrame = -1;

    public bool CanRestoreBattleState =>
        currentStep == SelectionStep.None &&
        !IsSkillSelectionActive &&
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

        gridInput.CellSelected +=
            OnCellSelected;

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
            gridInput.CellSelected -=
                OnCellSelected;
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

    // ========================================
    // 毎フレーム処理
    // ========================================

    private void Update()
    {
        if (IsSkillSelectionActive)
        {
            return;
        }

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

    // ========================================
    // 開発用入力
    // ========================================

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
    // 手動配置ユニット初期化
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

    // ========================================
    // ユニット登録
    // ========================================

    public bool RegisterSpawnedUnit(
        UnitView unit)
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
    // 指定座標のユニット取得
    // ========================================

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

    private bool IsOccupied(
        Vector2Int position)
    {
        return FindUnitAt(position) != null;
    }

    // ========================================
    // マス選択
    // ========================================

    private void OnCellSelected(
        Vector2Int position)
    {
        if (IsSkillSelectionActive)
        {
            return;
        }

        if (isMoving || isAttacking)
        {
            return;
        }

        if (Time.frameCount <=
            suppressCellSelectionUntilFrame)
        {
            return;
        }

        if (currentStep ==
            SelectionStep.SelectingAttackTarget)
        {
            SelectAttackTargetCandidate(
                position
            );

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
            HandleUnitSelection(
                clickedUnit
            );

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
    // ユニット選択判定
    // ========================================

    private void HandleUnitSelection(
        UnitView clicked)
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

        if (selectedUnit == clicked)
        {
            OpenCommandMenu();

            return;
        }

        SelectUnit(clicked);
    }

    // ========================================
    // ユニット選択
    // ========================================

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
    // 移動実行判定
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
                $"移動経路が見つかりません：{destination}"
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

    // ========================================
    // 移動演出
    // ========================================

    private IEnumerator MoveUnitRoutine(
        UnitView unit,
        Vector2Int destination,
        List<Vector2Int> path)
    {
        isMoving = true;

        gridInput.SetGridInputEnabled(false);

        gridDebugView.ClearMovementRange();

        yield return StartCoroutine(
            unit.MoveAlongPath(
                grid,
                path
            )
        );

        unit.State.CompleteMove(
            destination
        );

        isMoving = false;

        gridInput.SetGridInputEnabled(true);

        OpenCommandMenu();
    }

    // ========================================
    // コマンドメニュー表示
    // ========================================

    private void OpenCommandMenu()
    {
        if (selectedUnit == null ||
            isMoving ||
            isAttacking)
        {
            return;
        }

        currentStep =
            SelectionStep.CommandMenu;

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
        if (IsSkillSelectionActive)
        {
            return;
        }

        if (currentStep !=
                SelectionStep.CommandMenu ||
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
    // 攻撃対象選択
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

        // 同じ対象を再選択すると攻撃する。
        if (selectedAttackTarget == target)
        {
            ExecuteSelectedAttack(target);

            return;
        }

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
    // 攻撃実行判定
    // ========================================

    private void ExecuteSelectedAttack(
        UnitView target)
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

        isAttacking = true;

        StartCoroutine(
            ExecuteAttackEffectRoutine(
                selectedUnit,
                target
            )
        );
    }

    // ========================================
    // 攻撃・エフェクト・戦闘カメラ
    // ========================================

    private IEnumerator ExecuteAttackEffectRoutine(
        UnitView attacker,
        UnitView target)
    {
        isAttacking = true;

        gridInput.SetGridInputEnabled(false);

        attackRangeOverlay.Clear();

        attackPreviewPanel.Hide();

        // 1. 戦闘カメラ開始
        if (combatCamera != null)
        {
            yield return combatCamera.EnterCombat(
                attacker,
                target
            );
        }

        // 2. 攻撃エフェクト
        float hitDelay = 0.18f;

        if (effectPlayer != null)
        {
            hitDelay = effectPlayer.PlayAttack(
                attacker,
                target
            );
        }

        // 3. 被弾まで待機
        if (hitDelay > 0f)
        {
            yield return new WaitForSeconds(
                hitDelay
            );
        }

        // 攻撃側または対象が戦闘不能の場合
        if (!BattleUnitLife.IsAlive(attacker) ||
            !BattleUnitLife.IsAlive(target))
        {
            if (combatCamera != null)
            {
                yield return combatCamera.ExitCombat(
                    false
                );
            }

            DeselectUnit();

            isAttacking = false;

            gridInput.SetGridInputEnabled(true);

            yield break;
        }

        // 4. ダメージ処理
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
                yield return combatCamera.ExitCombat(
                    false
                );
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

        // スキルによって被弾対象が
        // 変更された場合にも対応する。
        target =
            result.ActualDefender ?? target;

        // 5. 被弾エフェクト
        if (effectPlayer != null)
        {
            effectPlayer.PlayHit(
                attacker,
                target
            );
        }

        // 6. 戦闘不能判定
        bool isDefeated =
            target.State.CombatStats.IsDead;

        string attackerName =
            attacker.State.CombatStats.CharacterName;

        string targetName =
            target.State.CombatStats.CharacterName;

        BattleUnitDefeatVisual defeatVisual =
            target.GetComponent<
                BattleUnitDefeatVisual>();

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

        // 7. 攻撃結果ログ
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

        // 8. 戦闘カメラ終了
        if (combatCamera != null)
        {
            yield return combatCamera.ExitCombat();
        }

        // 9. 行動終了
        SkillBattleAPI.TryCompleteAction(
            attacker
        );

        DeselectUnit();

        isAttacking = false;

        gridInput.SetGridInputEnabled(true);
    }

    // ========================================
    // 攻撃失敗時の処理
    // ========================================

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
    // 待機コマンド
    // ========================================

    private void OnWaitRequested()
    {
        if (IsSkillSelectionActive)
        {
            return;
        }

        if (currentStep !=
                SelectionStep.CommandMenu ||
            selectedUnit == null ||
            isMoving ||
            isAttacking)
        {
            return;
        }

        SkillBattleAPI.TryCompleteAction(
            selectedUnit
        );

        Debug.Log(
            $"{selectedUnit.name}: 待機・行動終了"
        );

        DeselectUnit();
    }

    // ========================================
    // 選択状態キャンセル
    // ========================================

    private void CancelCurrentStep()
    {
        if (IsSkillSelectionActive)
        {
            return;
        }

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
    // 開発用：味方行動リセット
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