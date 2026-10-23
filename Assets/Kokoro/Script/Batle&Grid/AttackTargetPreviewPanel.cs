
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 攻撃対象の候補選択画面。
///
/// 現段階では以下のみ担当する。
/// ・選択方法の案内
/// ・選択した敵の情報表示
///
/// 攻撃射程判定・ダメージ処理は行わない。
/// </summary>
public sealed class AttackTargetPreviewPanel : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private TMP_Text informationText;

    public bool IsOpen =>
        panelRoot != null &&
        panelRoot.activeSelf;

    private void Awake()
    {
        if (panelRoot == null ||
            informationText == null)
        {
            Debug.LogError(
                "AttackTargetPreviewPanelの参照が不足しています。",
                this
            );

            enabled = false;
            return;
        }

        panelRoot.SetActive(false);
    }

    /// <summary>
    /// 攻撃対象選択モードを表示する。
    /// </summary>
    public void Show()
    {
        if (!enabled || panelRoot == null)
            return;

        panelRoot.SetActive(true);

        ShowSearching();
    }

    /// <summary>
    /// 敵をまだ選択していない状態に戻す。
    /// </summary>
    public void ShowSearching()
    {
        if (!enabled || informationText == null)
            return;

        informationText.text =
            "【攻撃対象の選択】\n\n" +
            "敵を選んでください。\n\n" +
            "左クリック / Enter：候補選択\n" +
            "Esc / 右クリック：戻る\n\n" +
            "※ 射程・攻撃形状は未判定\n" +
            "※ 実際の攻撃はまだ行いません";
    }

    /// <summary>
    /// 選んだ敵の情報を表示する。
    ///
    /// attacker：攻撃する味方
    /// target：対象の敵
    /// </summary>
    public void ShowCandidate(
        UnitView attacker,
        UnitView target)
    {
        if (!enabled ||
            attacker == null ||
            target == null ||
            attacker.State == null ||
            target.State == null)
        {
            return;
        }

        Vector2Int attackerPosition =
            attacker.State.Position;

        Vector2Int targetPosition =
            target.State.Position;

        // 敵との縦横のマス距離。
        // これは表示用であり射程判定ではない。
        int manhattanDistance =
            Mathf.Abs(
                attackerPosition.x - targetPosition.x
            ) +
            Mathf.Abs(
                attackerPosition.y - targetPosition.y
            );

        UnitBattleStats attackerStats =
            attacker.State.CombatStats;

        UnitBattleStats targetStats =
            target.State.CombatStats;

        string attackerName =
            attackerStats != null
                ? attackerStats.CharacterName
                : attacker.name;

        string targetName =
            targetStats != null
                ? targetStats.CharacterName
                : target.name;

        StringBuilder builder =
            new StringBuilder(300);

        builder.AppendLine("【攻撃対象の候補】");
        builder.AppendLine();

        builder.AppendLine(
            $"攻撃側：{attackerName}"
        );

        builder.AppendLine(
            $"対象：{targetName}"
        );

        // CSVの座標は1始まり
        builder.AppendLine(
            $"対象座標：({targetPosition.x + 1}, " +
            $"{targetPosition.y + 1})"
        );

        builder.AppendLine(
            $"縦横マス距離：{manhattanDistance}"
        );

        if (attackerStats != null)
        {
            builder.AppendLine();

            builder.AppendLine(
                $"クラス：{attackerStats.CurrentClassName}"
            );

            builder.AppendLine(
                $"CSV射程値：{attackerStats.MinAttackRange}" +
                $"～{attackerStats.MaxAttackRange}"
            );

            string rangeType =
                string.IsNullOrWhiteSpace(
                    attackerStats.RangeType)
                    ? "未設定"
                    : attackerStats.RangeType;

            builder.AppendLine(
                $"RangeType：{rangeType}"
            );
        }

        if (targetStats != null)
        {
            builder.AppendLine();

            builder.AppendLine(
                $"対象HP：{targetStats.CurrentHP}" +
                $"/{targetStats.MaxHP}"
            );
        }

        builder.AppendLine();
        builder.AppendLine(
            "※ 攻撃可能かどうかは未判定"
        );

        builder.AppendLine(
            "※ HPは減りません"
        );

        builder.AppendLine(
            "Esc / 右クリックで戻る"
        );

        informationText.text =
            builder.ToString();
    }

    /// <summary>
    /// 攻撃対象選択画面を閉じる。
    /// </summary>
    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
}
