
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第14段階：敵AIの移動と攻撃対象の判断。
///
/// プランナーの行動仕様に基づき、
/// ・物理型は物理防御が低い味方を優先
/// ・魔法型は魔法防御が低い味方を優先
/// ・移動後に攻撃できる地点を探す
/// ・攻撃できる味方がいなければ
///   最も近い味方へ接近する
///
/// 実際のダメージはまだ発生させない。
/// 行動終了はTurnManager側で行う。
/// </summary>
public sealed class EnemyAiMovementController : MonoBehaviour
{
    [Header("グリッド")]
    [SerializeField]
    private BattleGrid grid;

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

    /// <summary>
    /// AIが選んだ移動先と対象。
    /// </summary>
    private sealed class MovePlan
    {
        public UnitView Target;
        public Vector2Int Destination;
        public List<Vector2Int> Path;
        public float DefenseValue;
        public int DistanceToTarget;
    }

    // ========================================
    // 敵1体の行動
    // ========================================

    /// <summary>
    /// TurnManagerの敵フェーズから呼び出す。
    ///
    /// 移動アニメーションが終わるまで待つ。
    /// CompleteAction()はここでは呼ばない。
    /// </summary>
    public IEnumerator ExecuteAction(UnitView enemy)
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

        UnitState enemyState = enemy.State;
        UnitBattleStats enemyStats =
            enemyState.CombatStats;

        if (enemyStats == null ||
            enemyStats.IsDead ||
            enemyState.HasActed)
        {
            yield break;
        }

        // 現在のユニット配置を取得する。
        UnitView[] allUnits = FindCurrentUnits();

        List<UnitView> allies =
            new List<UnitView>();

        HashSet<Vector2Int> occupied =
            new HashSet<Vector2Int>();

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

            occupied.Add(unit.State.Position);

