using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SkillManager : MonoBehaviour
{
    // ========================================
    // Inspector参照
    // ========================================

    [Header("マップ参照")]
    [SerializeField]
    private BattleGrid grid;

    [Header("ターン管理")]
    [SerializeField]
    private TurnManager turnManager;

    // ========================================
    // インスタンス管理
    // ========================================

    public static SkillManager Instance
    {
        get;
        private set;
    }

    // ========================================
    // スキル登録情報
    // ========================================

    private readonly Dictionary<string, SkillData>
        skills = new Dictionary<string, SkillData>();

    // ========================================
    // 共通情報
    // ========================================

    public BattleGrid Grid => grid;

    public IReadOnlyList<UnitView> AllUnits =>
        turnManager.BattleUnits;

    public int Turn =>
        turnManager.TurnNumber;

    public BattlePhase Phase =>
        turnManager.CurrentPhase;

    // ========================================
    // 戦闘イベント
    // ========================================

    // 攻撃計算前
    public event Action<SkillAttackContext>
        AttackPreparing;

    // ダメージ適用前
    public event Action<SkillAttackContext>
        DamagePreparing;

    // 攻撃完了後
    public event Action<
        SkillAttackContext,
        BattleAttackPreview>
        AttackResolved;

    // ユニットの行動終了時
    public event Action<UnitView>
        ActionCompleted;

    // フェーズ開始時
    public event Action<BattlePhase, int>
        PhaseStarted;

    // 新しいターンの開始時
    public event Action<int>
        BattleTurnStarted;

    // セーブデータ復元時
    public event Action<int>
        BattleRestored;

    // ========================================
    // 初期化
    // ========================================

    private void Awake()
    {
        // 重複防止
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "SkillManagerが重複しています。",
                this
            );

            enabled = false;

            return;
        }

        // Inspector参照確認
        if (grid == null || turnManager == null)
        {
            Debug.LogError(
                "SkillManagerの参照が不足しています。",
                this
            );

            enabled = false;

            return;
        }

        Instance = this;
    }

    // ========================================
    // 破棄処理
    // ========================================

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ========================================
    // スキル登録
    // ========================================

    /// <summary>
    /// 個別スキルを共通システムに登録する。
    /// 同じIDの重複登録は禁止。
    /// </summary>
    public bool Register(SkillData data)
    {
        if (data == null ||
            skills.ContainsKey(data.Id))
        {
            return false;
        }

        skills.Add(data.Id, data);

        return true;
    }

    // ========================================
    // スキル登録解除
    // ========================================

    public bool Unregister(string id)
    {
        return id != null &&
               skills.Remove(id);
    }

    // ========================================
    // 所有スキル取得
    // ========================================

    /// <summary>
    /// 指定したユニットが所有する
    /// 固有スキル・クラススキルを取得する。
    /// </summary>
    public List<SkillData> GetSkills(
        UnitView user)
    {
        List<SkillData> result =
            new List<SkillData>();

        if (!SkillBattleAPI.IsAlive(user))
        {
            return result;
        }

        foreach (SkillData skill in skills.Values)
        {
            if (skill.IsAvailable(user))
            {
                result.Add(skill);
            }
        }

        return result;
    }

    // ========================================
    // スキル使用可能判定
    // ========================================

    public bool CanUse(
        string id,
        SkillContext context)
    {
        if (context == null ||
            context.Manager != this ||
            turnManager.IsBattleFinished ||
            !SkillBattleAPI.IsAlive(context.User) ||
            context.User.State.HasActed ||
            !SkillBattleAPI.ContainsUnit(
                AllUnits,
                context.User) ||
            id == null ||
            !skills.TryGetValue(
                id,
                out SkillData data) ||
            !data.IsActive ||
            !data.IsAvailable(context.User))
        {
            return false;
        }

        UnitTeam team =
            context.User.State.Team;

        // 現在のフェーズと所属を確認
        if ((Phase == BattlePhase.Player &&
             team != UnitTeam.Ally) ||
            (Phase == BattlePhase.Enemy &&
             team != UnitTeam.Enemy) ||
            Phase == BattlePhase.None)
        {
            return false;
        }

        // 個別スキルの使用条件を確認
        return data.CanUse == null ||
               data.CanUse(context);
    }

    // ========================================
    // スキル実行
    // ========================================

    /// <summary>
    /// 指定したスキルを実行する。
    ///
    /// 対象選択・演出・行動終了は
    /// 呼び出し側で管理する。
    /// </summary>
    public bool TryUse(
        string id,
        SkillContext context)
    {
        if (!CanUse(id, context))
        {
            return false;
        }

        return skills[id].Execute(context);
    }

    // ========================================
    // 攻撃計算前通知
    // ========================================

    internal void NotifyAttackPreparing(
        SkillAttackContext context)
    {
        AttackPreparing?.Invoke(context);
    }

    // ========================================
    // ダメージ適用前通知
    // ========================================

    internal void NotifyDamagePreparing(
        SkillAttackContext context)
    {
        DamagePreparing?.Invoke(context);
    }

    // ========================================
    // 攻撃完了通知
    // ========================================

    internal void NotifyAttackResolved(
        SkillAttackContext context,
        BattleAttackPreview result)
    {
        AttackResolved?.Invoke(
            context,
            result
        );
    }

    // ========================================
    // 行動終了通知
    // ========================================

    internal void NotifyActionCompleted(
        UnitView unit)
    {
        ActionCompleted?.Invoke(unit);
    }

    // ========================================
    // フェーズ開始通知
    // ========================================

    internal void NotifyPhaseStarted(
        BattlePhase phase,
        int turn)
    {
        // 新しいターンの開始時に
        // スキルの効果時間を更新する。
        //
        // PLAYER PHASEのみで更新することで
        // 1ターンに2回更新されることを防ぐ。
        if (phase == BattlePhase.Player)
        {
            foreach (UnitView unit in AllUnits)
            {
                if (unit == null ||
                    unit.State == null ||
                    unit.State.CombatStats == null)
                {
                    continue;
                }

                unit.State.CombatStats
                    .Skills.ExpireAtTurn(turn);
            }

            BattleTurnStarted?.Invoke(turn);
        }

        PhaseStarted?.Invoke(
            phase,
            turn
        );
    }

    // ========================================
    // ロード完了通知
    // ========================================

    // 通常のターン開始とは区別する。
    // ロードしただけで効果時間を減らさない。
    internal void NotifyBattleRestored(
        int turn)
    {
        BattleRestored?.Invoke(turn);
    }
}