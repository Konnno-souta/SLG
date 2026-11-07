using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 攻撃対象選択時の案内UI。
/// 第19段階：選択した敵の予測ダメージを表示。
/// </summary>
public sealed class AttackTargetPreviewPanel : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private TMP_Text informationText;

    public bool IsOpen =>
        panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        if (panelRoot == null || informationText == null)
        {
            Debug.LogError(
                "AttackTargetPreviewPanelの参照が不足しています。",
                this);
            enabled = false;
            return;
        }

        panelRoot.SetActive(false);
    }

    public void Show()
    {
        if (!enabled || panelRoot == null)
        {
            return;
        }

        panelRoot.SetActive(true);
        ShowSearching();
    }

    public void ShowSearching()
    {
        if (informationText == null)
        {
            return;
        }

        informationText.text =
            "【攻撃対象の選択】\n\n" +
            "赤い枠が攻撃可能なマスです。\n\n" +
            "射程内の敵を1回選択：情報表示\n" +
            "同じ敵をもう1回選択：攻撃実行\n\n" +
            "左クリック / Enter：対象選択・決定\n" +
            "Esc / 右クリック：戻る\n\n" +
            "※HP0で戦闘不能になります。";
    }

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
            "CSVと攻撃範囲ルールを確認してください。\n\n" +
            "Esc / 右クリック：戻る";
    }

    public void ShowCandidate(UnitView attacker, UnitView target)
    {
        if (informationText == null ||
            attacker == null || target == null ||
            attacker.State == null || target.State == null)
        {
            return;
        }

        UnitBattleStats attackerStats = attacker.State.CombatStats;
        UnitBattleStats targetStats = target.State.CombatStats;

        Vector2Int attackerPosition = attacker.State.Position;
        Vector2Int targetPosition = target.State.Position;

        int distance =
            Mathf.Abs(attackerPosition.x - targetPosition.x) +
            Mathf.Abs(attackerPosition.y - targetPosition.y);

        string attackerName = attackerStats != null
            ? attackerStats.CharacterName : attacker.name;
        string targetName = targetStats != null
            ? targetStats.CharacterName : target.name;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("【攻撃対象：範囲内】");
        builder.AppendLine();
        builder.AppendLine($"攻撃側：{attackerName}");
        builder.AppendLine($"対象：{targetName}");
        builder.AppendLine($"対象座標：({targetPosition.x + 1}, {targetPosition.y + 1})");
        builder.AppendLine($"縦横距離：{distance}");

        if (attackerStats != null)
        {
            builder.AppendLine();
            builder.AppendLine($"クラス：{attackerStats.CurrentClassName}");
            builder.AppendLine($"攻撃タイプ：{attackerStats.RangeType}");
            builder.AppendLine($"CSV射程：{attackerStats.MinAttackRange}～{attackerStats.MaxAttackRange}");
        }

        if (targetStats != null)
        {
            builder.AppendLine();
            builder.AppendLine($"対象HP：{targetStats.CurrentHP}/{targetStats.MaxHP}");
        }

        if (BattleAttackExecutor.TryPreview(attacker, target,
            out BattleAttackPreview preview))
        {
            builder.AppendLine();
            builder.AppendLine($"攻撃種別：{preview.DamageType}");
            builder.AppendLine($"計算ダメージ（切り捨て）：{preview.CalculatedDamage}");
            builder.AppendLine($"今回減らすHP：{preview.AppliedDamage}");
            builder.AppendLine($"攻撃後の残HP：{preview.RemainingHP}");
            builder.AppendLine(
    preview.RemainingHP <= 0 ? "この攻撃で対象は戦闘不能になります。"
        :   "対象は攻撃後も戦闘可能です。");
            builder.AppendLine();
            builder.AppendLine("同じ敵をもう一度決定：攻撃実行");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("攻撃タイプを判定できません。");
            builder.AppendLine("CSVのRangeTypeを確認してください。");
        }

        builder.AppendLine("Esc / 右クリックで戻る");
        informationText.text = builder.ToString();
    }

    public void ShowOutOfRange(UnitView target)
    {
        if (informationText == null || target == null)
        {
            return;
        }

        string targetName = target.name;
        if (target.State != null && target.State.CombatStats != null)
        {
            targetName = target.State.CombatStats.CharacterName;
        }

        informationText.text =
            "【攻撃対象：範囲外】\n\n" +
            $"対象：{targetName}\n\n" +
            "この敵は攻撃可能範囲の外にいます。\n\n" +
            "赤い枠の中にいる敵を選択してください。\n\n" +
            "Esc / 右クリック：戻る";
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }
}