            if (unit.State.Team == UnitTeam.Ally)
            {
                allies.Add(unit);
            }
        }

        if (allies.Count == 0)
        {
            Log($"{enemy.name}: 有効な味方がいません。");
            yield break;
        }

        // 同点時の選択が実行ごとに
        // 不安定にならないよう順序を統一する。
        allies.Sort(
            (a, b) => string.CompareOrdinal(
                GetUnitId(a),
                GetUnitId(b)
            )
        );

        Vector2Int start =
            enemyState.Position;

        // ====================================
        // 1. 敵の移動可能範囲
        // ====================================

        Func<Vector2Int, bool> isBlocked =
            position =>
                occupied.Contains(position) &&
                position != start;

        HashSet<Vector2Int> reachable =
            GridRangeCalculator.Calculate(
                grid,
                start,
                enemyState.MoveRange,
                isBlocked
            );

        // 移動せず攻撃する場合もある。
        reachable.Add(start);

        List<Vector2Int> moveCandidates =
            new List<Vector2Int>(reachable);

        moveCandidates.Sort(ComparePositions);

        // ====================================
        // 2. 物理型か魔法型かを判定
        // ====================================

        bool physicalType =
            enemyStats.Attack > enemyStats.Magic;

        bool magicType =
            enemyStats.Magic > enemyStats.Attack;

        // 同値の場合はまだ正式な優先順位が
        // 決まっていないため、暫定で
        // 移動距離の短い攻撃を選ぶ。
        if (!physicalType && !magicType)
        {
            Debug.LogWarning(
                $"{enemy.name}: 物理攻撃力と" +
                "魔法攻撃力が同じです。" +
                "対象優先順位は仮処理です。",
                enemy
            );
        }

        // ====================================
        // 3. 今ターンに攻撃できる味方を探す
        // ====================================

        MovePlan attackPlan = FindAttackPlan(
            start,
            enemyState.MoveRange,
            enemyStats,
            allies,
            moveCandidates,
            isBlocked,
            physicalType,
            magicType
        );


        // ====================================
        // 第21段階：敵AIの攻撃実行
        // ====================================

        if (attackPlan != null)
        {
            // =================================
            // 1. 攻撃可能な位置まで移動
            // =================================

            yield return MoveEnemy(
                enemy,
                attackPlan
            );

            UnitView target = attackPlan.Target;

            // =================================
            // 2. 移動完了後の生存確認
            // =================================

            if (!BattleUnitLife.IsAlive(enemy) ||
                !BattleUnitLife.IsAlive(target))
            {
                Log(
                    "【敵攻撃中止】" +
                    "攻撃側または対象が戦闘不能です。"
                );

                yield break;
            }

            // =================================
            // 3. 現在位置から射程を再計算
            // =================================

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

            // =================================
            // 4. ダメージ計算・HP減少
            // =================================

            // 第19～20段階で使用している
            // 共通の攻撃処理を再利用。
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

                yield break;
            }

            // =================================
            // 5. 戦闘不能の表示を反映
            // =================================

            bool isDefeated =
                result.RemainingHP <= 0;

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

            // =================================
            // 6. 攻撃結果を表示
            // =================================

            string attackerName =
                enemy.State.CombatStats.CharacterName;

            string targetName =
                target.State.CombatStats.CharacterName;

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
            // 7. 敵の行動処理終了
            // =================================

            // CompleteAction()は
            // 既存TurnManagerで実行するため、
            // ここでは呼び出さない。

            yield break;
        }


        // ====================================
        // 4. 攻撃できる味方がいない場合
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
    // 攻撃する味方の選択
    // ========================================

    /// <summary>
    /// 今回の移動範囲内から、
    /// 攻撃できる敵対ユニットを探す。
    ///
    /// 物理型：物理防御が低い順
    /// 魔法型：魔法防御が低い順
    ///
    /// 防御力が同じ場合は移動歩数が
    /// 少ない方を暫定的に優先する。
    /// </summary>
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

        // 攻撃範囲の計算を移動先ごとに
        // 1回だけ実行して再利用する。
        Dictionary<Vector2Int, HashSet<Vector2Int>>
            attackRanges =
            new Dictionary<Vector2Int, HashSet<Vector2Int>>();

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

            if (physicalType)
            {
                defense =
                    allyStats.AttackDefense;
            }
            else if (magicType)
            {
                defense =
                    allyStats.MagicDefense;
            }
            else
            {
                // 攻撃値が同じ場合の正式仕様は
                // 未決定なので防御優先度は付けない。
                defense = 0f;
            }

            Vector2Int targetPosition =
                ally.State.Position;

            foreach (Vector2Int destination
                in moveCandidates)
            {
                if (!attackRanges[destination].Contains(
                    targetPosition))
                {
                    continue;
                }

                // 攻撃可能位置までの実際の経路。
                if (!TryCreatePath(
                    start,
                    destination,
                    moveRange,
                    isBlocked,
                    out List<Vector2Int> path))
                {
                    continue;
                }

                int steps = path.Count - 1;

                bool better =
                    best == null ||
                    defense < best.DefenseValue - 0.0001f;

                if (!better && best != null &&
                    Mathf.Abs(
                        defense - best.DefenseValue
                    ) < 0.0001f)
                {
                    int bestSteps =
                        best.Path.Count - 1;

                    if (steps < bestSteps)
                    {
                        better = true;
                    }
                    else if (steps == bestSteps)
                    {
                        // 最後はID順で固定。
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
    // 攻撃できないときの接近
    // ========================================

    /// <summary>
    /// 最も近い味方へ接近する。
    ///
    /// 障害物を挟んだ直線距離だけではなく、
    /// 通行可能マスを通る経路距離を使う。
    /// </summary>
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

        // まず追いかける味方を決める。
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

        // 到達可能な移動先のうち、
        // ターゲットへの距離が最小になる
        // マスを探す。
        foreach (Vector2Int destination
            in moveCandidates)
        {
            if (!nearestDistances.TryGetValue(
                destination,
                out int remainingDistance))
            {
                continue;
            }

            // 現在より近づけない場所は除外。
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

            if (!better && best != null &&
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
                DistanceToTarget = remainingDistance
            };
        }

        return best;
    }

    // ========================================
    // ターゲットまでの距離マップ
    // ========================================

    /// <summary>
    /// ターゲットのいるマスを起点として
    /// 通行可能な経路距離を逆算する。
    ///
    /// 他のユニットがいるマスは通過不可。
    /// 自分の現在位置は通行可能扱い。
    /// </summary>
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

            foreach (Vector2Int direction
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
    // 経路作成
    // ========================================

    private bool TryCreatePath(
        Vector2Int start,
        Vector2Int destination,
        int moveRange,
        Func<Vector2Int, bool> isBlocked,
        out List<Vector2Int> path)
    {
        // 移動しない場合にも、
        // 始点だけの経路を作る。
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
    // 実際の移動
    // ========================================

    /// <summary>
    /// UnitViewの既存の移動アニメーションを使う。
    ///
    /// 移動終了後にUnitStateの座標を更新。
    /// 行動終了はTurnManager側で実行する。
    /// </summary>
    private IEnumerator MoveEnemy(
        UnitView enemy,
        MovePlan plan)
    {
        if (plan == null ||
            plan.Path == null ||
            plan.Path.Count <= 1)
        {
            // 移動せず、その場から攻撃予定。
            yield break;
        }

        Log(
            $"【敵移動開始】{enemy.name}" +
            $" ({enemy.State.Position.x + 1}," +
            $"{enemy.State.Position.y + 1})" +
            $" → ({plan.Destination.x + 1}," +
            $"{plan.Destination.y + 1})"
        );

        // 1マスずつ移動させる。
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
    // ユニット取得と補助関数
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


    /// <summary>
    /// AIが利用可能な生存ユニットか。
    ///
    /// HP0の敵：
    /// ・行動しない
    ///
    /// HP0の味方：
    /// ・追跡対象にしない
    ///
    /// HP0の全ユニット：
    /// ・移動障害物にしない
    /// </summary>
    private static bool IsUsable(UnitView unit)
    {
        return BattleUnitLife.IsAlive(unit);
    }


    private static string GetUnitId(UnitView unit)
    {
        if (unit == null)
        {
            return "";
        }

        if (!string.IsNullOrEmpty(unit.SpawnId))
        {
            return unit.SpawnId;
        }

        if (unit.MasterData != null)
        {
            return unit.MasterData.Id;
        }

        return unit.name;
    }

    private static int ComparePositions(
        Vector2Int a,
        Vector2Int b)
    {
        int x = a.x.CompareTo(b.x);

        return x != 0
            ? x
            : a.y.CompareTo(b.y);
    }

    private void Log(string message)
    {
        if (showLogs)
        {
            Debug.Log(message, this);
        }
    }
}
