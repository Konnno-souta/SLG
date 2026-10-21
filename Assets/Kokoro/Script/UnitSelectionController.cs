
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ユニットの選択と移動操作を管理する。
/// </summary>
public sealed class UnitSelectionController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private BattleGrid grid;

    [SerializeField]
    private GridDebugView gridDebugView;

    [SerializeField]
    private GridClickInput gridInput;

    [Header("配置するユニット")]
    [SerializeField]
    private List<UnitView> units = new();

    [Header("開発用")]
    [SerializeField]
    private bool enableDebugResetKey = true;

    // 正常に初期化されたユニット
    private readonly List<UnitView> activeUnits = new();

    private UnitView selectedUnit;

    private HashSet<Vector2Int> reachableCells = new();

    private void Start()
    {
        if (grid == null ||
            gridDebugView == null ||
            gridInput == null)
        {
            Debug.LogError(
                "UnitSelectionControllerの参照が不足しています。",
                this
            );

            enabled = false;
            return;
        }

        InitializeUnits();

        // マスの決定を受け取る
        gridInput.CellSelected += OnCellSelected;
    }

    private void OnDestroy()
    {
        if (gridInput != null)
        {
            gridInput.CellSelected -= OnCellSelected;
        }
    }

    private void Update()
    {
        // 仮のテスト用リセット
        // 正式なターン管理完成後に置き換える
        if (enableDebugResetKey &&
            Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetAllyActions();
        }
    }

    /// <summary>
    /// ユニットをグリッド上に配置する。
    /// </summary>
    private void InitializeUnits()
    {
        HashSet<Vector2Int> usedPositions = new();

        foreach (UnitView unit in units)
        {
            if (unit == null)
                continue;

            Vector2Int position = unit.InitialPosition;

            // 同じマスへの重複配置を防止
            if (!usedPositions.Add(position))
            {
                Debug.LogError(
                    $"{unit.name}: 初期配置が重複しています。",
                    unit
                );

                continue;
            }

            if (unit.Initialize(grid))
            {
                activeUnits.Add(unit);
            }
            else
            {
                usedPositions.Remove(position);
            }
        }
    }

    /// <summary>
    /// 指定マスにいるユニットを探す。
    /// </summary>
    private UnitView FindUnitAt(Vector2Int position)
    {
        foreach (UnitView unit in activeUnits)
        {
            if (unit.State.Position == position)
                return unit;
        }

        return null;
    }

    /// <summary>
    /// そのマスにユニットがいるか。
    /// </summary>
    private bool IsOccupied(Vector2Int position)
    {
        return FindUnitAt(position) != null;
    }

    /// <summary>
    /// マスが決定されたときの処理。
    /// </summary>
    private void OnCellSelected(Vector2Int position)
    {
        UnitView clickedUnit = FindUnitAt(position);

        // ユニットがいるマスを選択した場合
        if (clickedUnit != null)
        {
            if (clickedUnit.State.Team == UnitTeam.Ally &&
                !clickedUnit.State.HasMoved)
            {
                if (selectedUnit == clickedUnit)
                {
                    DeselectUnit();
                }
                else
                {
                    SelectUnit(clickedUnit);
                }
            }
            else
            {
                DeselectUnit();
            }

            return;
        }

        // 選択中の味方が移動できるマスか
        if (selectedUnit != null &&
            reachableCells.Contains(position))
        {
            MoveSelectedUnit(position);
            return;
        }

        // 何もできないマスなら選択解除
        DeselectUnit();
    }

    /// <summary>
    /// 味方を選択し、移動可能範囲を表示。
    /// </summary>
    private void SelectUnit(UnitView unit)
    {
        selectedUnit = unit;

        UnitState state = unit.State;

        reachableCells = GridRangeCalculator.Calculate(
            grid,
            state.Position,
            state.MoveRange,
            IsOccupied
        );

        gridDebugView.ShowMovementRange(reachableCells);

        Debug.Log(
            $"{unit.name}を選択しました。" +
            $" 移動力: {state.MoveRange}"
        );
    }

    /// <summary>
    /// 選択中のユニットを移動させる。
    /// 今回は瞬間移動でテストする。
    /// </summary>
    private void MoveSelectedUnit(Vector2Int destination)
    {
        if (selectedUnit == null)
            return;

        UnitState state = selectedUnit.State;

        // 再確認
        if (!reachableCells.Contains(destination) ||
            !grid.IsWalkable(destination) ||
            IsOccupied(destination))
            return;

        state.CompleteMove(destination);

        // 3Dモデルの位置を更新
        selectedUnit.SyncPosition(grid);

        Debug.Log(
            $"{selectedUnit.name}が" +
            $"{destination}へ移動しました。"
        );

        DeselectUnit();
    }

    /// <summary>
    /// キャラの選択解除。
    /// </summary>
    private void DeselectUnit()
    {
        selectedUnit = null;
        reachableCells.Clear();

        gridDebugView.ClearMovementRange();
    }

    /// <summary>
    /// 味方全員の移動済み状態をリセット。
    /// 後でTurnManagerから呼び出す。
    /// </summary>
    public void ResetAllyActions()
    {
        DeselectUnit();

        foreach (UnitView unit in activeUnits)
        {
            if (unit.State.Team == UnitTeam.Ally)
            {
                unit.State.ResetAction();
            }
        }

        Debug.Log("味方の行動状態をリセットしました。");
    }
}
