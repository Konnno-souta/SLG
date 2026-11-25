using System;
using UnityEngine;

/// <summary>
/// 攻撃結果の確認用データ。
/// </summary>
public struct BattleAttackPreview
{
    public UnitView ActualDefender
    {
        get;
        private set;
    }

    public BattleDamageType DamageType
    {
        get;
        private set;
    }

    public int CalculatedDamage
    {
        get;
        private set;
    }

    public int AppliedDamage
    {
        get;
        private set;
    }

    public int RemainingHP
    {
        get;
        private set;
    }

    public BattleAttackPreview(
        BattleDamageType damageType,
        int calculatedDamage,
        int appliedDamage,
        int remainingHP,
        UnitView actualDefender = null)
    {
        ActualDefender = actualDefender;

        DamageType = damageType;

        CalculatedDamage = calculatedDamage;

        AppliedDamage = appliedDamage;

        RemainingHP = remainingHP;
    }
}

/// <summary>
/// 攻撃計算・攻撃実行。
///
/// 物理：Attack - AttackDefense
/// 魔法：Magic - MagicDefense
///
/// ・小数点以下切り捨て
/// ・負のダメージは暫定的に0
/// ・HP0までダメージを適用可能
/// ・命中、クリティカルは今後実装
/// </summary>
public static class BattleAttackExecutor
{
    private static int resolvingDepth;

    // ========================================
    // 通常攻撃の予測
    // ========================================

    public static bool TryPreview(
        UnitView attacker,
        UnitView defender,
        out BattleAttackPreview preview)
    {
        return TryPreview(
            attacker,
            defender,
            out preview,
            1f,
            true
        );
    }

    // ========================================
    // 倍率指定攻撃の予測
    // ========================================

    public static bool TryPreview(
        UnitView attacker,
        UnitView defender,
        out BattleAttackPreview preview,
        float damageMultiplier,
        bool useSkillHooks = true)
    {
        return TryBuildAttack(
            attacker,
            defender,
            damageMultiplier,
            useSkillHooks,
            true,
            false,
            out _,
            out preview
        );
    }

    // ========================================
    // 攻撃対象の有効判定
    // ========================================

    private static bool IsValidPair(
        UnitView attacker,
        UnitView defender)
    {
        return attacker != null &&
               defender != null &&
               attacker != defender &&
               attacker.State != null &&
               defender.State != null &&
               attacker.State.Team !=
                   defender.State.Team &&
               attacker.State.CombatStats != null &&
               defender.State.CombatStats != null &&
               attacker.State.CombatStats.CurrentHP > 0 &&
               defender.State.CombatStats.CurrentHP > 0;
    }

    // ========================================
    // 共通攻撃計算
    // ========================================

