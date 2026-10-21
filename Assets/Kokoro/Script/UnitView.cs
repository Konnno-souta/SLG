
using UnityEngine;

/// <summary>
/// ユニットの3D表示を管理する。
/// </summary>
public sealed class UnitView : MonoBehaviour
{
    [Header("仮のユニット設定")]
    [SerializeField]
    private UnitTeam team = UnitTeam.Ally;

    [SerializeField]
    private Vector2Int initialPosition =
        new Vector2Int(1, 1);

    [SerializeField, Min(0)]
    private int moveRange = 3;

    public Vector2Int InitialPosition => initialPosition;

    // 実際の戦闘中の状態
    public UnitState State { get; private set; }

    /// <summary>
    /// グリッド情報を使って初期化する。
    /// </summary>
    public bool Initialize(BattleGrid grid)
    {
        if (!grid.IsWalkable(initialPosition))
        {
            Debug.LogError(
                $"{name}: 初期配置マスが無効です。",
                this
            );

            return false;
        }

        State = new UnitState(
            team,
            initialPosition,
            moveRange
        );

        SyncPosition(grid);

        return true;
    }

    /// <summary>
    /// UnitStateの位置を3Dモデルに反映する。
    /// </summary>
    public void SyncPosition(BattleGrid grid)
    {
        if (State == null)
            return;

        transform.position =
            grid.CellToWorld(State.Position);
    }
}
