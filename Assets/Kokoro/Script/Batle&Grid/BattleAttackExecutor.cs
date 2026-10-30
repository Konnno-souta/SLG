
using System;
using UnityEngine;

/// <summary>
/// 攻撃結果の確認用データ。
/// </summary>
public struct BattleAttackPreview
{
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
        int remainingHP)
    {
        DamageType = damageType;
        CalculatedDamage = calculatedDamage;
        AppliedDamage = appliedDamage;
        RemainingHP = remainingHP;
    }
}

/// <summary>
/// 第20段階：HP0に対応した攻撃計算。
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
    // ========================================
    // 攻撃の予測
    // ========================================

    public static bool TryPreview(
        UnitView attacker,
        UnitView defender,
        out BattleAttackPreview preview)
    {
        preview = default(BattleAttackPreview);

        if (attacker == null ||
            defender == null ||
            attacker == defender ||
            attacker.State == null ||
            defender.State == null ||
            attacker.State.Team ==
                defender.State.Team ||
            attacker.State.CombatStats == null ||
            defender.State.CombatStats == null ||
            attacker.State.CombatStats.CurrentHP <= 0 ||
            defender.State.CombatStats.CurrentHP <= 0)
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
            );

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

        // 第20段階：
        // HP1制限を解除し、HP0を許可する。
        // オーバーキルしてもHPは0未満にならない。
        int appliedDamage = Mathf.Min(
            calculatedDamage,
            defenseStats.CurrentHP
        );

        preview = new BattleAttackPreview(
            damageType,
            calculatedDamage,
            appliedDamage,
            defenseStats.CurrentHP - appliedDamage
        );

        return true;
    }

    // ========================================
    // 攻撃実行
    // ========================================

    public static bool TryExecute(
        UnitView attacker,
        UnitView defender,
        out BattleAttackPreview preview)
    {
        if (!TryPreview(
            attacker,
            defender,
            out preview))
        {
            return false;
        }

        if (!defender.State.CombatStats.TryTakeDamage(
            preview.AppliedDamage,
            out int appliedDamage))
        {
            return false;
        }

        if (appliedDamage != preview.AppliedDamage)
        {
            Debug.LogError(
                "攻撃予測と実際のHP減少量が一致しません。",
                defender
            );

            return false;
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
