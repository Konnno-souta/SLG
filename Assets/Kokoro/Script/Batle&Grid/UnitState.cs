
using UnityEngine;

/// <summary>
/// キャラクターの所属。
/// </summary>
public enum UnitTeam
{
    Ally,
    Enemy
}

/// <summary>
/// 戦闘中のユニット状態。
/// MonoBehaviourではないデータクラス。
/// </summary>
public sealed class UnitState
{
    public UnitTeam Team { get; }

    // 現在のマス座標
    public Vector2Int Position { get; private set; }

    // 移動できる最大マス数
    public int MoveRange { get; }

    // このフェーズですでに移動したか
    public bool HasMoved { get; private set; }

    public UnitState(
        UnitTeam team,
        Vector2Int position,
        int moveRange)
    {
        Team = team;
        Position = position;
        MoveRange = Mathf.Max(0, moveRange);

        HasMoved = false;
    }

    /// <summary>
    /// 移動を完了する。
    /// </summary>
    public void CompleteMove(Vector2Int destination)
    {
        Position = destination;
        HasMoved = true;
    }

    /// <summary>
    /// 新しいフェーズに向けて行動をリセット。
    /// </summary>
    public void ResetAction()
    {
        HasMoved = false;
    }
}
