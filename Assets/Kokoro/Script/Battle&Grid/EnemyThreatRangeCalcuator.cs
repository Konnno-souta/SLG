
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第13段階：敵の危険範囲計算。
///
/// 敵の危険範囲は以下の合計とする。
///
/// 1. 敵が移動できるマス
/// 2. 移動しない場合の攻撃可能マス
/// 3. 各移動先からの攻撃可能マス
///
/// 複数の敵が同じマスを危険範囲に含めても
/// HashSetによって重複を除外する。
///
/// ※ 今回は実際の敵AIの移動先を
///    予測するものではない。
/// </summary>
public static class EnemyThreatRangeCalculator
{
    /// <summary>
    /// 全敵の危険範囲を計算する。
    ///
    /// grid:
    ///     現在の戦闘グリッド
    ///
    /// units:
    ///     味方と敵を含む全ユニット
    ///
    /// 戻り値:
    ///     危険範囲のグリッド座標一覧
    /// </summary>
    public static HashSet<Vector2Int> Calculate(
        BattleGrid grid,
        IEnumerable<UnitView> units)
    {
        HashSet<Vector2Int> dangerCells =
            new HashSet<Vector2Int>();

        if (grid == null || units == null)
        {
            return dangerCells;
        }

        // ======================================
        // 1. 有効なユニットを収集
        // ======================================

        List<UnitView> validUnits =
            new List<UnitView>();

        HashSet<Vector2Int> occupiedCells =
            new HashSet<Vector2Int>();

        foreach (UnitView unit in units)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy ||
                unit.State == null)
            {
                continue;
            }

            UnitState state = unit.State;

            // グリッド外は対象にしない。
            if (!grid.IsInside(state.Position))
            {
                continue;
            }

            UnitBattleStats stats =
                state.CombatStats;

            // 戦闘不能ユニットは除外する。
            if (stats != null && stats.IsDead)
            {
                continue;
            }

            validUnits.Add(unit);

            // 全ユニットの現在位置を記録。
            // 移動範囲の占有判定に使用する。
            occupiedCells.Add(state.Position);
        }

        // ======================================
        // 2. 敵1体ずつ危険範囲を計算
        // ======================================

        foreach (UnitView enemy in validUnits)
        {
            UnitState enemyState =
                enemy.State;

            // 味方の移動・攻撃範囲は含めない。
            if (enemyState.Team != UnitTeam.Enemy)
            {
                continue;
            }

            UnitBattleStats enemyStats =
                enemyState.CombatStats;

            // 第8段階のステータス初期化前なら
            // 攻撃形状が分からないのでスキップ。
            if (enemyStats == null)
            {
                continue;
            }

            Vector2Int enemyPosition =
                enemyState.Position;

            int moveRange =
                enemyState.MoveRange;

            // ==================================
            // 3. 敵の移動可能範囲を計算
            // ==================================

            HashSet<Vector2Int> movableCells =
                GridRangeCalculator.Calculate(
                    grid,
                    enemyPosition,
                    moveRange,

                    // ほかのユニットがいるマスは
                    // 移動先・通過マスとして使わない。
                    //
                    // 自分の現在位置だけ例外。
                    position =>
                        occupiedCells.Contains(position) &&
                        position != enemyPosition
                );

            // 移動しない攻撃も考慮するため、
            // 必ず現在位置を追加する。
            movableCells.Add(enemyPosition);

            // ==================================
            // 4. 移動可能マスを危険範囲に追加
            // ==================================

            foreach (Vector2Int moveCell in movableCells)
            {
                if (!grid.IsInside(moveCell))
                {
                    continue;
                }

                dangerCells.Add(moveCell);

                // ==============================
                // 5. 移動先からの攻撃範囲
                // ==============================

                HashSet<Vector2Int> attackCells =
                    AttackRangeCalculator.Calculate(
                        grid,
                        moveCell,
                        enemyStats
                    );

                foreach (Vector2Int attackCell
                    in attackCells)
                {
                    if (grid.IsInside(attackCell))
                    {
                        dangerCells.Add(attackCell);
                    }
                }
            }
        }

        return dangerCells;
    }
}
