using System.Collections.Generic;
using UnityEngine;

// クラン 采配: 仲間と位置を入れ替える
public static class ClanSkill
{
    public const string Id = "clan.saihai";

    private const int Range = 3;   // TODO: プランナー確認(仮)

    public static void Register(SkillManager m)
    {
        m.Register(new SkillData(
            Id,
            "采配",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Clan),
            execute: Execute));

        SkillTargetRules.Register(Id, new SkillTargetRule
        {
            GetValidCells = (u, mgr) =>
            {
                var cells = new HashSet<Vector2Int>();

                foreach (var t in mgr.AllUnits)
                {
                    if (IsValid(u, t) && SkillTargeting.Distance(u, t) <= Range)
                    {
                        cells.Add(t.State.Position);
                    }
                }

                return cells;
            },

            GetAffectedCells = (u, c) => new HashSet<Vector2Int> { c },

            ResolveTargets = (u, c, mgr) =>
                SkillTargeting.InSquare(mgr.AllUnits, c, 0, t => IsValid(u, t))
        });
    }

    private static bool IsValid(UnitView user, UnitView t) =>
        t != user &&
        SkillBattleAPI.IsAlive(t) &&
        t.State.Team == user.State.Team;

    private static bool Execute(SkillContext c)
    {
        if (c.Targets.Count != 1 || !IsValid(c.User, c.Targets[0]))
        {
            return false;
        }

        Debug.Log($"SkillPlay_Clan : Swap (to {c.Targets[0].name})");
        return SkillBattleAPI.TrySwapUnits(c.Grid, c.AllUnits, c.User, c.Targets[0]);
    }
}
