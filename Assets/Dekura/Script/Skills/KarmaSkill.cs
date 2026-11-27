using System.Collections.Generic;
using UnityEngine;

// カルマ 1ターン敵が移動不可
public static class KarmaSkill
{
    public const string Id = "karma.block";

    // TODO: プランナー確認(仮)。対象が単体か範囲かも未確定
    private const int Range = 3;

    public static void Register(SkillManager m)
    {
        m.Register(new SkillData(
            Id,
            "移動封じ",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Karma),
            execute: Execute));

        SkillTargetRules.Register(Id, new SkillTargetRule
        {
            GetValidCells = (u, mgr) =>
            {
                var cells = new HashSet<Vector2Int>();

                foreach (var t in mgr.AllUnits)
                {
                    if (IsEnemy(u, t) && SkillTargeting.Distance(u, t) <= Range)
                    {
                        cells.Add(t.State.Position);
                    }
                }

                return cells;
            },

            GetAffectedCells = (u, c) => new HashSet<Vector2Int> { c },

            ResolveTargets = (u, c, mgr) =>
                SkillTargeting.InSquare(mgr.AllUnits, c, 0, t => IsEnemy(u, t))
        });
    }

    private static bool IsEnemy(UnitView user, UnitView t) =>
        SkillBattleAPI.IsAlive(t) && t.State.Team != user.State.Team;

    private static bool Execute(SkillContext c)
    {
        if (c.Targets.Count != 1 || !IsEnemy(c.User, c.Targets[0]))
        {
            return false;
        }

        // MoveBlocked > 0 で UnitState.MoveRange が 0 になる。
        // Turn+1 なら、使用ターンの敵フェーズだけ移動不可。
        return SkillBattleAPI.TrySetModifier(
            c.Targets[0],
            $"karma.block.{c.User.State.CombatStats.CharacterId}",
            SkillStat.MoveBlocked,
            1f,
            c.Turn + 1);
    }
}
