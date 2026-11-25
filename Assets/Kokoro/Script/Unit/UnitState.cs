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
/// </summary>
public sealed class UnitState
{
    public UnitTeam Team { get; }

    public Vector2Int Position
    {
        get;
        private set;
    }

    // 補正前の移動力
    public int BaseMoveRange
    {
        get;
        private set;
    }

    // スキル補正後の移動力
    public int MoveRange =>
        CombatStats == null
            ? BaseMoveRange
            : CombatStats.Skills.GetModifier(
                SkillStat.MoveBlocked) > 0f
                ? 0
                : Mathf.Max(
                    0,
                    BaseMoveRange +
                    Mathf.RoundToInt(
                        CombatStats.Skills.GetModifier(
                            SkillStat.MoveRange)));

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

        BaseMoveRange = Mathf.Max(0, moveRange);

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
        BaseMoveRange = Mathf.Max(
            0,
            newMoveRange
        );
    }

    // ========================================
    // 通常移動
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
    // スキルによる強制移動
    // ========================================

    /// <summary>
    /// 行動済み・移動済みでも座標を変更する。
    /// 行動状態は変更しない。
    /// </summary>
    public void SetForcedPosition(
        Vector2Int destination)
    {
        Position = destination;
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

        BaseMoveRange = moveRange;

        CombatStats.Skills.Clear();

        HasMoved = hasMoved;
        HasActed = hasActed;

        return true;
    }
}