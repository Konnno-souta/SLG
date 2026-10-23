
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 攻撃対象選択時の案内UI。
///
/// ・攻撃可能範囲の説明
/// ・射程内の敵の情報表示
/// ・射程外の敵への警告表示
///
/// 実際の攻撃処理は行わない。
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
                "AttackTargetPreviewPanelの参照が" +
                "不足しています。",
                this
            );

            enabled = false;
            return;
        }

        panelRoot.SetActive(false);
    }

    // =====================================
    // 表示開始
    // =====================================

    public void Show()
    {
        if (!enabled || panelRoot == null)
        {
            return;
        }

        panelRoot.SetActive(true);

        ShowSearching();
    }

    // =====================================
    // 対象を探している状態
    // =====================================

    public void ShowSearching()
    {
        if (informationText == null)
        {
            return;
        }

        informationText.text =
            "【攻撃対象の選択】\n\n" +
            "赤い枠が攻撃可能なマスです。\n\n" +
            "範囲内にいる敵を選んでください。\n\n" +
            "左クリック / Enter：対象選択\n" +
            "Esc / 右クリック：戻る\n\n" +
            "※ ダメージ処理は未実装です。";
    }

    /// <summary>
    /// RangeTypeが不明などで
    /// 攻撃可能なマスを計算できない場合。
    /// </summary>
    public void ShowNoRange(
        string className,
        string rangeType)
    {
        if (informationText == null)
        {
            return;
        }

        informationText.text =
            "【攻撃対象の選択】\n\n" +
            "攻撃範囲を取得できません。\n\n" +
            $"クラス：{className}\n" +
            $"RangeType：{rangeType}\n\n" +
            "CSVと攻撃範囲ルールを" +
            "確認してください。\n\n" +
            "Esc / 右クリック：戻る";
    }

    // =====================================
    // 範囲内の敵を選択
    // =====================================

    /// <summary>
    /// 攻撃可能範囲内にいる敵の情報を表示。
    ///
    /// ※ この段階ではHPを減らさない。
    /// </summary>
    public void ShowCandidate(
        UnitView attacker,
        UnitView target)
    {
        if (informationText == null ||
            attacker == null ||
            target == null ||
            attacker.State == null ||
            target.State == null)
        {
            return;
        }

        UnitBattleStats attackerStats =
            attacker.State.CombatStats;

        UnitBattleStats targetStats =
            target.State.CombatStats;

        Vector2Int attackerPosition =
            attacker.State.Position;

        Vector2Int targetPosition =
            target.State.Position;

        int distance =
            Mathf.Abs(
                attackerPosition.x - targetPosition.x
            ) +
            Mathf.Abs(
                attackerPosition.y - targetPosition.y
            );

        string attackerName =
            attackerStats != null
                ? attackerStats.CharacterName
                : attacker.name;

        string targetName =
            targetStats != null
                ? targetStats.CharacterName
                : target.name;

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "【攻撃対象：範囲内】"
        );

        builder.AppendLine();

        builder.AppendLine(
            $"攻撃側：{attackerName}"
        );

        builder.AppendLine(
            $"対象：{targetName}"
        );

        builder.AppendLine(
            $"対象座標：({targetPosition.x + 1}, " +
            $"{targetPosition.y + 1})"
        );

        builder.AppendLine(
            $"縦横距離：{distance}"
        );

        if (attackerStats != null)
        {
            builder.AppendLine();

            builder.AppendLine(
                $"クラス：{attackerStats.CurrentClassName}"
            );

            builder.AppendLine(
                $"攻撃タイプ：{attackerStats.RangeType}"
            );

            builder.AppendLine(
                $"CSV射程：{attackerStats.MinAttackRange}" +
                $"～{attackerStats.MaxAttackRange}"
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
            "攻撃可能範囲内の敵です。"
        );

        builder.AppendLine(
            "※ 攻撃実行・ダメージは未実装"
        );

        builder.AppendLine(
            "Esc / 右クリックで戻る"
        );

        informationText.text =
            builder.ToString();
    }

    // =====================================
    // 範囲外の敵を選択
    // =====================================

    /// <summary>
    /// 敵ではあるが、攻撃範囲外だった場合。
    /// </summary>
    public void ShowOutOfRange(UnitView target)
    {
        if (informationText == null ||
            target == null)
        {
            return;
        }

        string targetName = target.name;

        if (target.State != null &&
            target.State.CombatStats != null)
        {
            targetName =
                target.State.CombatStats.CharacterName;
        }

        informationText.text =
            "【攻撃対象：範囲外】\n\n" +
            $"対象：{targetName}\n\n" +
            "この敵は攻撃可能範囲の外にいます。\n\n" +
            "赤い枠の中にいる敵を" +
            "選択してください。\n\n" +
            "Esc / 右クリック：戻る";
    }

    // =====================================
    // 非表示
    // =====================================

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
}
