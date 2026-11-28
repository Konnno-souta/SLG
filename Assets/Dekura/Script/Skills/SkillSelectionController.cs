using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// スキルの対象選択を担当する。
// UnitSelectionController.TryBeginSkillSelection 中は既存の入力処理が止まるため、
// クリック受付・範囲表示・キャンセルはここで行う。
public sealed class SkillSelectionController : MonoBehaviour
{
    [SerializeField] private UnitSelectionController selection;
    [SerializeField] private GridClickInput gridInput;
    [SerializeField] private BattleCommandPanel commandPanel;
    [SerializeField] private AttackRangeOverlay overlay;

    [Header("通常攻撃型スキル用(UnitSelectionControllerと同じものを設定)")]
    [SerializeField] private AttackTargetPreviewPanel attackPreviewPanel;
    [SerializeField] private BattleCombatEffectPlayer effectPlayer;
    [SerializeField] private BattleCombatCameraController combatCamera;

    [Header("移動を伴うスキル用(UnitSelectionControllerと同じものを設定)")]
    [SerializeField] private GridDebugView gridDebugView;

    private UnitView user;
    private string skillId;
    private SkillTargetRule rule;
    private HashSet<Vector2Int> valid = new HashSet<Vector2Int>();
    private Vector2Int? pending;
    private int suppressUntil;

    // 通常攻撃型スキル用
    private UnitView attackCandidate;

    // 移動フェーズ用(エアなど)
    private bool inMovePhase;
    private int moveRange;
    private HashSet<Vector2Int> moveReachable = new HashSet<Vector2Int>();

    // 配置フェーズ用(エアの降ろす先)
    private bool inPlacePhase;
    private HashSet<Vector2Int> placeCells = new HashSet<Vector2Int>();

    // 演出・移動アニメ中は入力を受け付けない
    private bool isExecuting;

    private void Start()
    {
        commandPanel.SkillRequested += Begin;
    }

    private void OnDestroy()
    {
        if (commandPanel != null)
        {
            commandPanel.SkillRequested -= Begin;
        }
    }

    // 戦技ボタンから呼ばれる
    private void Begin()
    {
        Debug.Log("SkillBegining...");

        var mgr = SkillManager.Instance;

        if (mgr == null || !selection.TryBeginSkillSelection())
        {
            Debug.LogError("SkillBegin_CantUse");
            return;
        }

        user = selection.SelectedUnit;

        skillId = mgr.GetSkills(user).Find(s => s.IsActive)?.Id;

        if (skillId == null)
        {
            UnitBattleStats stats = user.State.CombatStats;
            string characterId = stats.CharacterId;
            string characterName = stats.CharacterName;

            Debug.LogError("SkillBegin_IdNull");
            Debug.LogError($"Target({characterName}) Id : {characterId}");
            End(false, false);
            return;
        }
        if(!SkillTargetRules.TryGet(skillId, out rule))
        {
            Debug.LogError("SkillBegin_RuleNull");
            End(false, false);
            return;
        }

        // 使用条件を満たさないなら、選択に入らずコマンドメニューへ戻る
        if (!mgr.CanUse(skillId, new SkillContext(mgr, user)))
        {
            Debug.Log($"[Skill] {skillId} は現在使用できません。");
            End(false, false);
            return;
        }

        commandPanel.Hide();

        valid = rule.GetValidCells(user, mgr);
        overlay.ShowRange(valid);

        // 通常攻撃型: ダメージ予測パネルを表示(通常攻撃と同じ)
        if (rule.AttackCount > 0 && attackPreviewPanel != null)
        {
            attackPreviewPanel.Show();

            if (valid.Count == 0)
            {
                UnitBattleStats stats = user.State.CombatStats;

                attackPreviewPanel.ShowNoRange(
                    stats.CurrentClassName, stats.RangeType);
            }
        }

        attackCandidate = null;
        pending = null;
        inMovePhase = false;
        inPlacePhase = false;

        gridInput.CellSelected += OnCell;

        // ボタン押下と同じフレームのクリックを拾わない
        suppressUntil = Time.frameCount + 1;
    }

