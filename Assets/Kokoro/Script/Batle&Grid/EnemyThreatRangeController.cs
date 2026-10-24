
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第13段階：敵危険範囲の表示管理。
///
/// ・PLAYER PHASEで表示
/// ・ENEMY PHASEでは非表示
/// ・Vキーで表示／非表示を切り替え
/// ・ユニットの位置変更を検知
/// ・昼夜クラス切り替えを検知
/// ・必要なときだけ再計算
///
/// 表示には第12段階の
/// AttackRangeOverlayを再利用する。
/// </summary>
public sealed class EnemyThreatRangeController : MonoBehaviour
{
    // ======================================
    // Inspector参照
    // ======================================

    [Header("戦闘グリッド")]
    [SerializeField]
    private BattleGrid grid;

    [Header("ターン管理")]
    [SerializeField]
    private TurnManager turnManager;

    [Header("危険範囲表示")]
    [SerializeField]
    private AttackRangeOverlay threatOverlay;

    [Header("表示設定")]
    [SerializeField]
    private bool showOnPlayerPhaseStart = true;

    [Header("開発用")]
    [SerializeField]
    private bool enableDebugToggleKey = true;

    [SerializeField]
    private bool showLogs = true;

    // ======================================
    // 内部データ
    // ======================================

    // ステージ内の味方・敵
    private UnitView[] cachedUnits;

    // 現在表示中かどうか
    private bool isVisible;

    // 再計算が必要か
    private bool needsRefresh = true;

    // 前回の配置・クラス状態
    private int lastSnapshotHash;

    public bool IsVisible => isVisible;

    // ======================================
    // 初期化
    // ======================================

    private void Awake()
    {
        if (grid == null ||
            turnManager == null ||
            threatOverlay == null)
        {
            Debug.LogError(
                "EnemyThreatRangeController: " +
                "Inspectorの参照が不足しています。",
                this
            );

            enabled = false;
            return;
        }

        // 最初は範囲を消しておく。
        threatOverlay.Clear();
    }

    private void OnEnable()
    {
        // 第11段階でも使用している
        // TurnManagerのフェーズ変更イベント。
        if (turnManager != null)
        {
            turnManager.PhaseChanged +=
                OnPhaseChanged;
        }
    }

    private IEnumerator Start()
    {
        if (!enabled)
        {
            yield break;
        }

        // BattleStageLoader.Startによる
        // ユニット生成を待つ。
        yield return null;

        RefreshUnits();

        // この時点ですでに味方フェーズなら、
        // 初回の危険範囲表示を有効にする。
        if (turnManager.CurrentPhase ==
            BattlePhase.Player)
        {
            SetVisible(showOnPlayerPhaseStart);
        }
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.PhaseChanged -=
                OnPhaseChanged;
        }

        if (threatOverlay != null)
        {
            threatOverlay.Clear();
        }

