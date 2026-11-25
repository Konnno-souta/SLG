using System.Collections.Generic;
using UnityEngine;

public sealed class SkillContext
{
    public SkillManager Manager { get; }

    public UnitView User { get; }

    public IReadOnlyList<UnitView> Targets { get; }

    public Vector2Int? TargetCell { get; }

    public BattleGrid Grid => Manager.Grid;

    public IReadOnlyList<UnitView> AllUnits =>
        Manager.AllUnits;

    public int Turn => Manager.Turn;

    public BattlePhase Phase => Manager.Phase;

    public SkillRuntimeState Runtime =>
        User.State.CombatStats.Skills;

    public SkillContext(
        SkillManager manager,
        UnitView user,
        IReadOnlyList<UnitView> targets = null,
        Vector2Int? targetCell = null)
    {
        Manager = manager ??
            throw new System.ArgumentNullException(
                nameof(manager));

        User = user;

        Targets = targets == null
            ? new List<UnitView>()
            : new List<UnitView>(targets);

        TargetCell = targetCell;
    }
}

// ========================================
// UŒ‚‚Ég—p‚·‚é‹¤’Êî•ñ
// ========================================

// UŒ‚—\‘ª‚ÉHP‚âg—p‰ñ”‚ğ•ÏX‚µ‚È‚¢‚±‚ÆB
public sealed class SkillAttackContext
{
    public UnitView Attacker { get; }

    public UnitView OriginalDefender { get; }

    public UnitView Defender { get; set; }

    public float DamageMultiplier { get; set; }

    public int RequestedDamage { get; set; }

    public BattleDamageType DamageType
    {
        get;
        internal set;
    }

    public bool IsPreview { get; }

    public bool IsFollowUp { get; }

    // UŒ‚1‰ñ‚¾‚¯—LŒø‚Èî•ñ
    private readonly HashSet<string> flags =
        new HashSet<string>();

    public void Mark(string key)
    {
        flags.Add(key);
    }

    public bool IsMarked(string key)
    {
        return flags.Contains(key);
    }

    public SkillAttackContext(
        UnitView attacker,
        UnitView defender,
        float damageMultiplier,
        bool isPreview,
        bool isFollowUp = false)
    {
        Attacker = attacker;

        OriginalDefender = defender;

        Defender = defender;

        DamageMultiplier = damageMultiplier;

        IsPreview = isPreview;

        IsFollowUp = isFollowUp;
    }
}