    private void Update()
    {
        // 配置フェーズ(移動が済んだ後)はキャンセルできない
        if (user != null && !isExecuting && !inPlacePhase && CancelPressed())
        {
            if (inMovePhase)
            {
                CancelMovePhase();
            }
            else
            {
                End(false, false);
            }
        }
    }

    private void OnCell(Vector2Int cell)
    {
        if (isExecuting || Time.frameCount <= suppressUntil)
        {
            return;
        }

        // 配置フェーズ(エアの降ろす先)
        if (inPlacePhase)
        {
            OnPlaceCell(cell);
            return;
        }

        // 移動フェーズ(エアの運搬後の移動)
        if (inMovePhase)
        {
            OnMoveCell(cell);
            return;
        }

        // 通常攻撃型は専用の流れ
        if (rule.AttackCount > 0)
        {
            OnAttackCell(cell);
            return;
        }

        if (!valid.Contains(cell))
        {
            return;
        }

        var mgr = SkillManager.Instance;

        List<UnitView> targets = rule.ResolveTargets(user, cell, mgr);

        // 対象が必要なスキルは、対象がいないマスでは何もしない
        if (rule.RequireTarget &&
            (targets == null || targets.Count == 0))
        {
            return;
        }

        // 1回目: 影響範囲のプレビュー
        if (pending != cell)
        {
            pending = cell;
            overlay.ShowRange(rule.GetAffectedCells(user, cell));
            return;
        }

        // 2回目(同じマス): 確定
        var ctx = new SkillContext(mgr, user, targets, cell);

        if (!mgr.TryUse(skillId, ctx))
        {
            return;   // 失敗時は選択を続ける
        }

        if (rule.FollowUpMoveRange > 0)
        {
            // 続けて移動先を選ばせる(行動終了はまだしない)
            BeginMovePhase(mgr);
        }
        else
        {
            End(true, true);
        }
    }

    // ========================================
    // 移動フェーズ(エアなど)
    // 通常の移動と同じ: 範囲表示 → クリックで経路移動
    // ========================================

    private bool IsCellOccupied(Vector2Int position) =>
        SkillBattleAPI.IsOccupied(
            position, SkillManager.Instance.AllUnits);

    private void BeginMovePhase(SkillManager mgr)
    {
        inMovePhase = true;
        pending = null;
        moveRange = rule.FollowUpMoveRange;

        overlay.Clear();

        moveReachable = GridRangeCalculator.Calculate(
            mgr.Grid,
            user.State.Position,
            moveRange,
            IsCellOccupied);

        // 移動後に配置するスキル: 配置マスが1つも無い移動先は選べない
        // (降ろせず詰むのを防ぐ)
        if (rule.GetPlaceCells != null)
        {
            moveReachable.RemoveWhere(
                c => rule.GetPlaceCells(user, c, mgr).Count == 0);
        }

        if (moveReachable.Count == 0)
        {
            Debug.Log("[Skill] 移動できるマスがありません。Esc で取り消せます。");
        }

        if (gridDebugView != null)
        {
            gridDebugView.ShowMovementRange(moveReachable);
        }

        // 確定クリックと同じフレームの入力を拾わない
        suppressUntil = Time.frameCount + 1;
    }

    private void OnMoveCell(Vector2Int destination)
    {
        var mgr = SkillManager.Instance;

        if (!moveReachable.Contains(destination) ||
            !mgr.Grid.IsWalkable(destination) ||
            IsCellOccupied(destination))
        {
            return;
        }

        bool found = GridPathfinder.TryFindPath(
            mgr.Grid,
            user.State.Position,
            destination,
            moveRange,
            IsCellOccupied,
            out List<Vector2Int> path);

        if (!found)
        {
            Debug.LogWarning($"移動経路が見つかりません:{destination}");
            return;
        }

        StartCoroutine(MoveRoutine(user, destination, path));
    }

