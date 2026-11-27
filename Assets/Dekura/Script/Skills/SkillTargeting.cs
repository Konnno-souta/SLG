using System;
using System.Collections.Generic;
using UnityEngine;

// 範囲・対象の共通ヘルパー
public static class SkillTargeting
{
    // origin から距離 min〜max のマス(マンハッタン距離)
    // TODO: 射程の形はプランナーに確認
    public static HashSet<Vector2Int> CellsInRange(
        BattleGrid grid, Vector2Int origin, int min, int max)
    {
        var result = new HashSet<Vector2Int>();

        for (int dx = -max; dx <= max; dx++)
        for (int dy = -max; dy <= max; dy++)
        {
            int d = Mathf.Abs(dx) + Mathf.Abs(dy);
            if (d < min || d > max) continue;

            var c = origin + new Vector2Int(dx, dy);

            // 通行不可マスを除外(範囲外判定がBattleGridにあればそちらが正確)
            if (grid.IsWalkable(c)) result.Add(c);
        }

        return result;
    }

    // 中心から半径 radius(3x3 なら 1)の正方形内のマス
    public static HashSet<Vector2Int> SquareCells(
        BattleGrid grid, Vector2Int center, int radius)
    {
        var result = new HashSet<Vector2Int>();

        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            var c = center + new Vector2Int(dx, dy);
            if (grid.IsWalkable(c)) result.Add(c);
        }

        return result;
    }

    // 中心から半径 radius の正方形内のユニット
    public static List<UnitView> InSquare(
        IReadOnlyList<UnitView> units, Vector2Int center, int radius,
        Func<UnitView, bool> filter)
    {
        var list = new List<UnitView>();

        foreach (var u in units)
        {
            if (!SkillBattleAPI.IsAlive(u) || !filter(u)) continue;

            var p = u.State.Position;

            if (Mathf.Abs(p.x - center.x) <= radius &&
                Mathf.Abs(p.y - center.y) <= radius)
            {
                list.Add(u);
            }
        }

        return list;
    }

    public static int Distance(UnitView a, UnitView b) =>
        Mathf.Abs(a.State.Position.x - b.State.Position.x) +
        Mathf.Abs(a.State.Position.y - b.State.Position.y);
}
