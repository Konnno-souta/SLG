
using UnityEngine;

/// <summary>
/// 攻撃・被弾エフェクトの表示担当。
///
/// ダメージ計算やHP変更は行わない。
/// </summary>
public sealed class BattleCombatEffectPlayer : MonoBehaviour
{
    [Header("攻撃エフェクト")]
    [SerializeField]
    private GameObject attackPrefab;

    [Header("被弾エフェクト")]
    [SerializeField]
    private GameObject hitPrefab;

    [Header("表示位置")]
    [SerializeField]
    private float attackHeight = 0f;

    [SerializeField]
    private float hitHeight = 1.1f;

    [Header("斬撃の角度")]
    [SerializeField]
    private float slashAngle = -35f;

    // 攻撃開始時
    public void PlayAttack(
        Transform attacker,
        Transform target)
    {
        if (attackPrefab == null ||
            attacker == null ||
            target == null)
        {
            return;
        }

        Quaternion rotation =
            GetAttackRotation(attacker, target);

        Vector3 position =
            attacker.position +
            Vector3.up * attackHeight;

        Instantiate(
            attackPrefab,
            position,
            rotation
        );
    }

    // ダメージが命中した時
    public void PlayHit(
        Transform attacker,
        Transform target)
    {
        if (hitPrefab == null ||
            attacker == null ||
            target == null)
        {
            return;
        }

        Quaternion rotation =
            GetAttackRotation(attacker, target) *
            Quaternion.Euler(0f, 0f, slashAngle);

        Vector3 position =
            target.position +
            Vector3.up * hitHeight;

        Instantiate(
            hitPrefab,
            position,
            rotation
        );
    }

    // 攻撃者から対象へ向かう向きを計算
    private Quaternion GetAttackRotation(
        Transform attacker,
        Transform target)
    {
        Vector3 direction =
            target.position - attacker.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = attacker.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        return Quaternion.LookRotation(
            direction.normalized,
            Vector3.up
        );
    }
}