        isVisible = false;
    }

    // ======================================
    // ユニット一覧の取得
    // ======================================

    /// <summary>
    /// シーンに存在するUnitViewを再取得する。
    ///
    /// ステージ開始時・新しい味方フェーズ開始時
    /// に呼び出す。
    ///
    /// 将来、増援ユニットを追加したときも
    /// この関数を呼べる。
    /// </summary>
    public void RefreshUnits()
    {
#if UNITY_2022_2_OR_NEWER
        cachedUnits = FindObjectsByType<UnitView>(
            FindObjectsSortMode.None
        );
#else
        cachedUnits = FindObjectsOfType<UnitView>();
#endif

        needsRefresh = true;
    }

    // ======================================
    // フェーズ変更
    // ======================================

    /// <summary>
    /// TurnManagerのPhaseChangedから呼ばれる。
    ///
    /// PLAYER PHASE：
    ///     最新状態を取得して表示
    ///
    /// ENEMY PHASE：
    ///     危険範囲を非表示
    /// </summary>
    private void OnPhaseChanged(
        BattlePhase phase,
        int turnNumber)
    {
        if (phase == BattlePhase.Player)
        {
            RefreshUnits();

            SetVisible(showOnPlayerPhaseStart);
        }
        else
        {
            SetVisible(false);
        }
    }

    // ======================================
    // Vキー入力
    // ======================================

    private void Update()
    {
        if (!enableDebugToggleKey ||
            turnManager == null)
        {
            return;
        }

        // PLAYER PHASEのみ操作可能
        if (turnManager.CurrentPhase !=
            BattlePhase.Player)
        {
            return;
        }

        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.vKey.wasPressedThisFrame)
        {
            SetVisible(!isVisible);
        }
    }

    // ======================================
    // 表示・非表示の切り替え
    // ======================================

    /// <summary>
    /// 他のUIからも呼び出せる公開関数。
    ///
    /// visible:
    ///     true  = 表示する
    ///     false = 表示しない
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (turnManager == null ||
            threatOverlay == null)
        {
            return;
        }

        // 敵フェーズでは表示しない。
        if (visible &&
            turnManager.CurrentPhase !=
                BattlePhase.Player)
        {
            return;
        }

        isVisible = visible;

        if (!isVisible)
        {
            threatOverlay.Clear();
            return;
        }

        // 表示ONに切り替えたときは、
        // 必ず最新状態から計算する。
        needsRefresh = true;
    }

    // ======================================
    // 変更検知
    // ======================================

    private void LateUpdate()
    {
        if (!isVisible ||
            cachedUnits == null ||
            turnManager == null ||
            turnManager.CurrentPhase !=
                BattlePhase.Player)
        {
            return;
        }

        int currentSnapshot =
            CalculateSnapshotHash();

        // 配置・クラス・移動力などに
        // 変更がなければ再計算しない。
        if (!needsRefresh &&
            currentSnapshot == lastSnapshotHash)
        {
            return;
        }

        RecalculateThreatRange();

        lastSnapshotHash =
            currentSnapshot;

        needsRefresh = false;
    }

    /// <summary>
    /// 各ユニットの状態をハッシュにまとめる。
    ///
    /// この値が変化した場合にだけ
    /// 危険範囲の再計算を行う。
    ///
    /// ハッシュは変更検知の簡易手段。
    /// 将来は専用イベントへ変更してもよい。
    /// </summary>
    private int CalculateSnapshotHash()
    {
        unchecked
        {
            int hash = 17;

            hash = hash * 31 + grid.Width;
            hash = hash * 31 + grid.Height;

            foreach (UnitView unit in cachedUnits)
            {
                if (unit == null ||
                    !unit.gameObject.activeInHierarchy ||
                    unit.State == null)
                {
                    hash = hash * 31;
                    continue;
                }

                UnitState state =
                    unit.State;

                // どのユニットか識別する
                hash = hash * 31 +
                    unit.GetInstanceID();

                // 味方か敵か
                hash = hash * 31 +
                    (int)state.Team;

                // グリッド座標
                hash = hash * 31 +
                    state.Position.x;

                hash = hash * 31 +
                    state.Position.y;

                // 移動力
                hash = hash * 31 +
                    state.MoveRange;

                UnitBattleStats stats =
                    state.CombatStats;

                if (stats == null)
                {
                    hash = hash * 31;
                    continue;
                }

                // 昼夜クラス
                hash = hash * 31 +
                    (int)stats.ClassMode;

                // 攻撃射程
                hash = hash * 31 +
                    stats.MinAttackRange;

                hash = hash * 31 +
                    stats.MaxAttackRange;

                // HP0による戦闘不能状態
                hash = hash * 31 +
                    (stats.IsDead ? 1 : 0);

                // 攻撃形状
                string rangeType =
                    stats.RangeType;

                hash = hash * 31 +
                    (rangeType != null
                        ? rangeType.GetHashCode()
                        : 0);
            }

            return hash;
        }
    }

    // ======================================
    // 危険範囲の再計算と表示
    // ======================================

    private void RecalculateThreatRange()
    {
        if (grid == null ||
            threatOverlay == null ||
            cachedUnits == null)
        {
            return;
        }

        var dangerCells =
            EnemyThreatRangeCalculator.Calculate(
                grid,
                cachedUnits
            );

        // 第12段階の赤枠表示を再利用
        threatOverlay.ShowRange(
            dangerCells
        );

        if (showLogs)
        {
            Debug.Log(
                "【敵の危険範囲】" +
                $"{dangerCells.Count}マスを表示",
                this
            );
        }
    }
}
