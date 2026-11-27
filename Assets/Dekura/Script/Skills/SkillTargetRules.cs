using System;
using System.Collections.Generic;
using UnityEngine;

// スキルごとの対象選択ルール。
// SkillData を変更せず、スキルIDをキーにして別管理する。
public sealed class SkillTargetRule
{
    // 選択できるマス
    public Func<UnitView, SkillManager, HashSet<Vector2Int>> GetValidCells;

    // 1回目クリックで表示する影響範囲(単体なら選択マスのみ)
    public Func<UnitView, Vector2Int, HashSet<Vector2Int>> GetAffectedCells;

    // 確定時に SkillContext.Targets へ入れるユニット
    public Func<UnitView, Vector2Int, SkillManager, List<UnitView>> ResolveTargets;
}

public static class SkillTargetRules
{
    private static readonly Dictionary<string, SkillTargetRule> rules =
        new Dictionary<string, SkillTargetRule>();

    public static void Register(string id, SkillTargetRule rule) =>
        rules[id] = rule;

    public static bool TryGet(string id, out SkillTargetRule rule) =>
        rules.TryGetValue(id, out rule);
}
