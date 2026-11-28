using System.Collections.Generic;
using UnityEngine;

public static class AirSkill
{
    public const string Id = "air.carry";

    // 運搬後の移動力
    private const int MoveRange = 3;

    // 運搬中フラグ。SkillRuntimeState に残るため、運搬中はセーブ不可になる
    private const string CarryKey = "air.carrying";

    // 運搬者 → 運搬中の味方。
    // SkillRuntimeState は int しか持てないので、参照はここで保持する。
    private static readonly Dictionary<UnitView, UnitView> carried =
        new Dictionary<UnitView, UnitView>();

    public static void Register(SkillManager m)
    {
        m.Register(new SkillData(
            Id,
            "運搬",
            isAvailable: u => SkillIds.IsCharacter(u, SkillIds.Air),
            canUse: CanUse,
            execute: Execute));

        SkillTargetRules.Register(Id, new SkillTargetRule
        {
            // 味方がいないマスでは確定できない
            RequireTarget = true,

            // 確定後、移動3マスの選択へ進む
            FollowUpMoveRange = MoveRange,

            // 移動選択中(移動前)にキャンセルしたら運搬を取り消す
            OnFollowUpCancelled = (u, mgr) => CancelPickup(u),

            // 移動後、降ろす先を前後左右の空きマスから選ぶ
            GetPlaceCells = (u, center, mgr) => GetUnloadCells(u, center, mgr),

            // 選んだマスに味方を降ろす(隠し状態の解除)
            OnPlace = (u, cell, mgr) => TryUnload(u, mgr.Grid, mgr.AllUnits, cell),

            // 運ぶ味方を選ぶ: 前後左右の4マス(距離ちょうど1)
            GetValidCells = (u, mgr) =>
                SkillTargeting.CellsInRange(
                    mgr.Grid, u.State.Position, 1, 1),

            GetAffectedCells = (u, c) =>
                new HashSet<Vector2Int> { c },

            ResolveTargets = (u, c, mgr) =>
                SkillTargeting.InSquare(
                    mgr.AllUnits, c, 0, t => IsCarryable(u, t))
        });
    }

    // シーン再読込時の古い参照を消す(SkillBootstrapper.Start から呼ぶ)
    public static void Reset()
    {
        carried.Clear();
    }

    public static bool IsCarrying(UnitView carrier) =>
        carrier != null && carried.ContainsKey(carrier);

    private static bool IsCarryable(UnitView user, UnitView t) =>
        t != user &&
        t.State.Team == user.State.Team;

    // 運搬中でなく、隣に運べる味方がいること
    private static bool CanUse(SkillContext c)
    {
        if (IsCarrying(c.User)) return false;

        foreach (UnitView t in c.AllUnits)
        {
            if (SkillBattleAPI.IsAlive(t) &&
                IsCarryable(c.User, t) &&
                SkillTargeting.Distance(c.User, t) == 1)
            {
                return true;
            }
        }

        return false;
    }

    // 味方を運搬状態にして隠す
    private static bool Execute(SkillContext c)
    {
        if (c.Targets.Count != 1) return false;

        Debug.Log("SkillActivation...：エア");

        UnitView t = c.Targets[0];

        if (IsCarrying(c.User) ||
            !SkillBattleAPI.IsAlive(t) ||
            !IsCarryable(c.User, t) ||
            SkillTargeting.Distance(c.User, t) != 1)
        {
            return false;
        }

        carried[c.User] = t;

        // 状態が残っている間はセーブ拒否される(仕様)
        c.Runtime.SetValue(CarryKey, 1);

        // 隠す。非アクティブのユニットは占有・選択・IsAlive 判定から外れる
        t.gameObject.SetActive(false);

        return true;
    }

    // 降ろせるマス: center の前後左右で、通行可能かつ空いているマス。
    // center はエアの現在地、または移動先の候補(移動先の絞り込み用)。
    // 運搬中の味方は非アクティブなので占有扱いにならず、エア自身は無視する。
    public static HashSet<Vector2Int> GetUnloadCells(
        UnitView carrier, Vector2Int center, SkillManager mgr)
    {
        var result = new HashSet<Vector2Int>();

        if (!IsCarrying(carrier)) return result;

        foreach (Vector2Int c in SkillTargeting.CellsInRange(mgr.Grid, center, 1, 1))
        {
            if (!SkillBattleAPI.IsOccupied(c, mgr.AllUnits, carrier))
            {
                result.Add(c);
            }
        }

        return result;
    }

    // 運搬の取り消し(移動前のキャンセル用)。味方は元のマスに戻る。
    public static void CancelPickup(UnitView carrier)
    {
        if (carrier == null ||
            !carried.TryGetValue(carrier, out UnitView t))
        {
            return;
        }

        if (t != null)
        {
            t.gameObject.SetActive(true);   // 位置は変えていないので元のマスのまま
        }

        ClearCarry(carrier);
    }

    // 運搬中の味方を指定マスに降ろし、隠し状態を解除する
    public static bool TryUnload(
        UnitView carrier,
        BattleGrid grid,
        IReadOnlyList<UnitView> units,
        Vector2Int cell)
    {
        if (carrier == null ||
            !carried.TryGetValue(carrier, out UnitView t) ||
            t == null)
        {
            return false;
        }

        // 一度表示に戻してから共通APIで移動する(非アクティブだと IsAlive が false になるため)
        t.gameObject.SetActive(true);

        if (!SkillBattleAPI.TryMoveUnit(grid, units, t, cell))
        {
            t.gameObject.SetActive(false);
            return false;
        }

        ClearCarry(carrier);

        return true;
    }

    private static void ClearCarry(UnitView carrier)
    {
        carried.Remove(carrier);

        if (carrier.State != null && carrier.State.CombatStats != null)
        {
            carrier.State.CombatStats.Skills.RemoveValue(CarryKey);
        }
    }
}
