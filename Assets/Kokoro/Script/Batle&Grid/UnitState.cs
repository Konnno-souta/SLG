
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
/// 戦闘中のユニットの状態。
///
/// ・現在位置
/// ・移動力
/// ・行動状態
/// ・戦闘ステータス
///
/// を保持する。
/// </summary>
public sealed class UnitState
{
    // ================================
    // 基本情報
    // ================================

    public UnitTeam Team { get; }

    public Vector2Int Position { get; private set; }

    /// <summary>
    /// 現在のクラスの最大移動範囲。
    /// クラスチェンジ時に更新可能。
    /// </summary>
    public int MoveRange { get; private set; }

    // ================================
    // 行動状態
    // ================================

    public bool HasMoved { get; private set; }
    public bool HasActed { get; private set; }

    // ================================
    // 戦闘ステータス
    // ================================

    public UnitBattleStats CombatStats
    {
        get;
        private set;
    }

    // ================================
    // 初期化
    // ================================

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

        CombatStats = null;
    }

    // ================================
    // 移動力の更新（今回追加）
    // ================================

    /// <summary>
    /// Class.csvの最大移動範囲を反映する。
    /// </summary>
    public void SetMoveRange(int newMoveRange)
    {
        MoveRange = Mathf.Max(0, newMoveRange);
    }

    // ================================
    // 戦闘ステータス登録
    // ================================

    public bool TrySetCombatStats(
        UnitBattleStats stats)
    {
        if (stats == null)
            return false;

        // 登録済みのデータを上書きしない
        if (CombatStats != null)
            return false;

        CombatStats = stats;

        // ステータス生成直後に
        // クラスの移動力を適用する。
        SetMoveRange(stats.MoveRange);

        return true;
    }

    // ================================
    // 移動
    // ================================

    /// <summary>
    /// 移動完了。
    /// この時点では行動終了にはならない。
    /// </summary>
    public void CompleteMove(Vector2Int destination)
    {
        if (HasActed)
            return;

        Position = destination;
        HasMoved = true;
    }

    /// <summary>
    /// 移動をキャンセルして元の位置へ戻す。
    /// </summary>
    public void UndoMove(Vector2Int originalPosition)
    {
        if (HasActed)
            return;

        Position = originalPosition;
        HasMoved = false;
    }

    // ================================
    // 行動
    // ================================

    /// <summary>
    /// 待機・攻撃などによる行動終了。
    /// </summary>
    public void CompleteAction()
    {
        HasActed = true;
    }

    /// <summary>
    /// 次の自分のフェーズ開始時に使用。
    ///
    /// HP・移動力・戦闘ステータスは
    /// リセットしない。
    /// </summary>
    public void ResetAction()
    {
        HasMoved = false;
        HasActed = false;
    }
}
