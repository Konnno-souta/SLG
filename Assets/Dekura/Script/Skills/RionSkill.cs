using System.Collections.Generic;
using UnityEngine;

// リオン 連撃(戦技): 通常攻撃と同じ操作で、攻撃を2回行う
public static class RionSkill
{
    public const string Id = "rion.rengeki";

    // 攻撃回数
    private const int AttackCount = 2;

    // TODO: 2回目以降の倍率が通常と違う場合はプランナー確認(現状はどちらも等倍)

    public static void Register(SkillManager m)
    {
        m.Register(new SkillData(
            Id,
            "連撃",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Rion),
            canUse: CanUse,
            execute: Execute));

        SkillTargetRules.Register(Id, new SkillTargetRule
        {
            // 通常攻撃型: 対象選択・プレビュー・演出は SkillSelectionController が
            // 通常攻撃と同じ流れで行い、この回数だけ攻撃する
            AttackCount = AttackCount,

            // 通常攻撃と同じ攻撃可能範囲
            GetValidCells = (u, mgr) =>
                AttackRangeCalculator.Calculate(
                    mgr.Grid, u.State.Position, u.State.CombatStats),

            GetAffectedCells = (u, c) =>
                new HashSet<Vector2Int> { c },

            ResolveTargets = (u, c, mgr) =>
                SkillTargeting.InSquare(
                    mgr.AllUnits, c, 0, t => IsEnemy(u, t))
        });
    }

    private static bool IsEnemy(UnitView user, UnitView t) =>
        t.State.Team != user.State.Team;

    // 攻撃範囲内に敵が1体もいなければ使えない
    private static bool CanUse(SkillContext c)
    {
        HashSet<Vector2Int> cells = AttackRangeCalculator.Calculate(
            c.Grid, c.User.State.Position, c.User.State.CombatStats);

        foreach (UnitView t in c.AllUnits)
        {
            if (SkillBattleAPI.IsAlive(t) &&
                IsEnemy(c.User, t) &&
                AttackRangeCalculator.Contains(cells, t.State.Position))
            {
                return true;
            }
        }

        return false;
    }

    // TryUse 経由で呼ばれた場合の同期実行(演出なし)。
    // SkillSelectionController の通常操作では使われず、演出付きで別に実行される。
    private static bool Execute(SkillContext c)
    {
        if (c.Targets.Count != 1 || !IsEnemy(c.User, c.Targets[0]))
        {
            return false;
        }

        UnitView target = c.Targets[0];

        HashSet<Vector2Int> cells = AttackRangeCalculator.Calculate(
            c.Grid, c.User.State.Position, c.User.State.CombatStats);

        if (!AttackRangeCalculator.Contains(cells, target.State.Position))
        {
            return false;
        }

        int hits = 0;

        for (int i = 0; i < AttackCount; i++)
        {
            if (!SkillBattleAPI.IsAlive(c.User) ||
                !SkillBattleAPI.IsAlive(target))
            {
                break;
            }

            if (!BattleAttackExecutor.TryExecute(
                    c.User, target, out BattleAttackPreview result))
            {
                break;
            }

            hits++;

            target = result.ActualDefender ?? target;

            SkillBattleAPI.RefreshDefeatVisual(target);
        }

        return hits > 0;
    }
}