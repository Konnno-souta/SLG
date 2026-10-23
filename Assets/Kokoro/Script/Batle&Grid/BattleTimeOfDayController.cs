
using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 戦闘中の朝夜管理。
///
/// ・4ターンごとに朝夜を切り替える
/// ・全ユニットのクラスを変更する
/// ・ステータスと移動力を再計算する
/// ・朝夜UIを更新する
///
/// 1ターンはPLAYER PHASEとENEMY PHASEの
/// 両方が終了した単位として数える。
///
/// TurnManagerのPhaseChangedイベントを使用する。
/// </summary>
public sealed class BattleTimeOfDayController : MonoBehaviour
{
    // ====================================
    // 参照
    // ====================================

    [Header("ターン管理")]
    [SerializeField]
    private TurnManager turnManager;

    [Header("戦闘開始時の時間帯")]
    [SerializeField]
    private BattleClassMode initialMode =
        BattleClassMode.Light;

    [Header("昼夜切り替え")]
    [SerializeField, Min(1)]
    private int turnsPerPeriod = 4;

    [Header("UI")]
    [SerializeField]
    private TMP_Text timeOfDayText;

    [Header("開発用")]
    [SerializeField]
    private bool enableDebugToggleKey = true;

    [SerializeField]
    private bool showLogs = true;

    // ====================================
    // 現在の状態
    // ====================================

    public BattleClassMode CurrentMode
    {
        get;
        private set;
    } = BattleClassMode.Light;

    /// <summary>
    /// 朝夜が切り替わったときの通知。
    /// 将来の背景・照明・演出などに使用する。
    /// </summary>
    public event Action<BattleClassMode> ModeChanged;

    // ====================================
    // 初期化
    // ====================================

    private void Awake()
    {
        CurrentMode = initialMode;

        UpdateTimeOfDayText();
    }

    private void OnEnable()
    {
        if (turnManager != null)
        {
            turnManager.PhaseChanged +=
                OnPhaseChanged;
        }
    }

