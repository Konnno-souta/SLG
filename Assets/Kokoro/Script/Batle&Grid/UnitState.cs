
using UnityEngine;

/// <summary>
/// ユニットの所属。
/// </summary>
public enum UnitTeam
{
    Ally,
    Enemy
}

/// <summary>
/// 戦闘中のユニット状態。
///
/// ・所属チーム
/// ・グリッド位置
/// ・移動力
/// ・移動済み／行動済み
/// ・戦闘ステータス
///
/// 第16段階ではセーブ復元機能を追加。
/// </summary>
public sealed class UnitState
{
    public UnitTeam Team { get; }

    public Vector2Int Position
    {
        get;
        private set;
    }

    public int MoveRange
    {
        get;
        private set;
    }

    public bool HasMoved
    {
        get;
        private set;
    }

    public bool HasActed
    {
        get;
        private set;
    }

    public UnitBattleStats CombatStats
    {
        get;
        private set;
    }

    // ========================================
    // 初期化
    // ========================================

    public UnitState(
        UnitTeam team,
        Vector2Int position,
        int moveRange)
    {
        Team = team;
        Position = position;
        MoveRange = Mathf.Max(0, moveRange);

        HasMoved = false;
        HasActed = false;
    }

    // ========================================
    // 戦闘ステータス登録
    // ========================================

    public bool TrySetCombatStats(
        UnitBattleStats stats)
    {
        if (stats == null ||
            CombatStats != null)
        {
            return false;
        }

        CombatStats = stats;

        SetMoveRange(stats.MoveRange);

        return true;
    }

    // ========================================
    // 移動力
    // ========================================

    public void SetMoveRange(int newMoveRange)
    {
        MoveRange = Mathf.Max(0, newMoveRange);
    }

    // ========================================
    // 通常の移動
    // ========================================

    public void CompleteMove(
        Vector2Int destination)
    {
        if (HasActed)
        {
            return;
        }

        Position = destination;
        HasMoved = true;
    }

    public void UndoMove(
        Vector2Int originalPosition)
    {
        if (HasActed)
        {
            return;
        }

        Position = originalPosition;
        HasMoved = false;
    }

    // ========================================
    // 行動終了
    // ========================================

    public void CompleteAction()
    {
        HasActed = true;
    }

    public void ResetAction()
    {
        HasMoved = false;
        HasActed = false;
    }

    // ========================================
    // 第16段階：セーブデータ復元
    // ========================================

    /// <summary>
    /// 保存したマス・移動力・行動状態を復元。
    ///
    /// 通常のCompleteMove()とは異なり、
    /// HasActedの状態に関係なく復元する。
    ///
    /// 注意：
    /// このメソッドだけでは3Dモデルは動かない。
    /// 復元後にUnitView.SyncPosition()を呼ぶ。
    /// </summary>
    public bool RestoreFromSave(
        Vector2Int position,
        int moveRange,
        bool hasMoved,
        bool hasActed)
    {
        if (CombatStats == null ||
            moveRange < 0)
        {
            return false;
        }

        Position = position;
        MoveRange = moveRange;

        HasMoved = hasMoved;
        HasActed = hasActed;

        return true;
    }
}
