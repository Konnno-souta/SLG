using System.Collections.Generic;
using UnityEngine;

// ビット 3x3範囲回復(中心は使用者の位置に固定)
public static class BitSkill
{
    public const string Id = "bit.heal";

    // TODO: プランナー確認(仮)
    private const int Radius = 1;         // 回復範囲(1 = 3x3)
    private const int HealAmount = 10;    // 回復量(魔力参照にするかも要確認)

    public static void Register(SkillManager m)
    {
        m.Register(new SkillData(
            Id,
            "範囲回復",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Bit),
            execute: Execute));

        SkillTargetRules.Register(Id, new SkillTargetRule
        {
            // 選べるのは自分のマスだけ(中心固定)
            GetValidCells = (u, mgr) =>
                new HashSet<Vector2Int> { u.State.Position },

            // 1回目クリックで自分を中心にした3x3をプレビュー
            GetAffectedCells = (u, c) =>
                SkillTargeting.SquareCells(
                    SkillManager.Instance.Grid, u.State.Position, Radius),

            ResolveTargets = (u, c, mgr) =>
                SkillTargeting.InSquare(
                    mgr.AllUnits, u.State.Position, Radius, t => IsAlly(u, t))
        });
    }

    private static bool IsAlly(UnitView user, UnitView t) =>
        t.State.Team == user.State.Team;   // 自分自身を含む

    private static bool Execute(SkillContext c)
    {
        // 中心は使用者の位置に固定。Targets は信用せず再計算する
        List<UnitView> targets = SkillTargeting.InSquare(
            c.AllUnits, c.User.State.Position, Radius, t => IsAlly(c.User, t));

        int healed = 0;

        foreach (UnitView t in targets)
        {
            // HP満タン・戦闘不能は TryHeal が false を返すので無視
            if (SkillBattleAPI.TryHeal(t, HealAmount, out _))
            {
                healed++;
            }
        }

        // 誰も回復できなければ失敗(行動を消費しない)
        return healed > 0;
    }
}
