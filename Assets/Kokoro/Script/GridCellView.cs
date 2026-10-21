
using UnityEngine;

/// <summary>
/// 1マスの見た目を管理する。
/// </summary>
[RequireComponent(typeof(Renderer))]
public sealed class GridCellView : MonoBehaviour
{
    public Vector2Int Position { get; private set; }

    private Renderer cellRenderer;
    private MaterialPropertyBlock propertyBlock;

    private Color normalColor;
    private Color selectedColor;

    // 移動可能なマスの色
    private Color movableColor =
        new Color(0.2f, 0.6f, 1f);

    private bool isSelected;
    private bool isMovable;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    public void Initialize(
        Vector2Int position,
        Color normal,
        Color selected)
    {
        Position = position;

        normalColor = normal;
        selectedColor = selected;

        cellRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        UpdateColor();
    }

    /// <summary>
    /// カーソルによる選択状態。
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateColor();
    }

    /// <summary>
    /// 移動可能範囲の表示。
    /// </summary>
    public void SetMovable(bool movable)
    {
        isMovable = movable;
        UpdateColor();
    }

    /// <summary>
    /// 状態に応じてマスの色を変える。
    /// </summary>
    private void UpdateColor()
    {
        Color color = normalColor;

        if (isMovable)
            color = movableColor;

        // カーソルの色を最優先
        if (isSelected)
            color = selectedColor;

        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);

        cellRenderer.SetPropertyBlock(propertyBlock);
    }
}
