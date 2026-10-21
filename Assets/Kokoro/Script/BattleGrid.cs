
using UnityEngine;

/// <summary>
/// 戦闘マップのグリッドを管理する。
/// 見た目のマップとは独立させる。
/// </summary>
public sealed class BattleGrid : MonoBehaviour
{
    [Header("グリッド設定")]
    [SerializeField, Min(1)]
    private int width = 8;

    [SerializeField, Min(1)]
    private int height = 8;

    [SerializeField, Min(0.1f)]
    private float cellSize = 1f;

    // グリッドの情報
    private GridCellData[,] cells;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        InitializeGrid();
    }

    /// <summary>
    /// マスのデータを初期化する。
    /// </summary>
    private void InitializeGrid()
    {
        cells = new GridCellData[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int position =
                    new Vector2Int(x, y);

                cells[x, y] =
                    new GridCellData(position);
            }
        }
    }

    /// <summary>
    /// 指定した座標がグリッド内にあるか。
    /// </summary>
    public bool IsInside(Vector2Int position)
    {
        return position.x >= 0 &&
               position.x < width &&
               position.y >= 0 &&
               position.y < height;
    }

    /// <summary>
    /// マスのデータを取得する。
    /// </summary>
    public bool TryGetCell(
        Vector2Int position,
        out GridCellData cell)
    {
        cell = null;

        if (!IsInside(position) || cells == null)
            return false;

        cell = cells[position.x, position.y];
        return true;
    }

    /// <summary>
    /// マスが通行可能か確認する。
    /// </summary>
    public bool IsWalkable(Vector2Int position)
    {
        return TryGetCell(position, out var cell)
               && cell.IsWalkable;
    }

    /// <summary>
    /// マスの通行可否を設定する。
    /// </summary>
    public bool SetWalkable(
        Vector2Int position,
        bool walkable)
    {
        if (!TryGetCell(position, out var cell))
            return false;

        cell.SetWalkable(walkable);
        return true;
    }

    /// <summary>
    /// マス座標を3D空間の中心座標へ変換する。
    /// </summary>
    public Vector3 CellToWorld(Vector2Int position)
    {
        Vector3 localPosition = new Vector3(
            (position.x + 0.5f) * cellSize,
            0f,
            (position.y + 0.5f) * cellSize
        );

        return transform.TransformPoint(localPosition);
    }

    /// <summary>
    /// 3D空間の座標をマス座標に変換する。
    /// </summary>
    public bool TryWorldToCell(
        Vector3 worldPosition,
        out Vector2Int position)
    {
        Vector3 localPosition =
            transform.InverseTransformPoint(worldPosition);

        int x = Mathf.FloorToInt(
            localPosition.x / cellSize
        );

        int y = Mathf.FloorToInt(
            localPosition.z / cellSize
        );

        position = new Vector2Int(x, y);

        return IsInside(position);
    }
}
