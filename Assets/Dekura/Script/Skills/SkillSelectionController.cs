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

    private UnitView user;
    private string skillId;
    private SkillTargetRule rule;
    private HashSet<Vector2Int> valid = new HashSet<Vector2Int>();
    private Vector2Int? pending;
    private int suppressUntil;

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
        Debug.Log("SkillBeginning....");

        if (!selection.TryBeginSkillSelection())
        {
            Debug.LogError("SkillFailed_CantUseSkill");
            return;
        }

        var mgr = SkillManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("SkillFailed_ManagerNull");
            return;
        }

        user = selection.SelectedUnit;

        skillId = mgr.GetSkills(user).Find(s => s.IsActive)?.Id;

        if (skillId == null)
        {
            UnitBattleStats stats = user.State.CombatStats;

            Debug.LogError($"SkillFailed_SkillIdNull{user}");
            Debug.LogError($"ThisUnitID : {stats.CharacterId}");

            End(false, false);
            return;
        }


        if (!SkillTargetRules.TryGet(skillId, out rule))
        {
            Debug.LogError("SkillFailed_SkillRuleNull");

            End(false, false);
            return;
        }

        commandPanel.Hide();

        valid = rule.GetValidCells(user, mgr);
        overlay.ShowRange(valid);

        gridInput.CellSelected += OnCell;

        // ボタン押下と同じフレームのクリックを拾わない
        suppressUntil = Time.frameCount + 1;
    }

    private void Update()
    {
        if (user != null && CancelPressed())
        {
            End(false, false);
        }
    }

    private void OnCell(Vector2Int cell)
    {
        if (Time.frameCount <= suppressUntil || !valid.Contains(cell))
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
        var mgr = SkillManager.Instance;

        var ctx = new SkillContext(
            mgr, user, rule.ResolveTargets(user, cell, mgr), cell);

        // 成功したら行動終了。失敗時は選択を続ける。
        if (mgr.TryUse(skillId, ctx))
        {
            End(true, true);
        }
    }

    private void End(bool ok, bool complete)
    {
        gridInput.CellSelected -= OnCell;

        overlay.Clear();

        pending = null;
        user = null;

        selection.TryEndSkillSelection(ok, complete);
    }

    private static bool CancelPressed() =>
        (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
        (Mouse.current?.rightButton.wasPressedThisFrame ?? false) ||
        (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);
}
