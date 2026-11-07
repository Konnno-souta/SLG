
using UnityEngine;

/// <summary>
/// 1マス分の論理データ。
/// 3Dモデルや見た目には依存しない。
/// </summary>
public sealed class GridCellData
{
    // マスの座標
    public Vector2Int Position { get; }

    // このマスを通行できるか
    public bool IsWalkable { get; private set; }

    public GridCellData(Vector2Int position)
    {
        Position = position;
        IsWalkable = true;
    }

    /// <summary>
    /// マスの通行可否を変更する。
    /// </summary>
    public void SetWalkable(bool walkable)
    {
        IsWalkable = walkable;
    }
}
