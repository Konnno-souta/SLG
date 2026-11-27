using System.Collections.Generic;
using UnityEngine;

// レイ 2ターン範囲デバフ
public static class RaySkill
{
    public const string Id = "ray.debuff";

    // TODO: すべてプランナー確認(仮)
    private const int Range = 3;                  // 使用者からの射程
    private const int Radius = 1;                 // 影響範囲(1 = 3x3)
    private const float Amount = 3f;              // デバフ量
    private const SkillStat Stat = SkillStat.Attack;
    private const int Duration = 2;               // 使用ターンを含めるかも要確認

    public static void Register(SkillManager m)
    {
        m.Register(new SkillData(
            Id,
            "デバフ",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Ray),
            execute: Execute));

        SkillTargetRules.Register(Id, new SkillTargetRule
        {
            GetValidCells = (u, mgr) =>
                SkillTargeting.CellsInRange(
                    mgr.Grid, u.State.Position, 0, Range),

            GetAffectedCells = (u, c) =>
                SkillTargeting.SquareCells(
                    SkillManager.Instance.Grid, c, Radius),

            ResolveTargets = (u, c, mgr) =>
                SkillTargeting.InSquare(
                    mgr.AllUnits, c, Radius, t => IsEnemy(u, t))
        });
    }

    private static bool IsEnemy(UnitView user, UnitView t) =>
        t.State.Team != user.State.Team;

    private static bool Execute(SkillContext c)
    {
        int applied = 0;

        // 同じ使用者の再使用は上書き、別の使用者とは重なる
        string key =
            $"ray.debuff.{c.User.State.CombatStats.CharacterId}";

        foreach (var t in c.Targets)
        {
            if (!IsEnemy(c.User, t)) continue;

            if (SkillBattleAPI.TrySetModifier(
                    t, key, Stat, -Amount, c.Turn + Duration))
            {
                applied++;
            }
        }

        return applied > 0;
    }
}
