
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// グリッド上の最短経路を計算する。
/// 現在は上下左右4方向・移動コスト1を想定。
/// </summary>
public static class GridPathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    /// <summary>
    /// 最短経路を取得する。
    /// 成功時は開始地点と目的地を含む経路を返す。
    /// </summary>
    public static bool TryFindPath(
        BattleGrid grid,
        Vector2Int start,
        Vector2Int goal,
        int maxSteps,
        Func<Vector2Int, bool> isOccupied,
        out List<Vector2Int> path)
    {
        path = new List<Vector2Int>();

        if (grid == null ||
            maxSteps <= 0 ||
            start == goal)
            return false;

        // 開始地点と目的地を検証
        if (!grid.IsWalkable(start) ||
            !grid.IsWalkable(goal))
            return false;

        // 目的地に別のユニットがいる
        if (isOccupied != null &&
            isOccupied(goal))
            return false;

        Queue<Vector2Int> queue = new();

        // 各マスに到達するまでの歩数
        Dictionary<Vector2Int, int> distances = new();

        // 経路復元用：どのマスから来たか
        Dictionary<Vector2Int, Vector2Int> previous =
            new();

        queue.Enqueue(start);
        distances.Add(start, 0);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            // 目的地に到着
            if (current == goal)
            {
                path = BuildPath(
                    previous,
                    start,
                    goal
                );

                return true;
            }

            int currentDistance = distances[current];

            // 移動力の上限に到達
            if (currentDistance >= maxSteps)
                continue;

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;

                // すでに探索したマス
                if (distances.ContainsKey(next))
                    continue;

                // 通れない地形
                if (!grid.IsWalkable(next))
                    continue;

                // ほかのユニットがいる
                if (isOccupied != null &&
                    isOccupied(next))
                    continue;

                distances.Add(
                    next,
                    currentDistance + 1
                );

                previous.Add(next, current);

                queue.Enqueue(next);
            }
        }

        // 経路が見つからなかった
        return false;
    }

    /// <summary>
    /// 探索結果を開始地点から目的地の順に復元。
    /// </summary>
    private static List<Vector2Int> BuildPath(
        Dictionary<Vector2Int, Vector2Int> previous,
        Vector2Int start,
        Vector2Int goal)
    {
        List<Vector2Int> path = new();

        Vector2Int current = goal;

        path.Add(current);

        // 目的地から開始地点へたどる
        while (current != start)
        {
            current = previous[current];
            path.Add(current);
        }

        // 開始地点→目的地の順にする
        path.Reverse();

        return path;
    }
}
