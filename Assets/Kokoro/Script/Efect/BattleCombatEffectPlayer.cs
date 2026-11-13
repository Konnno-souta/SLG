
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第25段階：クラス別・所属別の攻撃・被弾エフェクト。
/// 現在のClassID / RangeTypeを参照。
/// 味方は通常色、敵は赤色を使用する。
/// HPやターン管理には干渉しない。
/// </summary>
public sealed class BattleCombatEffectPlayer : MonoBehaviour
{
    public enum AttackVisual
    {
        None,
        Kesa,
        Thrust,
        Sweep,
        Rising,
        Arrow,
        Custom
    }

    public enum HitVisual
    {
        None,
        Slash,
        Pierce,
        Blunt,
        Custom
    }

    [Serializable]
    public class EffectRule
    {
        [Tooltip("個別指定するClassID。空ならRangeTypeを使う")]
        public string classId = "";

        [Tooltip("Class.csvのRangeType")]
        public string rangeType = "";

        public AttackVisual attack;
        public HitVisual hit;

        [Min(0f)]
        public float hitDelay = 0.18f;

        public float slashAngle;

        [Header("Custom使用時：味方用")]
        public GameObject customAttack;
        public GameObject customHit;

        [Header("Custom使用時：敵用")]
        public GameObject customAttackRed;
        public GameObject customHitRed;

        public EffectRule(
            string range,
            AttackVisual attackVisual,
            HitVisual hitVisual,
            float delay,
            float angle = 0f)
        {
            rangeType = range;
            attack = attackVisual;
            hit = hitVisual;
            hitDelay = delay;
            slashAngle = angle;
        }
    }

    // ====================================
    // 味方用Prefab
    // ====================================

    [Header("味方用：攻撃Prefab")]
    [SerializeField] private GameObject atkKesa;
    [SerializeField] private GameObject atkThrust;
    [SerializeField] private GameObject atkSweep;
    [SerializeField] private GameObject atkRising;
    [SerializeField] private GameObject atkArrow;

    [Header("味方用：被弾Prefab")]
    [SerializeField] private GameObject hitSlash;
    [SerializeField] private GameObject hitSlashCrit;
    [SerializeField] private GameObject hitPierce;
    [SerializeField] private GameObject hitPierceCrit;
    [SerializeField] private GameObject hitBlunt;

    // ====================================
    // 敵用Prefab
    // ====================================

    [Header("敵用：赤色攻撃Prefab")]
    [SerializeField] private GameObject atkKesaRed;
    [SerializeField] private GameObject atkThrustRed;
    [SerializeField] private GameObject atkSweepRed;
    [SerializeField] private GameObject atkRisingRed;
    [SerializeField] private GameObject atkArrowRed;

    [Header("敵用：赤色被弾Prefab")]
    [SerializeField] private GameObject hitSlashRed;
    [SerializeField] private GameObject hitSlashCritRed;
    [SerializeField] private GameObject hitPierceRed;
    [SerializeField] private GameObject hitPierceCritRed;
    [SerializeField] private GameObject hitBluntRed;

    // ====================================
    // 位置と速度
    // ====================================

    [Header("位置調整")]
    [SerializeField] private float attackHeight = 0f;
    [SerializeField] private float hitHeight = 1.1f;
    [SerializeField] private float hitForwardOffset = 0.2f;
    [SerializeField] private float arrowHeight = 1.1f;

    [Header("弓の移動速度")]
    [SerializeField, Min(0.1f)]
    private float arrowTravelSpeed = 12f;

    // ====================================
    // クラス別設定
    // ====================================

    [Header("クラス別ルール")]
    [SerializeField]
    private List<EffectRule> rules = new List<EffectRule>
    {
        new EffectRule(
            "Braver",
            AttackVisual.Kesa,
            HitVisual.Slash,
            0.18f,
            -35f),

        new EffectRule(
            "Trident",
            AttackVisual.Thrust,
            HitVisual.Pierce,
            0.17f),

        new EffectRule(
            "Spartan",
            AttackVisual.None,
            HitVisual.Blunt,
            0.12f),

        new EffectRule(
            "Phalanx",
            AttackVisual.Thrust,
            HitVisual.Pierce,
            0.17f),

        new EffectRule(
            "Cavalier",
            AttackVisual.Sweep,
            HitVisual.Slash,
            0.19f),

        new EffectRule(
            "Artemis",
            AttackVisual.Arrow,
            HitVisual.Pierce,
            0f),

        new EffectRule(
            "Light Sorcerer",
            AttackVisual.None,
            HitVisual.None,
            0f),

        new EffectRule(
            "Dark Wizard",
            AttackVisual.None,
            HitVisual.None,
            0f)
    };

