using System.Collections.Generic;
using UnityEngine;

// ミリアルド パワーチャージ: 次の攻撃が強化される(2ターンに1回)
public static class MillardSkill
{
    public const string Id = "millard.charge";

    private const string CdKey = "millard.charge.cd";        // 次に使えるターン
    private const string ChargedKey = "millard.charged";     // 溜めの有効期限ターン

    private const float Rate = 2f;   // TODO: プランナー確認(仮)

    public static void Register(SkillManager m)
    {
        m.Register(new SkillData(
            Id,
            "パワーチャージ",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Millard),
            canUse: CanUse,
            execute: Execute));

        // 自分自身が対象。自分のマスを2回クリックで確定
        SkillTargetRules.Register(Id, new SkillTargetRule
        {
            GetValidCells = (u, mgr) =>
                new HashSet<Vector2Int> { u.State.Position },

            GetAffectedCells = (u, c) =>
                new HashSet<Vector2Int> { c },

            ResolveTargets = (u, c, mgr) =>
                new List<UnitView> { u }
        });

    }

    private static bool CanUse(SkillContext c) => c.Runtime.GetValue(CdKey, 0) <= c.Turn;

    private static bool Execute(SkillContext c)
    {
        c.Runtime.SetValue(CdKey, c.Turn + 2);        // 2ターンに1回
        c.Runtime.SetValue(ChargedKey, c.Turn + 2);   // 使用翌ターンまで有効

        Debug.Log($"SkillPlay_Millard : Charge (2t)");

        return true;
    }

    // 攻撃計算前: 倍率のみ掛ける(予測にも反映して問題なし)
    public static void OnAttackPreparing(SkillAttackContext a)
    {
        if (!SkillIds.IsCharacter(a.Attacker, SkillIds.Millard)) return;

        if (a.Attacker.State.CombatStats.Skills.GetValue(ChargedKey, 0) > 0)
        {
            a.DamageMultiplier *= Rate;
        }
    }

    // 攻撃完了後: 溜めを消費(予測では消費しない)
    public static void OnAttackResolved(SkillAttackContext a, BattleAttackPreview r)
    {
        if (a.IsPreview) return;
        if (!SkillIds.IsCharacter(a.Attacker, SkillIds.Millard)) return;

        Debug.Log($"Millard : ChargeTerminate");
        a.Attacker.State.CombatStats.Skills.RemoveValue(ChargedKey);
    }

    // ターン開始時: 溜めを1減らし、期限切れの値を消す
    public static void OnTurnStarted(int turn)
    {
        foreach (var u in SkillManager.Instance.AllUnits)
        {
            if (!SkillIds.IsCharacter(u, SkillIds.Millard)) continue;

            var rt = u.State.CombatStats.Skills;
            int left = rt.GetValue(ChargedKey, 0);

            if (left > 0)
            {
                left--;

                if (left > 0) rt.SetValue(ChargedKey, left);
                else rt.RemoveValue(ChargedKey);   // 0で消す(残るとセーブ不可になる)
            }

            int cd = rt.GetValue(CdKey, 0);
            if (cd > 0 && cd <= turn)
            {
                rt.RemoveValue(CdKey);
            }
        }
    }
}
