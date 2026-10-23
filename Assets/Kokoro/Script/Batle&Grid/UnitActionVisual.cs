
using UnityEngine;

/// <summary>
/// ユニットの行動終了時の暗転表示。
///
/// 共有Materialを直接変更せず、
/// Rendererごとに見た目を変更する。
/// </summary>
public sealed class UnitActionVisual : MonoBehaviour
{
    [Header("暗転設定")]
    [SerializeField, Range(0.1f, 1f)]
    private float finishedBrightness = 0.45f;

    // ユニットに含まれるRenderer
    private Renderer[] renderers;

    // Materialの色を個別に変更するためのブロック
    private MaterialPropertyBlock propertyBlock;

    private bool hasAppliedState;
    private bool lastDimmed;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private void Awake()
    {
        RefreshRenderers();
    }

    /// <summary>
    /// 子オブジェクトも含めてRendererを取得する。
    /// </summary>
    public void RefreshRenderers()
    {
        renderers =
            GetComponentsInChildren<Renderer>(true);

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        // Rendererが変わった場合は再反映できるようにする
        hasAppliedState = false;
    }

    /// <summary>
    /// trueなら暗く、falseなら元の色に戻す。
    /// </summary>
    public void SetDimmed(bool dimmed)
    {
        // 同じ状態なら処理しない
        if (hasAppliedState &&
            lastDimmed == dimmed)
        {
            return;
        }

        if (renderers == null)
        {
            RefreshRenderers();
        }

        float brightness =
            dimmed ? finishedBrightness : 1f;

        foreach (Renderer target in renderers)
        {
            if (target == null)
                continue;

            Material material = target.sharedMaterial;

            if (material == null)
                continue;

            // 他のPropertyBlock設定を維持する
            target.GetPropertyBlock(propertyBlock);

            bool hasColor = false;

            // URP Litなどの_BaseColor
            if (material.HasProperty(BaseColorId))
            {
                Color original =
                    material.GetColor(BaseColorId);

                propertyBlock.SetColor(
                    BaseColorId,
                    MultiplyBrightness(
                        original,
                        brightness
                    )
                );

                hasColor = true;
            }

            // Standard Shaderなどの_Color
            if (material.HasProperty(ColorId))
            {
                Color original =
                    material.GetColor(ColorId);

                propertyBlock.SetColor(
                    ColorId,
                    MultiplyBrightness(
                        original,
                        brightness
                    )
                );

                hasColor = true;
            }

            if (hasColor)
            {
                target.SetPropertyBlock(propertyBlock);
            }
        }

        lastDimmed = dimmed;
        hasAppliedState = true;
    }

    /// <summary>
    /// RGBだけを暗くする。
    /// アルファ値は変更しない。
    /// </summary>
    private static Color MultiplyBrightness(
        Color color,
        float brightness)
    {
        return new Color(
            color.r * brightness,
            color.g * brightness,
            color.b * brightness,
            color.a
        );
    }
}
