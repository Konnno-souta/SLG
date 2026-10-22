using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ユニットの選択・移動。既存の手動配置とCSVからの自動生成の両方に対応。
/// </summary>
public sealed class UnitSelectionController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private BattleGrid grid;
    [SerializeField] private GridDebugView gridDebugView;
    [SerializeField] private GridClickInput gridInput;

    [Header("手動配置用：CSV生成を使う場合はSize=0")]
    [SerializeField] private List<UnitView> units = new();

    [Header("開発用")]
    [SerializeField] private bool enableDebugResetKey = true;

    private readonly List<UnitView> activeUnits = new();
    private UnitView selectedUnit;
    private HashSet<Vector2Int> reachableCells = new();
    private bool isMoving;

    private void Start()
    {
        if (grid == null || gridDebugView == null || gridInput == null)
        {
            Debug.LogError("UnitSelectionControllerの参照が不足しています。", this);
            enabled = false;
            return;
        }

        InitializeManualUnits();
        gridInput.CellSelected += OnCellSelected;
    }

    private void OnDestroy()
    {
        if (gridInput != null)
            gridInput.CellSelected -= OnCellSelected;
    }

    private void Update()
    {
        if (!isMoving && enableDebugResetKey &&
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            ResetAllyActions();
    }

    private void InitializeManualUnits()
    {
        foreach (UnitView unit in units)
        {
            if (unit == null) continue;
            RegisterSpawnedUnit(unit);
        }
    }

    /// <summary>
    /// 自動生成したUnitViewを戦闘に登録する。
    /// Spawner.StartとこのコンポーネントのStartの順序に依存しない。
    /// </summary>
    public bool RegisterSpawnedUnit(UnitView unit)
    {
        if (grid == null || unit == null || activeUnits.Contains(unit))
            return false;

        Vector2Int position = unit.InitialPosition;
        if (FindUnitAt(position) != null)
        {
            Debug.LogError($"{unit.name}: 他のユニットと配置座標が重複しています。", unit);
            return false;
        }

        if (!unit.Initialize(grid))
            return false;

        activeUnits.Add(unit);
        return true;
    }

    private UnitView FindUnitAt(Vector2Int position)
    {
        foreach (UnitView unit in activeUnits)
        {
            if (unit != null && unit.State != null && unit.State.Position == position)
                return unit;
        }
        return null;
    }

    private bool IsOccupied(Vector2Int position) => FindUnitAt(position) != null;

    private void OnCellSelected(Vector2Int position)
    {
        if (isMoving) return;

        UnitView clicked = FindUnitAt(position);
        if (clicked != null)
        {
            if (clicked.State.Team == UnitTeam.Ally && !clicked.State.HasMoved)
            {
                if (selectedUnit == clicked) DeselectUnit();
                else SelectUnit(clicked);
            }
            else DeselectUnit();
            return;
        }

        if (selectedUnit != null && reachableCells.Contains(position))
        {
            MoveSelectedUnit(position);
            return;
        }
        DeselectUnit();
    }

    private void SelectUnit(UnitView unit)
    {
        selectedUnit = unit;
        UnitState state = unit.State;
        reachableCells = GridRangeCalculator.Calculate(
            grid, state.Position, state.MoveRange, IsOccupied);
        gridDebugView.ShowMovementRange(reachableCells);
        Debug.Log($"{unit.name}を選択 / 移動力={state.MoveRange}", unit);
    }

    private void MoveSelectedUnit(Vector2Int destination)
    {
        if (selectedUnit == null || isMoving || selectedUnit.State.HasMoved)
            return;
        if (!reachableCells.Contains(destination) ||
            !grid.IsWalkable(destination) || IsOccupied(destination))
            return;

        bool found = GridPathfinder.TryFindPath(
            grid, selectedUnit.State.Position, destination,
            selectedUnit.State.MoveRange, IsOccupied,
            out List<Vector2Int> path);
        if (!found)
        {
            Debug.LogWarning($"移動経路が見つかりません: {destination}");
            return;
        }
        StartCoroutine(MoveUnitRoutine(selectedUnit, destination, path));
    }

    private IEnumerator MoveUnitRoutine(
        UnitView unit, Vector2Int destination, List<Vector2Int> path)
    {
        isMoving = true;
        gridInput.SetGridInputEnabled(false);
        gridDebugView.ClearMovementRange();

        yield return StartCoroutine(unit.MoveAlongPath(grid, path));
        unit.State.CompleteMove(destination);
        Debug.Log($"{unit.name} 移動完了: {destination}");

        DeselectUnit();
        isMoving = false;
        gridInput.SetGridInputEnabled(true);
    }

    private void DeselectUnit()
    {
        selectedUnit = null;
        reachableCells.Clear();
        gridDebugView.ClearMovementRange();
    }

    public void ResetAllyActions()
    {
        if (isMoving) return;
        DeselectUnit();
        foreach (UnitView unit in activeUnits)
        {
            if (unit != null && unit.State != null && unit.State.Team == UnitTeam.Ally)
                unit.State.ResetAction();
        }
        Debug.Log("味方の行動状態をリセットしました。");
    }
}
