
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第19段階：HP減少の動作確認専用。
///
/// PLAYER PHASE中にF7を押すと、
/// 敵1体にテストダメージを与える。
///
/// 戦闘AIや攻撃対象選択には接続しない。
/// 正式な攻撃実装後は無効化する。
/// </summary>
public sealed class BattleHPDebugTester : MonoBehaviour
{
    [Header("ターン管理")]
    [SerializeField]
    private TurnManager turnManager;

    [Header("テスト用")]
    [SerializeField, Min(1)]
    private int testDamage = 10;

    [SerializeField]
    private bool enableTest = true;

#if UNITY_EDITOR
    private void Update()
    {
        if (!enableTest ||
            Keyboard.current == null ||
            !Keyboard.current.f7Key.wasPressedThisFrame)
        {
            return;
        }

        if (turnManager == null ||
            turnManager.CurrentPhase !=
                BattlePhase.Player)
        {
            Debug.LogWarning(
                "HPテストはPLAYER PHASE中に行ってください。",
                this
            );
            return;
        }

        UnitView[] units =
            FindObjectsByType<UnitView>(
                FindObjectsSortMode.None
            );

        Array.Sort(
            units,
            (a, b) => string.CompareOrdinal(
                a.SpawnId,
                b.SpawnId
            )
        );

        foreach (UnitView unit in units)
        {
            if (unit == null ||
                unit.State == null ||
                unit.State.Team != UnitTeam.Enemy)
            {
                continue;
            }

            UnitBattleStats stats =
                unit.State.CombatStats;

            if (stats == null || stats.CurrentHP <= 1)
            {
                continue;
            }

            // この段階では敵を撃破させず、
            // HP減少だけを安全に確認する。
            int damage = Mathf.Min(
                testDamage,
                stats.CurrentHP - 1
            );

            if (!stats.TryTakeDamage(
                damage,
                out int appliedDamage))
            {
                continue;
            }

            Debug.Log(
                "【HP減少テスト成功】" +
                $"\nキャラID：{stats.CharacterId}" +
                $"\n名前：{stats.CharacterName}" +
                $"\nダメージ：{appliedDamage}" +
                $"\n残HP：{stats.CurrentHP}/{stats.MaxHP}",
                unit
            );

            return;
        }

        Debug.LogWarning(
            "テスト可能な敵が見つかりません。",
            this
        );
    }
#endif
}
