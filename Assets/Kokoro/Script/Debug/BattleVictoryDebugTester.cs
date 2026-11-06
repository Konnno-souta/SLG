
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第23段階：ステージクリアの動作確認用。
///
/// PLAYER PHASE中にF8を押すと、
/// 生存している敵全員のHPを0にする。
///
/// 勝敗判定・演出再生は既存の
/// TurnManagerに任せる。
///
/// 確認後は削除または無効化すること。
/// </summary>
public sealed class BattleVictoryDebugTester
    : MonoBehaviour
{
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private bool enableDebugKey = true;

    private void Update()
    {
        if (!enableDebugKey ||
            Keyboard.current == null ||
            !Keyboard.current.f8Key.wasPressedThisFrame)
        {
            return;
        }

        if (turnManager == null ||
            turnManager.CurrentPhase != BattlePhase.Player ||
            turnManager.IsBattleFinished)
        {
            Debug.LogWarning(
                "F8テストは戦闘中の" +
                "PLAYER PHASEで実行してください。",
                this
            );
            return;
        }

#if UNITY_2022_2_OR_NEWER
        UnitView[] units =
            FindObjectsByType<UnitView>(
                FindObjectsSortMode.None
            );
#else
        UnitView[] units =
            FindObjectsOfType<UnitView>();
#endif

        int defeatedCount = 0;

        foreach (UnitView unit in units)
        {
            if (!BattleUnitLife.IsAlive(unit))
            {
                continue;
            }

            if (unit.State.Team != UnitTeam.Enemy)
            {
                continue;
            }

            UnitBattleStats stats =
                unit.State.CombatStats;

            int damage = stats.CurrentHP;

            if (!stats.TryTakeDamage(
                damage,
                out int appliedDamage))
            {
                Debug.LogWarning(
                    $"{unit.name}: HP変更に失敗",
                    unit
                );
                continue;
            }

            BattleUnitDefeatVisual visual =
                unit.GetComponent<
                    BattleUnitDefeatVisual>();

            if (visual != null)
            {
                visual.Refresh();
            }

            defeatedCount++;

            Debug.Log(
                $"【F8撃破テスト】{unit.name}" +
                $" ダメージ:{appliedDamage}",
                unit
            );
        }

        Debug.Log(
            "【F8テスト完了】" +
            $" 撃破数:{defeatedCount}" +
            "\n次のTurnManager更新で" +
            "勝利判定が行われます。",
            this
        );
    }
}
