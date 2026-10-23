
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
/// ユニットの戦闘中の状態を管理する。
///
/// ・現在位置
/// ・移動済みか
/// ・行動終了済みか
/// ・戦闘ステータス
///
/// を保持する。
/// </summary>
public sealed class UnitState
{
    // =====================================
    // 基本情報
    // =====================================

    // 味方か敵か
    public UnitTeam Team { get; }

    // 現在のグリッド座標
    public Vector2Int Position { get; private set; }

    // 移動可能なマス数
    public int MoveRange { get; }

    // =====================================
    // 行動状態
    // =====================================

    // このフェーズで移動したか
    public bool HasMoved { get; private set; }

    // このフェーズで行動が終了したか
    public bool HasActed { get; private set; }

    // =====================================
    // 戦闘ステータス（今回追加）
    // =====================================

    /// <summary>
    /// CSVから計算した戦闘ステータス。
    ///
    /// BattleStatsInitializerによる初期化前は
    /// nullになる。
    /// </summary>
    public UnitBattleStats CombatStats
    {
        get;
        private set;
    }

    // =====================================
    // 初期化
    // =====================================

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

    // =====================================
    // 戦闘ステータス登録
    // =====================================

    /// <summary>
    /// 計算済みの戦闘ステータスを登録する。
    ///
    /// 一度だけ登録できるようにして、
    /// 誤った再初期化を防ぐ。
    /// </summary>
    public bool TrySetCombatStats(
        UnitBattleStats stats)
    {
        if (stats == null)
        {
            return false;
        }

        if (CombatStats != null)
        {
            return false;
        }

        CombatStats = stats;

        return true;
    }

    // =====================================
    // 移動処理
    // =====================================

    /// <summary>
    /// 移動を完了する。
    /// 移動だけでは行動終了にしない。
    /// </summary>
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

    /// <summary>
    /// 移動をキャンセルする。
    /// 移動前の座標に戻す。
    /// </summary>
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

    // =====================================
    // 行動終了
    // =====================================

    /// <summary>
    /// 攻撃・戦技・待機などで
    /// 行動を終了する。
    /// </summary>
    public void CompleteAction()
    {
        HasActed = true;
    }

    /// <summary>
    /// 新しい自分のフェーズ開始時に
    /// 移動・行動状態をリセットする。
    ///
    /// HPや戦闘ステータスは変更しない。
    /// </summary>
    public void ResetAction()
    {
        HasMoved = false;
        HasActed = false;
    }
}