    private void Start()
    {
        if (turnManager == null)
        {
            Debug.LogError(
                "BattleTimeOfDayController: " +
                "TurnManagerが設定されていません。" +
                "自動切り替えを使用できません。",
                this
            );

            return;
        }

        // このコンポーネントが後から有効になった場合にも
        // 現在のターン数に合わせる。
        if (turnManager.CurrentPhase ==
            BattlePhase.Player)
        {
            ApplyModeForTurn(turnManager.TurnNumber);
        }
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.PhaseChanged -=
                OnPhaseChanged;
        }
    }

    // ====================================
    // ターン変更通知
    // ====================================

    /// <summary>
    /// TurnManagerからフェーズ変更を受け取る。
    ///
    /// PLAYER PHASE開始時にだけ
    /// 朝夜の更新を確認する。
    /// </summary>
    private void OnPhaseChanged(
        BattlePhase phase,
        int turnNumber)
    {
        if (phase != BattlePhase.Player)
        {
            return;
        }

        ApplyModeForTurn(turnNumber);
    }

    /// <summary>
    /// ターン番号から時間帯を決定する。
    ///
    /// 初期がLightの場合：
    /// TURN 1～4   Light
    /// TURN 5～8   Dark
    /// TURN 9～12  Light
    /// TURN 13～16 Dark
    ///
    /// 初期がDarkの場合は逆になる。
    /// </summary>
    private void ApplyModeForTurn(int turnNumber)
    {
        if (turnNumber < 1)
        {
            return;
        }

        int periodLength =
            Mathf.Max(1, turnsPerPeriod);

        // 0なら最初の時間帯。
        // 1なら次の時間帯。
        // 2なら再び最初の時間帯。
        int periodIndex =
            (turnNumber - 1) / periodLength;

        bool useInitialMode =
            periodIndex % 2 == 0;

        BattleClassMode nextMode =
            useInitialMode
                ? initialMode
                : OppositeMode(initialMode);

        // 前回と違う場合のみ切り替える
        SetMode(nextMode);
    }

    // ====================================
    // 昼夜の変更
    // ====================================

    /// <summary>
    /// 朝から夜、夜から朝へ切り替える。
    ///
    /// 現状はTキーによるテスト用。
    /// 次のPLAYER PHASE開始時には
    /// 正式なターン数から時間帯を再判定する。
    /// </summary>
    public void ToggleMode()
    {
        SetMode(OppositeMode(CurrentMode));
    }

    /// <summary>
    /// 指定した時間帯に変更する。
    ///
    /// クラス・ステータス・移動力を更新する。
    /// </summary>
    public void SetMode(BattleClassMode mode)
    {
        if (CurrentMode == mode)
        {
            return;
        }

        CurrentMode = mode;

        // 味方と敵のクラスを更新
        ApplyCurrentModeToExistingUnits();

        // 朝夜表示を更新
        UpdateTimeOfDayText();

        // 他のシステムへ通知
        ModeChanged?.Invoke(CurrentMode);

        if (showLogs)
        {
            Debug.Log(
                "【昼夜切り替え】" +
                GetModeName(CurrentMode),
                this
            );
        }
    }

    /// <summary>
    /// 朝夜を逆にする共通処理。
    /// </summary>
    private BattleClassMode OppositeMode(
        BattleClassMode mode)
    {
        return mode == BattleClassMode.Light
            ? BattleClassMode.Dark
            : BattleClassMode.Light;
    }

    // ====================================
    // 全ユニットへ適用
    // ====================================

    /// <summary>
    /// シーンにいる初期化済みユニットへ
    /// 現在の時間帯を適用する。
    ///
    /// 将来、増援が発生した場合にも呼び出せる。
    /// </summary>
    public void ApplyCurrentModeToExistingUnits()
    {
        UnitView[] units;

#if UNITY_2022_2_OR_NEWER
        units = FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        units = FindObjectsOfType<UnitView>();
#endif

        int changedCount = 0;
        int skippedCount = 0;

        foreach (UnitView unit in units)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy ||
                unit.State == null)
            {
                skippedCount++;
                continue;
            }

            UnitBattleStats stats =
                unit.State.CombatStats;

            if (stats == null)
            {
                skippedCount++;
                continue;
            }

            // 朝夜が異なればステータスを再計算
            if (stats.ClassMode != CurrentMode)
            {
                stats.SetClassMode(CurrentMode);

                changedCount++;
            }

            // 今回追加した移動力も同期する
            unit.State.SetMoveRange(stats.MoveRange);

            if (showLogs)
            {
                Debug.Log(
                    $"【クラス適用】{stats.CharacterName}" +
                    $" / {stats.CurrentClassName}" +
                    $" ({stats.CurrentClassId})" +
                    $"\nHP: {stats.CurrentHP}/{stats.MaxHP}" +
                    $"\nATK: {stats.Attack:0.##}" +
                    $"\n移動力: {unit.State.MoveRange}",
                    unit
                );
            }
        }

        if (showLogs)
        {
            Debug.Log(
                "【昼夜反映結果】" +
                $"クラス変更{changedCount}体 / " +
                $"未初期化など{skippedCount}体",
                this
            );
        }
    }

    // ====================================
    // 開発用Tキー
    // ====================================

    private void Update()
    {
        if (!enableDebugToggleKey)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            keyboard.tKey.wasPressedThisFrame)
        {
            ToggleMode();
        }
    }

    // ====================================
    // UI表示
    // ====================================

    private string GetModeName(
        BattleClassMode mode)
    {
        return mode == BattleClassMode.Light
            ? "朝"
            : "夜";
    }

    private void UpdateTimeOfDayText()
    {
        if (timeOfDayText == null)
        {
            return;
        }

        timeOfDayText.text =
            $"現在：{GetModeName(CurrentMode)}";
    }

    // ====================================
    // 他のスクリプトから参照
    // ====================================

    public bool IsLight()
    {
        return CurrentMode == BattleClassMode.Light;
    }

    public bool IsDark()
    {
        return CurrentMode == BattleClassMode.Dark;
    }
}
