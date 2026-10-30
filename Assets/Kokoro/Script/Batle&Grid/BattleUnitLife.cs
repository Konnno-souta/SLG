
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第20段階・後半：
/// ユニットの生存・戦闘不能を共通判定する。
///
/// HP > 0：戦闘可能
/// HP = 0：戦闘不能
///
/// HP0でもUnitViewとGameObjectは保持する。
/// </summary>
public static class BattleUnitLife
{
    // ========================================
    // 生存判定
    // ========================================

    /// <summary>
    /// 現在戦闘に参加できるユニットか。
    /// </summary>
    public static bool IsAlive(UnitView unit)
    {
        if (unit == null ||
            !unit.gameObject.activeInHierarchy ||
            unit.State == null ||
            unit.State.CombatStats == null)
        {
            return false;
        }

        return unit.State.CombatStats.CurrentHP > 0;
    }

    // ========================================
    // 戦闘不能判定
    // ========================================

    /// <summary>
    /// HP0の戦闘不能ユニットか。
    /// </summary>
    public static bool IsDefeated(UnitView unit)
    {
        if (unit == null ||
            unit.State == null ||
            unit.State.CombatStats == null)
        {
            return false;
        }

        return unit.State.CombatStats.CurrentHP <= 0;
    }

    // ========================================
    // チーム別生存判定
    // ========================================

    /// <summary>
    /// 指定チームに生存ユニットがいるか。
    /// </summary>
    public static bool HasLivingUnits(
        IEnumerable<UnitView> units,
        UnitTeam team)
    {
        if (units == null)
        {
            return false;
        }

        foreach (UnitView unit in units)
        {
            if (!IsAlive(unit))
            {
                continue;
            }

            if (unit.State.Team == team)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 指定チームの生存数。
    /// </summary>
    public static int CountLivingUnits(
        IEnumerable<UnitView> units,
        UnitTeam team)
    {
        if (units == null)
        {
            return 0;
        }

        int count = 0;

        foreach (UnitView unit in units)
        {
            if (!IsAlive(unit))
            {
                continue;
            }

            if (unit.State.Team == team)
            {
                count++;
            }
        }

        return count;
    }

    // ========================================
    // マスの占有判定
    // ========================================

    /// <summary>
    /// 生存ユニットが指定マスを占有しているか。
    ///
    /// HP0のユニットはマスを占有しない。
    /// </summary>
    public static bool IsOccupiedByLivingUnit(
        IEnumerable<UnitView> units,
        Vector2Int position)
    {
        if (units == null)
        {
            return false;
        }

        foreach (UnitView unit in units)
        {
            if (!IsAlive(unit))
            {
                continue;
            }

            if (unit.State.Position == position)
            {
                return true;
            }
        }

        return false;
    }
}
