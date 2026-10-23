
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻撃可能マスの計算。
///
/// Class.csvのRangeTypeと
/// AttackType_Rule.csvのルールに対応。
///
/// 現段階では攻撃範囲の形だけを計算する。
///
/// 未実装：
/// ・壁による攻撃遮断
/// ・高低差
/// ・攻撃後の特殊移動
/// ・命中判定
/// ・ダメージ計算
/// </summary>
public static class AttackRangeCalculator
{
    /// <summary>
    /// 攻撃可能なグリッド座標を取得する。
    /// </summary>
    public static HashSet<Vector2Int> Calculate(
        BattleGrid grid,
        Vector2Int origin,
        UnitBattleStats stats)
    {
        HashSet<Vector2Int> result =
            new HashSet<Vector2Int>();

        if (grid == null || stats == null)
        {
            return result;
        }

        if (!grid.IsInside(origin))
        {
            Debug.LogWarning(
                "AttackRangeCalculator: " +
                "攻撃側の座標がグリッド外です。"
            );

            return result;
        }

        string rangeType =
            stats.RangeType == null
                ? ""
                : stats.RangeType.Trim();

        switch (rangeType.ToLowerInvariant())
        {
            // =================================
            // 上下左右の隣接4マス
            // =================================

            case "braver":
            case "trident":
            case "spartan":
            case "phalanx":
            case "cavalier":

                AddCardinalCells(
                    grid,
                    origin,
                    stats,
                    result
                );

                break;

            // =================================
            // 斜めに隣接する4マス
            // =================================

            case "artemis":

                AddDiagonalCells(
                    grid,
                    origin,
                    stats,
                    result
                );

                break;

            // =================================
            // 上下左右＋斜めの隣接8マス
            // =================================

            case "light sorcerer":
            case "dark wizard":

                AddCardinalCells(
                    grid,
                    origin,
                    stats,
                    result
                );

                AddDiagonalCells(
                    grid,
                    origin,
                    stats,
                    result
                );

                break;

            // =================================
            // 未定義の攻撃タイプ
            // =================================

            default:

                Debug.LogWarning(
                    "未対応のRangeTypeです：" +
                    $"'{rangeType}' / " +
                    $"クラス：{stats.CurrentClassName}"
                );

                break;
        }

        return result;
    }

    /// <summary>
    /// 上下左右の隣接マスを追加。
    /// </summary>
    private static void AddCardinalCells(
        BattleGrid grid,
        Vector2Int origin,
        UnitBattleStats stats,
        HashSet<Vector2Int> result)
    {
        AddCandidate(
            grid,
            origin,
            new Vector2Int(1, 0),
            stats,
            result
        );

        AddCandidate(
            grid,
            origin,
            new Vector2Int(-1, 0),
            stats,
            result
        );

        AddCandidate(
            grid,
            origin,
            new Vector2Int(0, 1),
            stats,
            result
        );

        AddCandidate(
            grid,
            origin,
            new Vector2Int(0, -1),
            stats,
            result
        );
    }

    /// <summary>
    /// 斜めに隣接する4マスを追加。
    /// </summary>
    private static void AddDiagonalCells(
        BattleGrid grid,
        Vector2Int origin,
        UnitBattleStats stats,
        HashSet<Vector2Int> result)
    {
        AddCandidate(
            grid,
            origin,
            new Vector2Int(1, 1),
            stats,
            result
        );

        AddCandidate(
            grid,
            origin,
            new Vector2Int(-1, 1),
            stats,
            result
        );

        AddCandidate(
            grid,
            origin,
            new Vector2Int(1, -1),
            stats,
            result
        );

        AddCandidate(
            grid,
            origin,
            new Vector2Int(-1, -1),
            stats,
            result
        );
    }

    /// <summary>
    /// 候補マスを攻撃範囲に追加する。
    ///
    /// CSVのMinRange_Attackと
    /// MaxRange_Attackも確認する。
    ///
    /// 距離は縦横移動の合計。
    /// 斜め隣接は距離2として扱う。
    /// </summary>
    private static void AddCandidate(
        BattleGrid grid,
        Vector2Int origin,
        Vector2Int offset,
        UnitBattleStats stats,
        HashSet<Vector2Int> result)
    {
        Vector2Int target =
            origin + offset;

        // ステージ外のマスは追加しない
        if (!grid.IsInside(target))
        {
            return;
        }

        int distance =
            Mathf.Abs(offset.x) +
            Mathf.Abs(offset.y);

        // Class.csvの最小・最大射程に従う
        if (distance < stats.MinAttackRange ||
            distance > stats.MaxAttackRange)
        {
            return;
        }

        // 現段階では障害物による遮断をしない。
        // 攻撃範囲の形だけを登録する。
        result.Add(target);
    }

    /// <summary>
    /// 指定座標が攻撃可能範囲に含まれるか。
    /// </summary>
    public static bool Contains(
        HashSet<Vector2Int> range,
        Vector2Int position)
    {
        return range != null &&
               range.Contains(position);
    }
}
