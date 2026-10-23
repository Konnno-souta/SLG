
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 攻撃可能マスを赤い枠で表示する。
///
/// ・LineRendererで四角い枠を作る
/// ・Colliderは作らない
/// ・既存GridDebugViewには干渉しない
/// ・一度作った枠は再利用する
///
/// 20×30マスのステージでも、
/// 攻撃候補マスの数だけ表示する。
/// </summary>
public sealed class AttackRangeOverlay : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private BattleGrid grid;

    [Header("表示色")]
    [SerializeField]
    private Color outlineColor =
        new Color(1f, 0.15f, 0.15f, 1f);

    [Header("線の太さ")]
    [SerializeField, Min(0.01f)]
    private float lineWidth = 0.07f;

    [Header("地面からの高さ")]
    [SerializeField, Min(0f)]
    private float heightOffset = 0.15f;

    [Header("表示用Material（任意）")]
    [SerializeField]
    private Material customMaterial;

    // 再利用するLineRenderer
    private readonly List<LineRenderer> markerPool =
        new List<LineRenderer>();

    // 自動生成したMaterial
    private Material generatedMaterial;

    private void Awake()
    {
        if (grid == null)
        {
            Debug.LogError(
                "AttackRangeOverlay: " +
                "BattleGridが設定されていません。",
                this
            );
        }
    }

    // ====================================
    // 攻撃範囲を表示
    // ====================================

    /// <summary>
    /// 指定されたマスに赤い枠を表示する。
    /// </summary>
    public void ShowRange(
        IEnumerable<Vector2Int> cells)
    {
        Clear();

        if (grid == null || cells == null)
        {
            return;
        }

        int index = 0;

        foreach (Vector2Int cell in cells)
        {
            if (!grid.IsInside(cell))
            {
                continue;
            }

            LineRenderer marker =
                GetOrCreateMarker(index);

            if (marker == null)
            {
                continue;
            }

            marker.gameObject.SetActive(true);

            DrawCellOutline(marker, cell);

            index++;
        }
    }

    // ====================================
    // 表示を消す
    // ====================================

    /// <summary>
    /// 攻撃範囲を非表示にする。
    ///
    /// GameObjectはDestroyしないので、
    /// 次に表示するとき再利用できる。
    /// </summary>
    public void Clear()
    {
        foreach (LineRenderer marker in markerPool)
        {
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }
        }
    }

    // ====================================
    // 表示枠の生成と再利用
    // ====================================

    private LineRenderer GetOrCreateMarker(
        int index)
    {
        if (index < markerPool.Count)
        {
            return markerPool[index];
        }

        Material material = GetDisplayMaterial();

        if (material == null)
        {
            return null;
        }

        GameObject markerObject =
            new GameObject(
                $"AttackRangeMarker_{index}"
            );

        markerObject.transform.SetParent(
            transform,
            false
        );

        LineRenderer line =
            markerObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;

        // 4つの頂点をつなぎ四角形にする
        line.positionCount = 4;
        line.loop = true;

        line.widthMultiplier = lineWidth;

        line.startColor = outlineColor;
        line.endColor = outlineColor;

        line.sharedMaterial = material;

        line.shadowCastingMode =
            ShadowCastingMode.Off;

        line.receiveShadows = false;

        line.numCornerVertices = 0;
        line.numCapVertices = 0;

        markerPool.Add(line);

        return line;
    }

    /// <summary>
    /// 使用するMaterialを取得する。
    /// </summary>
    private Material GetDisplayMaterial()
    {
        if (customMaterial != null)
        {
            return customMaterial;
        }

        if (generatedMaterial != null)
        {
            return generatedMaterial;
        }

        // 可能ならスプライト用のShaderを使用
        Shader shader =
            Shader.Find("Sprites/Default");

        // 使用できない場合の代替
        if (shader == null)
        {
            shader = Shader.Find(
                "Universal Render Pipeline/Unlit"
            );
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            Debug.LogError(
                "AttackRangeOverlay: " +
                "表示用Shaderが見つかりません。",
                this
            );

            return null;
        }

        generatedMaterial = new Material(shader);

        generatedMaterial.name =
            "Runtime_AttackRangeMaterial";

        // Shader側でも赤い色を設定する
        if (generatedMaterial.HasProperty("_BaseColor"))
        {
            generatedMaterial.SetColor(
                "_BaseColor",
                outlineColor
            );
        }

        if (generatedMaterial.HasProperty("_Color"))
        {
            generatedMaterial.SetColor(
                "_Color",
                outlineColor
            );
        }

        return generatedMaterial;
    }

    // ====================================
    // 1マス分の赤い枠
    // ====================================

    private void DrawCellOutline(
        LineRenderer line,
        Vector2Int cell)
    {
        Vector3 center =
            grid.CellToWorld(cell);

        float half =
            grid.CellSize * 0.45f;

        // グリッドの向きを考慮する
        Vector3 right =
            grid.transform.TransformVector(
                new Vector3(half, 0f, 0f)
            );

        Vector3 forward =
            grid.transform.TransformVector(
                new Vector3(0f, 0f, half)
            );

        Vector3 up =
            grid.transform.up * heightOffset;

        center += up;

        // 四隅を時計回りに登録
        line.SetPosition(
            0,
            center - right - forward
        );

        line.SetPosition(
            1,
            center + right - forward
        );

        line.SetPosition(
            2,
            center + right + forward
        );

        line.SetPosition(
            3,
            center - right + forward
        );

        line.startColor = outlineColor;
        line.endColor = outlineColor;
        line.widthMultiplier = lineWidth;
    }

    // ====================================
    // 終了時の片付け
    // ====================================

    private void OnDestroy()
    {
        // 自動生成Materialだけ破棄する。
        // Inspectorから指定されたMaterialは
        // 共有アセットなので破棄しない。
        if (generatedMaterial != null)
        {
            Destroy(generatedMaterial);
        }
    }
}
