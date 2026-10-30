
using System;

public enum BattleDamageType
{
    Physical,
    Magic
}

/// <summary>
/// 物理・魔法のダメージ計算を担当。
/// HPの減少処理は行わない。
/// </summary>
public static class BattleDamageCalculator
{
    public static float CalculateDamage(
        UnitBattleStats attacker,
        UnitBattleStats defender,
        BattleDamageType damageType)
    {
        if (attacker == null)
            throw new ArgumentNullException(nameof(attacker));

        if (defender == null)
            throw new ArgumentNullException(nameof(defender));

        switch (damageType)
        {
            case BattleDamageType.Physical:
                return attacker.Attack
                    - defender.AttackDefense;

            case BattleDamageType.Magic:
                return attacker.Magic
                    - defender.MagicDefense;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(damageType)
                );
        }
    }
}