    private IEnumerator MoveRoutine(
        UnitView unit, Vector2Int destination, List<Vector2Int> path)
    {
        isExecuting = true;

        gridInput.SetGridInputEnabled(false);

        if (gridDebugView != null)
        {
            gridDebugView.ClearMovementRange();
        }

        yield return StartCoroutine(
            unit.MoveAlongPath(SkillManager.Instance.Grid, path));

        unit.State.CompleteMove(destination);

        isExecuting = false;

        gridInput.SetGridInputEnabled(true);

        inMovePhase = false;

        if (rule.GetPlaceCells != null)
        {
            // 続けて配置マス(降ろす先)を選ばせる
            BeginPlacePhase(SkillManager.Instance);
        }
        else
        {
            // 移動後に行動終了
            End(true, true);
        }
    }

    // 移動選択中(移動前)のキャンセル: スキルの効果を巻き戻してコマンドメニューへ戻る
    private void CancelMovePhase()
    {
        if (gridDebugView != null)
        {
            gridDebugView.ClearMovementRange();
        }

        rule.OnFollowUpCancelled?.Invoke(user, SkillManager.Instance);

        End(false, false);
    }

    // ========================================
    // 配置フェーズ(エアの降ろす先)
    // 移動完了後、前後左右の空きマスを選ぶ → 味方を移動して隠し状態を解除 → スキル終了
    // ========================================

    private void BeginPlacePhase(SkillManager mgr)
    {
        inPlacePhase = true;

        placeCells = rule.GetPlaceCells(user, user.State.Position, mgr);

        if (placeCells.Count == 0)
        {
            // 移動先の絞り込みで通常は起きない。詰まないようスキルを終了する。
            Debug.LogWarning("[Skill] 配置できるマスがありません。スキルを終了します。");
            End(true, true);
            return;
        }

        overlay.ShowRange(placeCells);

        // 移動完了と同じフレームの入力を拾わない
        suppressUntil = Time.frameCount + 1;
    }

    private void OnPlaceCell(Vector2Int cell)
    {
        if (!placeCells.Contains(cell))
        {
            return;
        }

        // 配置に失敗したら選択を続ける
        if (!rule.OnPlace(user, cell, SkillManager.Instance))
        {
            return;
        }

        End(true, true);
    }

    // ========================================
    // 通常攻撃型スキル(リオンなど)
    // 対象選択は UnitSelectionController の攻撃と同じ:
    // 敵を選ぶとダメージ予測、同じ敵をもう一度選ぶと実行。
    // ========================================

    private void OnAttackCell(Vector2Int cell)
    {
        var mgr = SkillManager.Instance;

        UnitView target = FindEnemyAt(mgr, cell);

        // 敵がいないマス
        if (target == null)
        {
            attackCandidate = null;

            if (attackPreviewPanel != null)
            {
                attackPreviewPanel.ShowSearching();
            }

            return;
        }

        // 攻撃範囲外の敵
        if (!valid.Contains(cell))
        {
            attackCandidate = null;

            if (attackPreviewPanel != null)
            {
                attackPreviewPanel.ShowOutOfRange(target);
            }

            return;
        }

        // 1回目: 予測表示
        if (attackCandidate != target)
        {
            attackCandidate = target;

            if (attackPreviewPanel != null)
            {
                attackPreviewPanel.ShowCandidate(user, target);
            }

            return;
        }

        // 2回目(同じ敵): 実行
        var ctx = new SkillContext(
            mgr, user, new List<UnitView> { target }, cell);

        if (!mgr.CanUse(skillId, ctx) ||
            !BattleAttackExecutor.TryPreview(user, target, out _))
        {
            attackCandidate = null;

            if (attackPreviewPanel != null)
            {
                attackPreviewPanel.ShowSearching();
            }

            return;
        }

        StartCoroutine(AttackRoutine(user, target, rule.AttackCount));
    }

