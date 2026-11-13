
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// F7で攻撃エフェクトをテストする。
/// 味方と敵を自動検索。
/// ダメージ適用はInspectorで切り替え可能。
/// </summary>
public sealed class BattleEffectDebugTester
    : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private BattleCombatEffectPlayer effectPlayer;

    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private BattleGrid grid;

    [Header("攻撃するクラス")]
    [SerializeField]
    private string rangeTypeFilter = "Braver";

    [Header("手動指定（空欄なら自動検索）")]
    [SerializeField]
    private UnitView attackerOverride;

    [SerializeField]
    private UnitView targetOverride;

    [Header("テスト設定")]
    [SerializeField]
    private bool applyDamage = false;

    [SerializeField]
    private bool ignoreAttackRange = true;

    [SerializeField]
    private float defaultHitDelay = 0.18f;

    private bool isTesting;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.f7Key.wasPressedThisFrame)
        {
            StartDebugAttack();
        }
    }
#endif

    [ContextMenu("デバッグ攻撃実行")]
    public void StartDebugAttack()
    {
        if (!Application.isPlaying || isTesting)
        {
            return;
        }

        if (effectPlayer == null)
        {
            Debug.LogWarning(
                "Effect Playerが未設定です。",
                this
            );
            return;
        }

        if (turnManager != null &&
            (turnManager.IsBattleFinished ||
             turnManager.CurrentPhase != BattlePhase.Player))
        {
            Debug.LogWarning(
                "PLAYER PHASE中のみテストできます。",
                this
            );
            return;
        }

        UnitView[] units = FindBattleUnits();

        UnitView attacker =
            FindAttacker(units);

        if (attacker == null)
        {
            Debug.LogWarning(
                "攻撃者が見つかりません。" +
                $" RangeType={rangeTypeFilter}",
                this
            );
            return;
        }

        UnitView target =
            FindTarget(units, attacker);

        if (target == null)
        {
            Debug.LogWarning(
                "攻撃対象の敵が見つかりません。",
                this
            );
            return;
        }

        if (applyDamage &&
            !BattleAttackExecutor.TryPreview(
                attacker, target, out _))
        {
            Debug.LogWarning(
                "ダメージ計算ができません。",
                this
            );
            return;
        }

        StartCoroutine(
            DebugAttackRoutine(attacker, target)
        );
    }

    private IEnumerator DebugAttackRoutine(
        UnitView attacker,
        UnitView target)
    {
        isTesting = true;

        string rangeType =
            attacker.State.CombatStats.RangeType;

        Debug.Log(
            $"【攻撃デバッグ開始】" +
            $"\n攻撃者：{attacker.name}" +
            $"\n対象：{target.name}" +
            $"\nRangeType：{rangeType}" +
            $"\nダメージ適用：{applyDamage}",
            this
        );

        // 1. 攻撃エフェクト
        effectPlayer.PlayAttack(
            attacker,
            target
        );

        float delay = defaultHitDelay;

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (!IsUsable(attacker, UnitTeam.Ally) ||
            !IsUsable(target, UnitTeam.Enemy))
        {
            isTesting = false;
            yield break;
        }

        // 2. ダメージ（設定ON時のみ）
        if (applyDamage)
        {
            if (!BattleAttackExecutor.TryExecute(
                attacker,
                target,
                out BattleAttackPreview result))
            {
                Debug.LogWarning(
                    "デバッグ攻撃に失敗しました。",
                    this
                );
                isTesting = false;
                yield break;
            }

            Debug.Log(
                $"【デバッグダメージ】" +
                $"\n与ダメージ：{result.AppliedDamage}" +
                $"\n残HP：{result.RemainingHP}",
                this
            );
        }

        // 3. 被弾エフェクト
        effectPlayer.PlayHit(
            attacker,
            target
        );

        // 4. 戦闘不能表示の更新
        if (applyDamage)
        {
            BattleUnitDefeatVisual visual =
                target.GetComponent<
                    BattleUnitDefeatVisual>();

            if (visual != null)
            {
                visual.Refresh();
            }
        }

        // 行動済みにはしない。
        // 繰り返しテストできるようにする。
        isTesting = false;
    }

    private UnitView FindAttacker(
        UnitView[] units)
    {
        if (attackerOverride != null)
        {
            return IsUsable(
                attackerOverride, UnitTeam.Ally)
                ? attackerOverride : null;
        }

        string filter =
            (rangeTypeFilter ?? "").Trim();

        foreach (UnitView unit in units)
        {
            if (!IsUsable(unit, UnitTeam.Ally))
            {
                continue;
            }

            string type =
                unit.State.CombatStats.RangeType;

            if (string.IsNullOrEmpty(filter) ||
                string.Equals(
                    (type ?? "").Trim(),
                    filter,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return unit;
            }
        }

        return null;
    }

    private UnitView FindTarget(
        UnitView[] units,
        UnitView attacker)
    {
        if (targetOverride != null)
        {
            return IsValidTarget(
                attacker, targetOverride)
                ? targetOverride : null;
        }

        UnitView bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (UnitView unit in units)
        {
            if (!IsValidTarget(attacker, unit))
            {
                continue;
            }

            float distance =
                (unit.transform.position -
                 attacker.transform.position)
                .sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = unit;
            }
        }

        return bestTarget;
    }

    private bool IsValidTarget(
        UnitView attacker,
        UnitView target)
    {
        if (!IsUsable(target, UnitTeam.Enemy))
        {
            return false;
        }

        if (ignoreAttackRange)
        {
            return true;
        }

        if (grid == null)
        {
            return false;
        }

        HashSet<Vector2Int> range =
            AttackRangeCalculator.Calculate(
                grid,
                attacker.State.Position,
                attacker.State.CombatStats
            );

        return AttackRangeCalculator.Contains(
            range,
            target.State.Position
        );
    }

    private static bool IsUsable(
        UnitView unit,
        UnitTeam team)
    {
        return unit != null &&
               unit.gameObject.activeInHierarchy &&
               unit.State != null &&
               unit.State.Team == team &&
               unit.State.CombatStats != null &&
               unit.State.CombatStats.CurrentHP > 0;
    }

    private static UnitView[] FindBattleUnits()
    {
#if UNITY_2022_2_OR_NEWER
        return FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        return FindObjectsOfType<UnitView>();
#endif
    }
}