    [Header("ログ")]
    [SerializeField]
    private bool showLogs = false;

    // ====================================
    // 攻撃エフェクト
    // ====================================

    /// <summary>
    /// 攻撃側のクラスとチームを判定し、
    /// 対応する攻撃エフェクトを生成する。
    /// 戻り値は着弾までの待機秒数。
    /// </summary>
    public float PlayAttack(
        UnitView attacker,
        UnitView target)
    {
        if (attacker == null || target == null)
        {
            return 0f;
        }

        EffectRule rule = FindRule(attacker);

        if (rule == null)
        {
            return 0f;
        }

        bool isEnemy =
            attacker.State.Team == UnitTeam.Enemy;

        GameObject prefab =
            GetAttackPrefab(rule, isEnemy);

        Vector3 direction =
            DirectionTo(
                attacker.transform,
                target.transform
            );

        Quaternion rotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        float delay =
            Mathf.Max(0f, rule.hitDelay);

        // 弓攻撃の場合
        if (rule.attack == AttackVisual.Arrow)
        {
            Vector3 start =
                attacker.transform.position +
                Vector3.up * arrowHeight;

            Vector3 end =
                target.transform.position +
                Vector3.up * hitHeight;

            delay =
                Vector3.Distance(start, end) /
                Mathf.Max(0.1f, arrowTravelSpeed);

            if (prefab != null)
            {
                GameObject carrier =
                    new GameObject("ArrowEffectCarrier");

                Vector3 arrowDirection = end - start;

                Quaternion arrowRotation =
                    arrowDirection.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(arrowDirection)
                    : rotation;

                carrier.transform.SetPositionAndRotation(
                    start,
                    arrowRotation
                );

                Instantiate(
                    prefab,
                    carrier.transform,
                    false
                );

                StartCoroutine(
                    MoveArrow(
                        carrier.transform,
                        start,
                        end,
                        delay
                    )
                );
            }
        }
        else if (prefab != null)
        {
            Vector3 position =
                attacker.transform.position +
                Vector3.up * attackHeight;

            Instantiate(
                prefab,
                position,
                rotation
            );
        }

        if (showLogs)
        {
            Debug.Log(
                $"【攻撃FX】{rule.rangeType}" +
                $" / {(isEnemy ? "敵・赤" : "味方")}" +
                $" / {rule.attack}" +
                $" / Delay={delay:F2}",
                this
            );
        }

        return delay;
    }

    // ====================================
    // 被弾エフェクト
    // ====================================

    /// <summary>
    /// 攻撃した側の所属に応じた
    /// 被弾エフェクトを生成する。
    /// </summary>
    public void PlayHit(
        UnitView attacker,
        UnitView target,
        bool critical = false)
    {
        if (attacker == null || target == null)
        {
            return;
        }

        EffectRule rule = FindRule(attacker);

        if (rule == null)
        {
            return;
        }

        bool isEnemy =
            attacker.State.Team == UnitTeam.Enemy;

        GameObject prefab =
            GetHitPrefab(
                rule,
                critical,
                isEnemy
            );

        if (prefab == null)
        {
            return;
        }

        Vector3 direction =
            DirectionTo(
                attacker.transform,
                target.transform
            );

        Vector3 position =
            target.transform.position +
            Vector3.up * hitHeight -
            direction * hitForwardOffset;

        Quaternion rotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );

        if (rule.hit == HitVisual.Slash)
        {
            rotation *= Quaternion.AngleAxis(
                rule.slashAngle,
                Vector3.forward
            );
        }

        Instantiate(
            prefab,
            position,
            rotation
        );

