
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BattleGrid))]
public sealed class GridDebugView : MonoBehaviour
{
    [Header("マスの色")]
    [SerializeField]
    private Color lightColor =
        new Color(0.65f, 0.75f, 0.65f);

    [SerializeField]
    private Color darkColor =
        new Color(0.35f, 0.50f, 0.35f);

    [SerializeField]
    private Color selectedColor = Color.yellow;

    private BattleGrid grid;

    private readonly Dictionary<Vector2Int, GridCellView>
        tileViews = new();

    private Vector2Int? selectedPosition;

    private void Awake()
    {
        grid = GetComponent<BattleGrid>();
    }

    private void Start()
    {
        GenerateGrid();
    }

    /// <summary>
    /// テスト用のマスを生成する。
    /// </summary>
    private void GenerateGrid()
    {
        float size = grid.CellSize;
        float thickness = Mathf.Min(0.08f, size * 0.1f);

        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                Vector2Int position =
                    new Vector2Int(x, y);

                GameObject tile =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube
                    );

                tile.name = $"GridCell_{x}_{y}";
                tile.transform.SetParent(transform, false);

                tile.transform.localPosition =
                    new Vector3(
                        (x + 0.5f) * size,
                        -thickness * 0.5f,
                        (y + 0.5f) * size
                    );

                tile.transform.localScale =
                    new Vector3(
                        size * 0.95f,
                        thickness,
                        size * 0.95f
                    );

                // 平面でクリックするのでCollider不要
                Collider col = tile.GetComponent<Collider>();

                if (col != null)
                    Destroy(col);

                GridCellView view =
                    tile.AddComponent<GridCellView>();

                Color color =
                    (x + y) % 2 == 0
                    ? lightColor
                    : darkColor;

                view.Initialize(
                    position,
                    color,
                    selectedColor
                );

                tileViews.Add(position, view);
            }
        }
    }

    /// <summary>
    /// カーソルの選択マスを変更する。
    /// </summary>
    public void SetSelectedCell(Vector2Int position)
    {
        if (selectedPosition.HasValue &&
            tileViews.TryGetValue(
                selectedPosition.Value,
                out var previous))
        {
            previous.SetSelected(false);
        }

        if (tileViews.TryGetValue(position, out var next))
        {
            next.SetSelected(true);
            selectedPosition = position;
        }
        else
        {
            selectedPosition = null;
        }
    }

    /// <summary>
    /// 移動可能範囲を表示する。
    /// </summary>
    public void ShowMovementRange(
        IEnumerable<Vector2Int> positions)
    {
        ClearMovementRange();

        foreach (Vector2Int position in positions)
        {
            if (tileViews.TryGetValue(
                position, out var view))
            {
                view.SetMovable(true);
            }
        }
    }

    /// <summary>
    /// 移動可能範囲を消す。
    /// </summary>
    public void ClearMovementRange()
    {
        foreach (GridCellView view in tileViews.Values)
        {
            view.SetMovable(false);
        }
    }
}
