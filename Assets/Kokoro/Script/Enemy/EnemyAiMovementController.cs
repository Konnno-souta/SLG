using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyAiMovementController : MonoBehaviour
{
    [Header("グリッド")]
    [SerializeField]
    private BattleGrid grid;

    [Header("第25段階：攻撃・被弾エフェクト")]
    [SerializeField]
    private BattleCombatEffectPlayer effectPlayer;

    [Header("第26段階：戦闘カメラ")]
    [SerializeField]
    private BattleCombatCameraController combatCamera;

    [Header("デバッグ")]
    [SerializeField]
    private bool showLogs = true;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // ========================================
    // 移動計画
    // ========================================

    private sealed class MovePlan
    {
        public UnitView Target;

        public Vector2Int Destination;

        public List<Vector2Int> Path;

        public float DefenseValue;

        public int DistanceToTarget;
    }

    // ========================================
    // 敵の行動
    // ========================================

    public IEnumerator ExecuteAction(
        UnitView enemy)
    {
        if (grid == null)
        {
            Debug.LogError(
                "EnemyAiMovementController: " +
                "BattleGridが設定されていません。",
                this
            );

            yield break;
        }

        if (!IsUsable(enemy) ||
            enemy.State.Team != UnitTeam.Enemy)
        {
            yield break;
        }

        UnitState enemyState =
            enemy.State;

        UnitBattleStats enemyStats =
            enemyState.CombatStats;

        if (enemyStats == null ||
            enemyStats.IsDead ||
            enemyState.HasActed)
        {
            yield break;
        }

        UnitView[] allUnits =
            FindCurrentUnits();

        List<UnitView> allies =
            new List<UnitView>();

        HashSet<Vector2Int> occupied =
            new HashSet<Vector2Int>();

        // 全ユニットの位置と攻撃対象を取得。
        foreach (UnitView unit in allUnits)
        {
            if (!IsUsable(unit))
            {
                continue;
            }

            if (unit.State.CombatStats != null &&
                unit.State.CombatStats.IsDead)
            {
                continue;
            }

            occupied.Add(
                unit.State.Position
            );

            if (unit.State.Team == UnitTeam.Ally)
            {
                allies.Add(unit);
            }
        }

        if (allies.Count == 0)
        {
            Log(
                $"{enemy.name}: " +
                "有効な味方ユニットがいません。"
            );

            yield break;
        }

        // 行動結果を安定させるためID順に並べる。
        allies.Sort(
            (a, b) => string.CompareOrdinal(
                GetUnitId(a),
                GetUnitId(b)
            )
        );

        Vector2Int start =
            enemyState.Position;

        Func<Vector2Int, bool> isBlocked =
            position =>
                occupied.Contains(position) &&
                position != start;

        // 移動可能範囲を取得。
        // MoveRangeにはスキル補正が反映される。
        HashSet<Vector2Int> reachable =
            GridRangeCalculator.Calculate(
                grid,
                start,
                enemyState.MoveRange,
                isBlocked
            );

        // 移動せず攻撃する場合にも対応。
        reachable.Add(start);

        List<Vector2Int> moveCandidates =
            new List<Vector2Int>(reachable);

        moveCandidates.Sort(
            ComparePositions
        );

        // 物理・魔法の対象優先判定。
        bool physicalType =
            enemyStats.Attack > enemyStats.Magic;

        bool magicType =
            enemyStats.Magic > enemyStats.Attack;

        if (!physicalType && !magicType)
        {
            Debug.LogWarning(
                $"{enemy.name}: " +
                "物理攻撃力と魔法攻撃力が同じです。" +
                "対象優先順位は仮処理です。",
                enemy
            );
        }

        // ====================================
        // 攻撃可能な移動先を探索
        // ====================================

        MovePlan attackPlan =
            FindAttackPlan(
                start,
                enemyState.MoveRange,
                enemyStats,
                allies,
                moveCandidates,
                isBlocked,
                physicalType,
                magicType
            );

        if (attackPlan != null)
        {
            yield return MoveEnemy(
                enemy,
                attackPlan
            );

            UnitView target =
                attackPlan.Target;

            // 移動後の生存確認。
            if (!BattleUnitLife.IsAlive(enemy) ||
                !BattleUnitLife.IsAlive(target))
            {
                Log(
                    "【敵攻撃中止】" +
                    "攻撃側または対象が戦闘不能です。"
                );

                yield break;
            }

            UnitBattleStats currentStats =
                enemy.State.CombatStats;

            HashSet<Vector2Int> currentAttackRange =
                AttackRangeCalculator.Calculate(
                    grid,
                    enemy.State.Position,
                    currentStats
                );

            Vector2Int targetPosition =
                target.State.Position;

            // 移動後の射程確認。
            if (!AttackRangeCalculator.Contains(
                currentAttackRange,
                targetPosition))
            {
                Log(
                    $"【敵攻撃中止】{enemy.name}" +
                    $" → {target.name}" +
                    "\n移動後の位置から" +
                    "攻撃射程に入っていません。"
                );

                yield break;
            }

            // 攻撃の事前検証。
            if (!BattleAttackExecutor.TryPreview(
                enemy,
                target,
                out _))
            {
                Log(
                    $"【敵攻撃中止】{enemy.name}: " +
                    "攻撃を実行できません。"
                );

                yield break;
            }

            // =================================
            // 戦闘カメラ開始
            // =================================

            if (combatCamera != null)
            {
                yield return combatCamera.EnterCombat(
                    enemy,
                    target
                );
            }

            // =================================
            // 攻撃エフェクト
            // =================================

            float hitDelay = 0f;

            if (effectPlayer != null)
            {
                hitDelay = effectPlayer.PlayAttack(
                    enemy,
                    target
                );
            }

            if (hitDelay > 0f)
            {
                yield return new WaitForSeconds(
                    hitDelay
                );
            }

            // 攻撃演出中に戦闘不能になった場合。
            if (!BattleUnitLife.IsAlive(enemy) ||
                !BattleUnitLife.IsAlive(target))
            {
                if (combatCamera != null)
                {
                    yield return combatCamera.ExitCombat(
                        false
                    );
                }

                yield break;
            }

            // =================================
            // 攻撃実行
            // =================================

            if (!BattleAttackExecutor.TryExecute(
                enemy,
                target,
                out BattleAttackPreview result))
            {
                Debug.LogWarning(
                    $"【敵攻撃失敗】{enemy.name}" +
                    $" → {target.name}" +
                    "\nダメージ計算または" +
                    "HP減少処理に失敗しました。",
                    this
                );

                if (combatCamera != null)
                {
                    yield return combatCamera.ExitCombat(
                        false
                    );
                }

                yield break;
            }

            // =================================
            // スキルによる被弾対象変更
            // =================================

            // 「仲間を庇う」などによって、
            // 実際の被弾先が変わった場合に対応。
            target =
                result.ActualDefender ?? target;

            // =================================
            // 被弾エフェクト
            // =================================

            if (effectPlayer != null)
            {
                effectPlayer.PlayHit(
                    enemy,
                    target
                );
            }

            // =================================
            // 戦闘不能表示
            // =================================

            bool isDefeated =
                target.State.CombatStats.IsDead;

            BattleUnitDefeatVisual defeatVisual =
                target.GetComponent<
                    BattleUnitDefeatVisual>();

            if (defeatVisual != null)
            {
                defeatVisual.Refresh();
            }
            else if (isDefeated)
            {
                Debug.LogWarning(
                    $"{target.name} に" +
                    "BattleUnitDefeatVisualがありません。",
                    target
                );
            }

            string attackerName =
                enemy.State.CombatStats.CharacterName;

            string targetName =
                target.State.CombatStats.CharacterName;

            // 攻撃結果のログ。
            Log(
                "【敵AI攻撃成功】" +
                $"\n攻撃側：{attackerName}" +
                $"\n対象：{targetName}" +
                $"\n攻撃種別：{result.DamageType}" +
                $"\n計算ダメージ：{result.CalculatedDamage}" +
                $"\n実際に減ったHP：{result.AppliedDamage}" +
                $"\n対象の残HP：{result.RemainingHP}" +
                $"\n戦闘不能：{isDefeated}"
            );

            if (isDefeated)
            {
                Debug.Log(
                    $"【味方戦闘不能】{targetName}",
                    target
                );
            }

            // =================================
            // 戦闘カメラ終了
            // =================================

            if (combatCamera != null)
            {
                yield return combatCamera.ExitCombat();
            }

            yield break;
        }

        // ====================================
        // 攻撃できない場合は味方へ接近
        // ====================================

        MovePlan approachPlan =
            FindApproachPlan(
                start,
                enemyState.MoveRange,
                allies,
                moveCandidates,
                occupied,
                isBlocked
            );

        if (approachPlan != null)
        {
            Log(
                $"【敵の接近】{enemy.name}" +
                $" → {approachPlan.Target.name}"
            );

            yield return MoveEnemy(
                enemy,
                approachPlan
            );
        }
        else
        {
            Log(
                $"{enemy.name}: " +
                "移動可能な経路がないため待機。"
            );
        }
    }

    // ========================================
    // 攻撃計画の探索
    // ========================================

    private MovePlan FindAttackPlan(
        Vector2Int start,
        int moveRange,
        UnitBattleStats enemyStats,
        List<UnitView> allies,
        List<Vector2Int> moveCandidates,
        Func<Vector2Int, bool> isBlocked,
        bool physicalType,
        bool magicType)
    {
        MovePlan best = null;

        Dictionary<Vector2Int, HashSet<Vector2Int>>
            attackRanges =
                new Dictionary<
                    Vector2Int,
                    HashSet<Vector2Int>>();

        // 各移動候補の攻撃範囲を計算。
        foreach (Vector2Int cell in moveCandidates)
        {
            attackRanges[cell] =
                AttackRangeCalculator.Calculate(
                    grid,
                    cell,
                    enemyStats
                );
        }

        foreach (UnitView ally in allies)
        {
            UnitBattleStats allyStats =
                ally.State.CombatStats;

            if (allyStats == null)
            {
                continue;
            }

            float defense;

            // 物理型なら物理防御を参照。
            if (physicalType)
            {
                defense =
                    allyStats.AttackDefense;
            }
            // 魔法型なら魔法防御を参照。
            else if (magicType)
            {
                defense =
                    allyStats.MagicDefense;
            }
            else
            {
                defense = 0f;
            }

            Vector2Int targetPosition =
                ally.State.Position;

            foreach (
                Vector2Int destination
                in moveCandidates)
            {
                // 攻撃射程に入るか確認。
                if (!attackRanges[destination].Contains(
                    targetPosition))
                {
                    continue;
                }

                // 移動経路を確認。
                if (!TryCreatePath(
                    start,
                    destination,
                    moveRange,
                    isBlocked,
                    out List<Vector2Int> path))
                {
                    continue;
                }

                int steps =
                    path.Count - 1;

                // 防御力が低い味方を優先。
                bool better =
                    best == null ||
                    defense <
                        best.DefenseValue - 0.0001f;

                if (!better &&
                    best != null &&
                    Mathf.Abs(
                        defense - best.DefenseValue
                    ) < 0.0001f)
                {
                    int bestSteps =
                        best.Path.Count - 1;

                    // 防御力が同じ場合は
                    // 移動距離が短い方を優先。
                    if (steps < bestSteps)
                    {
                        better = true;
                    }
                    else if (steps == bestSteps)
                    {
                        int compare =
                            string.CompareOrdinal(
                                GetUnitId(ally),
                                GetUnitId(best.Target)
                            );

                        if (compare < 0 ||
                            (compare == 0 &&
                             ComparePositions(
                                 destination,
                                 best.Destination
                             ) < 0))
                        {
                            better = true;
                        }
                    }
                }

                if (!better)
                {
                    continue;
                }

                best = new MovePlan
                {
                    Target = ally,

                    Destination = destination,

                    Path = path,

                    DefenseValue = defense,

                    DistanceToTarget = 0
                };
            }
        }

        return best;
    }

    // ========================================
    // 接近計画の探索
    // ========================================

    private MovePlan FindApproachPlan(
        Vector2Int start,
        int moveRange,
        List<UnitView> allies,
        List<Vector2Int> moveCandidates,
        HashSet<Vector2Int> occupied,
        Func<Vector2Int, bool> isBlocked)
    {
        UnitView nearestAlly = null;

        Dictionary<Vector2Int, int>
            nearestDistances = null;

        int nearestDistance =
            int.MaxValue;

        // 最も近い味方を探索。
        foreach (UnitView ally in allies)
        {
            Dictionary<Vector2Int, int> distances =
                BuildDistancesFromTarget(
                    ally.State.Position,
                    occupied,
                    start
                );

            if (!distances.TryGetValue(
                start,
                out int distance))
            {
                continue;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;

                nearestAlly = ally;

                nearestDistances = distances;
            }
        }

        if (nearestAlly == null ||
            nearestDistances == null)
        {
            return null;
        }

        MovePlan best = null;

        // 対象との距離が最も短くなるマスを探索。
        foreach (
            Vector2Int destination
            in moveCandidates)
        {
            if (!nearestDistances.TryGetValue(
                destination,
                out int remainingDistance))
            {
                continue;
            }

            if (remainingDistance >= nearestDistance)
            {
                continue;
            }

            if (!TryCreatePath(
                start,
                destination,
                moveRange,
                isBlocked,
                out List<Vector2Int> path))
            {
                continue;
            }

            int steps =
                path.Count - 1;

            bool better =
                best == null ||
                remainingDistance <
                    best.DistanceToTarget;

            if (!better &&
                best != null &&
                remainingDistance ==
                    best.DistanceToTarget)
            {
                int bestSteps =
                    best.Path.Count - 1;

                if (steps < bestSteps ||
                    (steps == bestSteps &&
                     ComparePositions(
                         destination,
                         best.Destination
                     ) < 0))
                {
                    better = true;
                }
            }

            if (!better)
            {
                continue;
            }

            best = new MovePlan
            {
                Target = nearestAlly,

                Destination = destination,

                Path = path,

                DefenseValue = 0f,

                DistanceToTarget =
                    remainingDistance
            };
        }

        return best;
    }

    // ========================================
    // 対象を起点とした距離計算
    // ========================================

    private Dictionary<Vector2Int, int>
        BuildDistancesFromTarget(
            Vector2Int targetPosition,
            HashSet<Vector2Int> occupied,
            Vector2Int enemyPosition)
    {
        Dictionary<Vector2Int, int> distances =
            new Dictionary<Vector2Int, int>();

        if (!grid.IsInside(targetPosition))
        {
            return distances;
        }

        Queue<Vector2Int> queue =
            new Queue<Vector2Int>();

        distances[targetPosition] = 0;

        queue.Enqueue(targetPosition);

        while (queue.Count > 0)
        {
            Vector2Int current =
                queue.Dequeue();

            int nextDistance =
                distances[current] + 1;

            foreach (
                Vector2Int direction
                in Directions)
            {
                Vector2Int next =
                    current + direction;

                if (!grid.IsWalkable(next) ||
                    distances.ContainsKey(next))
                {
                    continue;
                }

                if (occupied.Contains(next) &&
                    next != enemyPosition)
                {
                    continue;
                }

                distances[next] =
                    nextDistance;

                queue.Enqueue(next);
            }
        }

        return distances;
    }

    // ========================================
    // 移動経路作成
    // ========================================

    private bool TryCreatePath(
        Vector2Int start,
        Vector2Int destination,
        int moveRange,
        Func<Vector2Int, bool> isBlocked,
        out List<Vector2Int> path)
    {
        // 移動しない場合にも対応。
        if (start == destination)
        {
            path = new List<Vector2Int>
            {
                start
            };

            return true;
        }

        return GridPathfinder.TryFindPath(
            grid,
            start,
            destination,
            moveRange,
            isBlocked,
            out path
        );
    }

    // ========================================
    // 敵の移動実行
    // ========================================

    private IEnumerator MoveEnemy(
        UnitView enemy,
        MovePlan plan)
    {
        if (plan == null ||
            plan.Path == null ||
            plan.Path.Count <= 1)
        {
            yield break;
        }

        Log(
            $"【敵移動開始】{enemy.name}" +
            $" ({enemy.State.Position.x + 1}," +
            $"{enemy.State.Position.y + 1})" +
            $" → ({plan.Destination.x + 1}," +
            $"{plan.Destination.y + 1})"
        );

        yield return enemy.MoveAlongPath(
            grid,
            plan.Path
        );

        if (enemy != null &&
            enemy.State != null)
        {
            enemy.State.CompleteMove(
                plan.Destination
            );

            enemy.SyncPosition(grid);
        }

        Log(
            $"【敵移動完了】{enemy.name}"
        );
    }

    // ========================================
    // 全ユニット取得
    // ========================================

    private UnitView[] FindCurrentUnits()
    {
#if UNITY_2022_2_OR_NEWER
        return FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        return FindObjectsOfType<UnitView>();
#endif
    }

    // ========================================
    // ユニット有効判定
    // ========================================

    private static bool IsUsable(
        UnitView unit)
    {
        return BattleUnitLife.IsAlive(unit);
    }

    // ========================================
    // ユニットID取得
    // ========================================

    private static string GetUnitId(
        UnitView unit)
    {
        if (unit == null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(
            unit.SpawnId))
        {
            return unit.SpawnId;
        }

        if (unit.MasterData != null)
        {
            return unit.MasterData.Id;
        }

        return unit.name;
    }

    // ========================================
    // 座標比較
    // ========================================

    private static int ComparePositions(
        Vector2Int a,
        Vector2Int b)
    {
        int x =
            a.x.CompareTo(b.x);

        return x != 0
            ? x
            : a.y.CompareTo(b.y);
    }

    // ========================================
    // デバッグログ
    // ========================================

    private void Log(string message)
    {
        if (showLogs)
        {
            Debug.Log(
                message,
                this
            );
        }
    }
}