    private UnitView FindEnemyAt(SkillManager mgr, Vector2Int cell)
    {
        foreach (UnitView u in mgr.AllUnits)
        {
            if (!SkillBattleAPI.IsAlive(u)) continue;
            if (u.State.Team == user.State.Team) continue;

            if (u.State.Position == cell)
            {
                return u;
            }
        }

        return null;
    }

    // UnitSelectionController.ExecuteAttackEffectRoutine と同じ流れを、
    // 指定回数だけ繰り返す(カメラは最初に入り、最後に出る)
    private IEnumerator AttackRoutine(
        UnitView attacker, UnitView target, int count)
    {
        isExecuting = true;

        gridInput.SetGridInputEnabled(false);

        overlay.Clear();

        if (attackPreviewPanel != null)
        {
            attackPreviewPanel.Hide();
        }

        // 1. 戦闘カメラ開始
        if (combatCamera != null)
        {
            yield return combatCamera.EnterCombat(attacker, target);
        }

        bool anySucceeded = false;

        for (int i = 0; i < count; i++)
        {
            if (!SkillBattleAPI.IsAlive(attacker) ||
                !SkillBattleAPI.IsAlive(target))
            {
                break;
            }

            // 2. 攻撃エフェクト
            float hitDelay = 0.18f;

            if (effectPlayer != null)
            {
                hitDelay = effectPlayer.PlayAttack(attacker, target);
            }

            // 3. 被弾まで待機
            if (hitDelay > 0f)
            {
                yield return new WaitForSeconds(hitDelay);
            }

            if (!SkillBattleAPI.IsAlive(attacker) ||
                !SkillBattleAPI.IsAlive(target))
            {
                break;
            }

            // 4. ダメージ処理
            if (!BattleAttackExecutor.TryExecute(
                    attacker, target, out BattleAttackPreview result))
            {
                break;
            }

            anySucceeded = true;

            // 庇う等で被弾対象が変わった場合に対応
            target = result.ActualDefender ?? target;

            // 5. 被弾エフェクト・戦闘不能表示
            if (effectPlayer != null)
            {
                effectPlayer.PlayHit(attacker, target);
            }

            SkillBattleAPI.RefreshDefeatVisual(target);

            Debug.Log(
                $"【{skillId}】{i + 1}/{count}回目 " +
                $"ダメージ={result.AppliedDamage} 残HP={result.RemainingHP}");
        }

        // 6. 戦闘カメラ終了
        if (combatCamera != null)
        {
            if (anySucceeded)
            {
                yield return combatCamera.ExitCombat();
            }
            else
            {
                yield return combatCamera.ExitCombat(false);
            }
        }

        isExecuting = false;

        gridInput.SetGridInputEnabled(true);

        attackCandidate = null;

        if (anySucceeded)
        {
            // 行動終了して選択解除
            End(true, true);
        }
        else
        {
            // 1回も当たらなかった場合は選択に戻る
            overlay.ShowRange(valid);

            if (attackPreviewPanel != null)
            {
                attackPreviewPanel.Show();
                attackPreviewPanel.ShowSearching();
            }
        }
    }

    private void End(bool ok, bool complete)
    {
        gridInput.CellSelected -= OnCell;

        overlay.Clear();

        if (attackPreviewPanel != null)
        {
            attackPreviewPanel.Hide();
        }

        if (gridDebugView != null)
        {
            gridDebugView.ClearMovementRange();
        }

        pending = null;
        attackCandidate = null;
        inMovePhase = false;
        inPlacePhase = false;
        user = null;

        selection.TryEndSkillSelection(ok, complete);
    }

    private static bool CancelPressed() =>
        (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
        (Mouse.current?.rightButton.wasPressedThisFrame ?? false) ||
        (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);
}