    private static bool TryBuildAttack(
        UnitView attacker,
        UnitView defender,
        float multiplier,
        bool useSkillHooks,
        bool isPreview,
        bool isFollowUp,
        out SkillAttackContext context,
        out BattleAttackPreview preview)
    {
        context = null;

        preview = default(BattleAttackPreview);

        if (!IsValidPair(attacker, defender))
        {
            return false;
        }

        context = new SkillAttackContext(
            attacker,
            defender,
            multiplier,
            isPreview,
            isFollowUp
        );

        SkillManager manager =
            useSkillHooks
                ? SkillManager.Instance
                : null;

        // 攻撃前のスキル通知
        manager?.NotifyAttackPreparing(context);

        // 庇う等による対象変更を反映
        defender = context.Defender;

        if (!IsValidPair(attacker, defender) ||
            float.IsNaN(context.DamageMultiplier) ||
            float.IsInfinity(context.DamageMultiplier) ||
            context.DamageMultiplier < 0f)
        {
            return false;
        }

        if (defender != context.OriginalDefender &&
            (manager == null ||
             !SkillBattleAPI.ContainsUnit(
                 manager.AllUnits,
                 defender)))
        {
            return false;
        }

        UnitBattleStats attackStats =
            attacker.State.CombatStats;

        UnitBattleStats defenseStats =
            defender.State.CombatStats;

        // 物理・魔法の暫定判定
        if (!TryGetDamageType(
            attackStats.RangeType,
            out BattleDamageType damageType))
        {
            Debug.LogWarning(
                $"{attacker.name}: " +
                $"未対応のRangeType={attackStats.RangeType}",
                attacker
            );

            return false;
        }

        // 正式計算式
        float rawDamage =
            BattleDamageCalculator.CalculateDamage(
                attackStats,
                defenseStats,
                damageType
            ) * context.DamageMultiplier;

        if (float.IsNaN(rawDamage) ||
            float.IsInfinity(rawDamage))
        {
            Debug.LogError(
                "ダメージ計算が不正な値になりました。",
                attacker
            );

            return false;
        }

        // 小数点以下切り捨て。
        // 負のダメージは暫定的に0。
        int calculatedDamage = Mathf.Max(
            0,
            Mathf.FloorToInt(rawDamage)
        );

        context.DamageType = damageType;

        context.RequestedDamage =
            calculatedDamage;

        // ダメージ適用前の通知
        manager?.NotifyDamagePreparing(context);

        // この段階ではダメージ量のみ変更可能
        if (context.Defender != defender ||
            context.RequestedDamage < 0)
        {
            return false;
        }

        // HP0未満にならないようにする。
        int appliedDamage = Mathf.Min(
            context.RequestedDamage,
            defenseStats.CurrentHP
        );

        preview = new BattleAttackPreview(
            damageType,
            calculatedDamage,
            appliedDamage,
            defenseStats.CurrentHP - appliedDamage,
            defender
        );

        return true;
    }

    // ========================================
    // 通常攻撃実行
    // ========================================

    public static bool TryExecute(
        UnitView attacker,
        UnitView defender,
        out BattleAttackPreview preview)
    {
        return TryExecute(
            attacker,
            defender,
            out preview,
            1f,
            true
        );
    }

    // ========================================
    // 倍率指定攻撃・追加攻撃
    // ========================================

    public static bool TryExecute(
        UnitView attacker,
        UnitView defender,
        out BattleAttackPreview preview,
        float damageMultiplier,
        bool useSkillHooks = true,
        bool isFollowUp = false)
    {
        preview = default(BattleAttackPreview);

        // 追加攻撃から更に追加攻撃することを禁止
        if (resolvingDepth >= 2 ||
            (resolvingDepth > 0 && !isFollowUp))
        {
            return false;
        }

        if (!TryBuildAttack(
            attacker,
            defender,
            damageMultiplier,
            useSkillHooks,
            false,
            isFollowUp,
            out SkillAttackContext context,
            out preview))
        {
            return false;
        }

        UnitView actual =
            context.Defender;

        UnitBattleStats stats =
            actual.State.CombatStats;

        // 実際のダメージ適用
        if (!stats.TryTakeDamage(
            preview.AppliedDamage,
            out int applied))
        {
            return false;
        }

        // 既存の予測結果との一致確認
        if (applied != preview.AppliedDamage)
        {
            Debug.LogError(
                "攻撃予測と実際のHP減少量が一致しません。",
                actual
            );

            return false;
        }

        preview = new BattleAttackPreview(
            preview.DamageType,
            preview.CalculatedDamage,
            applied,
            stats.CurrentHP,
            actual
        );

        // 攻撃終了後のスキル通知
        if (useSkillHooks)
        {
            resolvingDepth++;

            try
            {
                SkillManager.Instance?.NotifyAttackResolved(
                    context,
                    preview
                );
            }
            finally
            {
                resolvingDepth--;
            }
        }

        return true;
    }

    // ========================================
    // 物理・魔法の暫定分類
    // ========================================

    private static bool TryGetDamageType(
        string rangeType,
        out BattleDamageType damageType)
    {
        damageType = BattleDamageType.Physical;

        switch ((rangeType ?? "").Trim())
        {
            case "Braver":
            case "Trident":
            case "Spartan":
            case "Phalanx":
            case "Cavalier":
            case "Artemis":

                damageType =
                    BattleDamageType.Physical;

                return true;

            case "Light Sorcerer":
            case "Dark Wizard":

                damageType =
                    BattleDamageType.Magic;

                return true;

            default:

                return false;
        }
    }
}