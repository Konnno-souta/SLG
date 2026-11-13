using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(BattleGrid))]
public sealed class GridDebugView : MonoBehaviour
{
    [Header("マスの色(A=不透明度。透過表示オンのとき有効)")]
    [SerializeField]
    private Color lightColor =
        new Color(1f, 1f, 1f, 0.05f);

    [SerializeField]
    private Color darkColor =
        new Color(0f, 0f, 0f, 0.25f);

    [SerializeField]
    private Color selectedColor = Color.yellow;

    [Header("表示設定")]
    [Tooltip("オン: 地形が透けて見える半透明マス / オフ: 従来の不透明マス")]
    [SerializeField]
    private bool transparent = true;

    [Tooltip("マス用マテリアル。空なら自動生成(透過表示オン時)。ビルドに含める場合はここに設定推奨")]
    [SerializeField]
    private Material tileMaterial;

    [Tooltip("マスの塗り幅(1=隙間なし、小さいほどマスの間に地面が見える)")]
    [SerializeField, Range(0.5f, 1f)]
    private float fillRatio = 0.95f;

    [Tooltip("マスの厚み(m)")]
    [SerializeField, Min(0.001f)]
    private float thickness = 0.02f;

    [Tooltip("マス上面の高さ(m)。地形と重なってちらつくときは少し上げる")]
    [SerializeField]
    private float heightOffset = 0f;

    [Tooltip("マスが影を落とすか")]
    [SerializeField]
    private bool castShadows = false;

    private BattleGrid grid;
    private Material runtimeMaterial;

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

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    /// <summary>
    /// テスト用のマスを生成する。
    /// </summary>
    private void GenerateGrid()
    {
        float size = grid.CellSize;
        float tileThickness = Mathf.Min(thickness, size * 0.1f);
        Material material = ResolveTileMaterial();

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

                // 上面が heightOffset の高さに来るように置く
                tile.transform.localPosition =
                    new Vector3(
                        (x + 0.5f) * size,
                        heightOffset - tileThickness * 0.5f,
                        (y + 0.5f) * size
                    );

                tile.transform.localScale =
                    new Vector3(
                        size * fillRatio,
                        tileThickness,
                        size * fillRatio
                    );

                // 平面でクリックするのでCollider不要
                Collider col = tile.GetComponent<Collider>();

                if (col != null)
                    Destroy(col);

                // GridCellViewが色を設定する前にマテリアルを差し替える
                MeshRenderer meshRenderer =
                    tile.GetComponent<MeshRenderer>();

                if (material != null)
                    meshRenderer.sharedMaterial = material;

                meshRenderer.shadowCastingMode =
                    castShadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;

                meshRenderer.receiveShadows = !transparent;

                GridCellView view =
                    tile.AddComponent<GridCellView>();

                Color color =
                    (x + y) % 2 == 0
                    ? lightColor
                    : darkColor;

                // 不透明表示のときはAを無視して従来どおりに見せる
                if (!transparent)
                    color.a = 1f;

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
    /// マスに使うマテリアルを決める。
    /// 1. Inspectorで指定されていればそれ
    /// 2. 透過表示オンなら半透明のUnlitマテリアルを自動生成
    /// 3. それ以外はnull(Cubeの標準マテリアルのまま=従来の見た目)
    /// </summary>
    private Material ResolveTileMaterial()
    {
        if (tileMaterial != null)
            return tileMaterial;

        if (!transparent)
            return null;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogWarning(
                "GridDebugView: URPのUnlitシェーダーが見つかりません。" +
                "Tile Materialに半透明マテリアルを設定してください。",
                this);
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "GridCell_Transparent(Runtime)"
        };

        // URP Unlit を「Surface Type: Transparent / Blending: Alpha」にする
        runtimeMaterial.SetFloat("_Surface", 1f);
        runtimeMaterial.SetFloat("_Blend", 0f);
        runtimeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        runtimeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        runtimeMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        runtimeMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        runtimeMaterial.SetFloat("_ZWrite", 0f);
        runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        runtimeMaterial.SetOverrideTag("RenderType", "Transparent");
        runtimeMaterial.SetShaderPassEnabled("ShadowCaster", false);
        runtimeMaterial.SetShaderPassEnabled("DepthOnly", false);
        runtimeMaterial.renderQueue = (int)RenderQueue.Transparent;

        return runtimeMaterial;
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