        if (showLogs)
        {
            Debug.Log(
                $"【被弾FX】{rule.rangeType}" +
                $" / {(isEnemy ? "敵・赤" : "味方")}" +
                $" / {rule.hit}",
                this
            );
        }
    }

    // ====================================
    // クラス別ルール検索
    // ====================================

    private EffectRule FindRule(UnitView attacker)
    {
        if (attacker == null ||
            attacker.State == null ||
            attacker.State.CombatStats == null)
        {
            return null;
        }

        UnitBattleStats stats =
            attacker.State.CombatStats;

        if (rules == null)
        {
            return null;
        }

        // ClassIDでの個別指定を優先
        foreach (EffectRule rule in rules)
        {
            if (rule == null ||
                string.IsNullOrWhiteSpace(rule.classId))
            {
                continue;
            }

            if (string.Equals(
                rule.classId.Trim(),
                stats.CurrentClassId,
                StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }

        // RangeTypeで判定
        foreach (EffectRule rule in rules)
        {
            if (rule == null ||
                !string.IsNullOrWhiteSpace(rule.classId))
            {
                continue;
            }

            if (string.Equals(
                rule.rangeType?.Trim(),
                stats.RangeType?.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }

        Debug.LogWarning(
            $"【攻撃FX未設定】" +
            $" ClassID={stats.CurrentClassId}" +
            $" / RangeType={stats.RangeType}",
            attacker
        );

        return null;
    }

    // ====================================
    // 攻撃Prefabの選択
    // ====================================

    private GameObject GetAttackPrefab(
        EffectRule rule,
        bool isEnemy)
    {
        switch (rule.attack)
        {
            case AttackVisual.Kesa:
                return isEnemy
                    ? atkKesaRed
                    : atkKesa;

            case AttackVisual.Thrust:
                return isEnemy
                    ? atkThrustRed
                    : atkThrust;

            case AttackVisual.Sweep:
                return isEnemy
                    ? atkSweepRed
                    : atkSweep;

            case AttackVisual.Rising:
                return isEnemy
                    ? atkRisingRed
                    : atkRising;

            case AttackVisual.Arrow:
                return isEnemy
                    ? atkArrowRed
                    : atkArrow;

            case AttackVisual.Custom:
                return isEnemy
                    ? rule.customAttackRed
                    : rule.customAttack;

            default:
                return null;
        }
    }

    // ====================================
    // 被弾Prefabの選択
    // ====================================

    private GameObject GetHitPrefab(
        EffectRule rule,
        bool critical,
        bool isEnemy)
    {
        switch (rule.hit)
        {
            case HitVisual.Slash:
                {
                    GameObject slash =
                        isEnemy
                        ? hitSlashRed
                        : hitSlash;

                    GameObject slashCrit =
                        isEnemy
                        ? hitSlashCritRed
                        : hitSlashCrit;

                    return critical && slashCrit != null
                        ? slashCrit
                        : slash;
                }

            case HitVisual.Pierce:
                {
                    GameObject pierce =
                        isEnemy
                        ? hitPierceRed
                        : hitPierce;

                    GameObject pierceCrit =
                        isEnemy
                        ? hitPierceCritRed
                        : hitPierceCrit;

                    return critical && pierceCrit != null
                        ? pierceCrit
                        : pierce;
                }

            case HitVisual.Blunt:
                return isEnemy
                    ? hitBluntRed
                    : hitBlunt;

            case HitVisual.Custom:
                return isEnemy
                    ? rule.customHitRed
                    : rule.customHit;

            default:
                return null;
        }
    }

    // ====================================
    // 攻撃方向
    // ====================================

    private static Vector3 DirectionTo(
        Transform attacker,
        Transform target)
    {
        Vector3 dir =
            target.position - attacker.position;

        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = attacker.forward;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.forward;
        }

        return dir.normalized;
    }

    // ====================================
    // 弓エフェクトの移動
    // ====================================

    private IEnumerator MoveArrow(
        Transform carrier,
        Vector3 start,
        Vector3 end,
        float duration)
    {
        float elapsed = 0f;

        while (carrier != null &&
               elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = duration > 0f
                ? Mathf.Clamp01(elapsed / duration)
                : 1f;

            carrier.position =
                Vector3.Lerp(start, end, t);

            yield return null;
        }

        if (carrier != null)
        {
            carrier.position = end;

            Destroy(
                carrier.gameObject,
                0.15f
            );
        }
    }
}
