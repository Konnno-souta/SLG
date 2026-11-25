using System.Collections.Generic;
using UnityEngine;

public static class SkillBattleAPI
{
    // ========================================
    // 生存判定
    // ========================================

    public static bool IsAlive(UnitView unit)
    {
        return unit != null &&
               unit.gameObject.activeInHierarchy &&
               unit.State != null &&
               unit.State.CombatStats != null &&
               BattleUnitLife.IsAlive(unit);
    }

    // ========================================
    // ユニット登録確認
    // ========================================

    public static bool ContainsUnit(
        IReadOnlyList<UnitView> units,
        UnitView target)
    {
        if (units == null || target == null)
        {
            return false;
        }

        foreach (UnitView unit in units)
        {
            if (unit == target)
            {
                return true;
            }
        }

        return false;
    }

    // ========================================
    // HP回復
    // ========================================

    public static bool TryHeal(
        UnitView target,
        int amount,
        out int healedHP)
    {
        healedHP = 0;

        if (!IsAlive(target) || amount <= 0)
        {
            return false;
        }

        UnitBattleStats stats =
            target.State.CombatStats;

        int actual = Mathf.Min(
            amount,
            stats.MaxHP - stats.CurrentHP
        );

        if (actual <= 0 ||
            !stats.TryRestoreCurrentHP(
                stats.CurrentHP + actual))
        {
            return false;
        }

        healedHP = actual;

        return true;
    }

    // ========================================
    // 直接ダメージ
    // ========================================

    // 持続ダメージなどに使用する。
    // 通常攻撃のイベントは呼び出さない。
    public static bool TryDamage(
        UnitView target,
        int amount,
        out int appliedDamage)
    {
        appliedDamage = 0;

        if (!IsAlive(target) ||
            amount < 0 ||
            !target.State.CombatStats.TryTakeDamage(
                amount,
                out appliedDamage))
        {
            return false;
        }

        RefreshDefeatVisual(target);

        return true;
    }

    // ========================================
    // マスの占有判定
    // ========================================

    public static bool IsOccupied(
        Vector2Int position,
        IReadOnlyList<UnitView> units,
        UnitView ignoreA = null,
        UnitView ignoreB = null)
    {
        if (units == null)
        {
            return true;
        }

        foreach (UnitView unit in units)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy ||
                unit.State == null ||
                unit == ignoreA ||
                unit == ignoreB)
            {
                continue;
            }

            if (unit.State.CombatStats != null &&
                unit.State.CombatStats.IsDead)
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

    // ========================================
    // 移動先の有効判定
    // ========================================

    public static bool CanMoveTo(
        BattleGrid grid,
        IReadOnlyList<UnitView> units,
        UnitView target,
        Vector2Int destination)
    {
        return grid != null &&
               IsAlive(target) &&
               ContainsUnit(units, target) &&
               grid.IsWalkable(destination) &&
               !IsOccupied(
                   destination,
                   units,
                   target);
    }

    // ========================================
    // 強制移動
    // ========================================

    // 移動済み・行動済みフラグは変更しない。
    // 通常の移動アニメーション中には使用しない。
    public static bool TryMoveUnit(
        BattleGrid grid,
        IReadOnlyList<UnitView> units,
        UnitView target,
        Vector2Int destination)
    {
        if (!CanMoveTo(
            grid,
            units,
            target,
            destination))
        {
            return false;
        }

        target.State.SetForcedPosition(
            destination
        );

        target.SyncPosition(grid);

        return true;
    }

    // ========================================
    // 座標入れ替え
    // ========================================

    public static bool TrySwapUnits(
        BattleGrid grid,
        IReadOnlyList<UnitView> units,
        UnitView a,
        UnitView b)
    {
        if (grid == null ||
            !IsAlive(a) ||
            !IsAlive(b) ||
            a == b ||
            !ContainsUnit(units, a) ||
            !ContainsUnit(units, b))
        {
            return false;
        }

        Vector2Int pa = a.State.Position;

        Vector2Int pb = b.State.Position;

        if (pa == pb ||
            !grid.IsWalkable(pa) ||
            !grid.IsWalkable(pb) ||
            IsOccupied(pa, units, a, b) ||
            IsOccupied(pb, units, a, b))
        {
            return false;
        }

        a.State.SetForcedPosition(pb);

        b.State.SetForcedPosition(pa);

        a.SyncPosition(grid);

        b.SyncPosition(grid);

        return true;
    }

    // ========================================
    // 行動終了
    // ========================================

    public static bool TryCompleteAction(
        UnitView target)
    {
        if (target == null ||
            target.State == null ||
            target.State.HasActed ||
            (target.State.CombatStats != null &&
             target.State.CombatStats.IsDead))
        {
            return false;
        }

        target.State.CompleteAction();

        if (IsAlive(target))
        {
            SkillManager.Instance?
                .NotifyActionCompleted(target);
        }

        RefreshActionVisual(target);

        return true;
    }

    // ========================================
    // 行動状態リセット
    // ========================================

    public static bool TryResetAction(
        UnitView target)
    {
        if (!IsAlive(target))
        {
            return false;
        }

        target.State.ResetAction();

        RefreshActionVisual(target);

        return true;
    }

    // ========================================
    // ステータス補正
    // ========================================

    public static bool TrySetModifier(
        UnitView target,
        string key,
        SkillStat stat,
        float amount,
        int expiresAtTurn = int.MaxValue)
    {
        if (!IsAlive(target))
        {
            return false;
        }

        target.State.CombatStats.Skills.SetModifier(
            key,
            stat,
            amount,
            expiresAtTurn
        );

        return true;
    }

    // ========================================
    // 戦闘不能表示更新
    // ========================================

    public static void RefreshDefeatVisual(
        UnitView target)
    {
        if (target == null)
        {
            return;
        }

        BattleUnitDefeatVisual visual =
            target.GetComponent<BattleUnitDefeatVisual>();

        if (visual != null)
        {
            visual.Refresh();
        }
    }

    // ========================================
    // 行動済み表示更新
    // ========================================

    private static void RefreshActionVisual(
        UnitView target)
    {
        UnitActionVisual visual =
            target.GetComponent<UnitActionVisual>();

        if (visual != null)
        {
            visual.SetDimmed(
                target.State.HasActed
            );
        }
    }
}