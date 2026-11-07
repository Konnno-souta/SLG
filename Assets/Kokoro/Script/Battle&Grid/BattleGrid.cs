using UnityEngine;

/// <summary>
/// グリッド座標は0始まり。StageConfig.csvに従うサイズへ変更可能。
/// 初期化はBattleStageLoader.Awakeから行う。
/// </summary>
public sealed class BattleGrid : MonoBehaviour
{
    [Header("ステージ読込なしのときの開発用サイズ")]
    [SerializeField, Min(1)] private int width = 8;
    [SerializeField, Min(1)] private int height = 8;
    [SerializeField, Min(0.1f)] private float cellSize = 1f;
    private GridCellData[,] cells;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        // BattleStageLoader.Awakeが先に呼ばれていても上書きしない。
        if (cells == null) Initialize(width, height);
    }

    public void Initialize(int newWidth, int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0)
        {
            Debug.LogError("BattleGrid: 縦横は1以上にしてください。", this);
            return;
        }

        width = newWidth;
        height = newHeight;
        cells = new GridCellData[width, height];
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            cells[x, y] = new GridCellData(new Vector2Int(x, y));
    }

    public bool IsInside(Vector2Int position) =>
        position.x >= 0 && position.x < width &&
        position.y >= 0 && position.y < height;

    public bool TryGetCell(Vector2Int position, out GridCellData cell)
    {
        cell = null;
        if (!IsInside(position) || cells == null) return false;
        cell = cells[position.x, position.y];
        return true;
    }

    public bool IsWalkable(Vector2Int position) =>
        TryGetCell(position, out GridCellData cell) && cell.IsWalkable;

    public bool SetWalkable(Vector2Int position, bool walkable)
    {
        if (!TryGetCell(position, out GridCellData cell)) return false;
        cell.SetWalkable(walkable);
        return true;
    }

    public Vector3 CellToWorld(Vector2Int position) =>
        transform.TransformPoint(new Vector3(
            (position.x + 0.5f) * cellSize,
            0f,
            (position.y + 0.5f) * cellSize));

    public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int position)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        position = new Vector2Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.z / cellSize));
        return IsInside(position);
    }
}
