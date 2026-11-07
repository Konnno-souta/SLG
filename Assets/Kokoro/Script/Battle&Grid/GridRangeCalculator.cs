
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// グリッド上の移動可能範囲を計算する。
/// </summary>
public static class GridRangeCalculator
{
    // 上下左右の4方向
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    /// <summary>
    /// 移動可能なマスの集合を返す。
    /// </summary>
    public static HashSet<Vector2Int> Calculate(
        BattleGrid grid,
        Vector2Int start,
        int moveRange,
        Func<Vector2Int, bool> isOccupied)
    {
        HashSet<Vector2Int> reachable = new();

        if (!grid.IsWalkable(start))
            return reachable;

        Queue<Vector2Int> queue = new();

        // マスごとに開始地点からの距離を保持
        Dictionary<Vector2Int, int> distances = new();

        queue.Enqueue(start);
        distances.Add(start, 0);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            int currentDistance = distances[current];

            // 移動力を使い切った場合
            if (currentDistance >= moveRange)
                continue;

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;

                // すでに調べたマス
                if (distances.ContainsKey(next))
                    continue;

                // 通行できない地形
                if (!grid.IsWalkable(next))
                    continue;

                // ほかのキャラクターがいる
                if (isOccupied != null &&
                    isOccupied(next))
                    continue;

                int nextDistance = currentDistance + 1;

                distances.Add(next, nextDistance);
                reachable.Add(next);
                queue.Enqueue(next);
            }
        }

        return reachable;
    }
}
